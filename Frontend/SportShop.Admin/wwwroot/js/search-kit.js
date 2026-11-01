// Bộ công cụ tìm kiếm dùng chung cho trang quản trị: tìm không phân biệt hoa thường và dấu tiếng Việt, nhiều từ khóa (phải khớp đủ),
// và bảng "Tìm kiếm nâng cao" tự dựng theo các cột của bảng: điều kiện "cột – toán tử quan hệ – giá trị" nối bằng toán tử logic Và / Hoặc.
// admin.js (bảng .dtable) và các trang Sản phẩm, Phản hồi cùng dùng SearchKit.matches() nên cách tìm giống nhau ở mọi nơi.
var SearchKit = (function () {
    // Chữ thường, bỏ dấu tiếng Việt, gộp khoảng trắng: "Giày  Chạy" -> "giay chay".
    function fold(s) {
        return String(s == null ? '' : s).toLowerCase().replace(/đ/g, 'd').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').trim();
    }

    // Văn bản khớp khi chứa đủ mọi từ trong chuỗi tìm (không cần đúng thứ tự); chuỗi tìm trống thì khớp tất cả.
    function matches(text, query) {
        var words = fold(query).split(' ').filter(Boolean);
        if (words.length === 0) return true;
        var f = fold(text);
        return words.every(function (w) { return f.indexOf(w) >= 0; });
    }

    // "3.326.000₫" -> 3326000; không có số thì trả về null.
    function parseNumber(text) {
        var n = parseFloat(String(text).replace(/[^\d,]/g, '').replace(',', '.'));
        return isNaN(n) ? null : (/^\s*-/.test(text) ? -n : n);
    }

    // Giá trị của ô nhập ngày (yyyy-MM-dd) -> mốc thời gian đầu ngày theo giờ máy; trống thì null.
    function inputDate(v) {
        var p = String(v).split('-');
        return p.length === 3 ? new Date(+p[0], +p[1] - 1, +p[2]).getTime() : null;
    }

    // "14/10/2026 · 10:05" -> mốc thời gian của ngày 14/10/2026; không phải ngày thì null.
    function parseDate(text) {
        var m = /(\d{1,2})\/(\d{1,2})\/(\d{4})/.exec(String(text));
        return m ? new Date(+m[3], +m[2] - 1, +m[1]).getTime() : null;
    }

    // Kiểu cột suy ra từ nội dung các ô: date | money | number | choice (ít giá trị khác nhau) | text.
    function columnType(cells) {
        var filled = cells.filter(function (t) { return t && t !== '-'; });
        if (filled.length === 0) return { type: 'text' };
        function most(test) { return filled.filter(test).length >= Math.max(1, Math.ceil(filled.length * 0.6)); }
        if (most(function (t) { return parseDate(t) !== null; })) return { type: 'date' };
        if (most(function (t) { return /\d\s*[₫đ]/.test(t); })) return { type: 'money' };
        // Cột số: ô bắt đầu bằng số ngắn (tối đa 8 chữ số, không phải số điện thoại / mã dài), có thể kèm đơn vị như "5 sản phẩm"
        if (most(function (t) { var m = /^-?(\d[\d.,]*)(\s*\S+(\s\S+)?)?$/.exec(t); return !!m && t.length <= 24 && m[1].replace(/[.,]/g, '').length <= 8; })) return { type: 'number' };
        var distinct = filled.filter(function (t, i) { return filled.indexOf(t) === i; });
        if (distinct.length >= 2 && distinct.length <= 8 && filled.length > distinct.length) return { type: 'choice', values: distinct.sort() };
        return { type: 'text' };
    }

    // Toán tử quan hệ theo kiểu cột: chữ (chứa, không chứa, bằng, khác, bắt đầu bằng), số / tiền / ngày (=, ≠, >, ≥, <, ≤), danh sách chọn (=, ≠).
    var COMPARE_OPS = [['eq', '= (bằng)'], ['neq', '≠ (khác)'], ['gt', '> (lớn hơn)'], ['gte', '≥ (từ)'], ['lt', '< (nhỏ hơn)'], ['lte', '≤ (đến)']];
    var OPS = {
        text: [['contains', 'chứa'], ['notcontains', 'không chứa'], ['eq', 'bằng'], ['neq', 'khác'], ['starts', 'bắt đầu bằng']],
        number: COMPARE_OPS, money: COMPARE_OPS, date: COMPARE_OPS,
        choice: [['eq', '= (bằng)'], ['neq', '≠ (khác)']]
    };

    // So sánh hai giá trị đã quy về số (số, tiền, mốc thời gian) theo toán tử quan hệ.
    function compare(op, a, b) {
        switch (op) {
            case 'eq': return a === b;
            case 'neq': return a !== b;
            case 'gt': return a > b;
            case 'gte': return a >= b;
            case 'lt': return a < b;
            default: return a <= b;
        }
    }

    // Một điều kiện (kiểu cột, toán tử, giá trị) đúng với ô chữ `text` hay không.
    function test(type, op, value, text) {
        if (type === 'choice') return op === 'neq' ? text !== value : text === value;
        if (type === 'date' || type === 'money' || type === 'number') {
            var left = type === 'date' ? parseDate(text) : parseNumber(text);
            var right = type === 'date' ? inputDate(value) : parseFloat(value);
            if (left === null || right === null || isNaN(right)) return op === 'neq';
            return compare(op, left, right);
        }
        var f = fold(text), v = fold(value);
        switch (op) {
            case 'notcontains': return !matches(text, value);
            case 'eq': return f === v;
            case 'neq': return f !== v;
            case 'starts': return f.indexOf(v) === 0;
            default: return matches(text, value);
        }
    }

    // Dựng bảng tìm kiếm nâng cao cho một .dtable: nút .js-adv mở / đóng khung .adv-panel, mỗi dòng là một điều kiện "cột – toán tử – giá trị";
    // các điều kiện nối với nhau bằng Và / Hoặc (Và tính trước Hoặc, như toán tử logic thông thường). Điều kiện chưa nhập giá trị thì bỏ qua.
    // onChange() được gọi mỗi khi bộ lọc đổi; trả về { matches(tr), count() } để bảng lọc từng dòng.
    function advancedPanel(card, rows, onChange) {
        var btn = card.querySelector('.js-adv'), panel = card.querySelector('.adv-panel');
        if (!btn || !panel) return null;

        var heads = Array.prototype.slice.call(card.querySelectorAll('thead th'));
        var fields = [];
        heads.forEach(function (th, i) {
            if (th.classList.contains('col-act')) return;
            var cells = rows.map(function (tr) { return tr.children[i] ? tr.children[i].textContent.trim() : ''; });
            var kind = columnType(cells);
            fields.push({ index: i, label: th.textContent.trim(), type: kind.type, values: kind.values || [] });
        });
        if (fields.length === 0) { btn.hidden = true; return null; }

        var list = document.createElement('div');
        list.className = 'adv-rows';
        var foot = document.createElement('div');
        foot.className = 'adv-foot';
        foot.innerHTML = '<button type="button" class="btn-outline js-adv-add">+ Thêm điều kiện</button><button type="button" class="btn-outline js-adv-reset">Xóa bộ lọc</button>';
        panel.appendChild(list); panel.appendChild(foot);

        var conds = [];

        function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;'); }
        function options(pairs) { return pairs.map(function (p) { return '<option value="' + p[0] + '">' + esc(p[1]) + '</option>'; }).join(''); }
        function isActive(c) { return c.value.value !== ''; }
        function count() { return conds.filter(isActive).length; }
        function changed() {
            var n = count(), badge = btn.querySelector('.adv-count');
            if (badge) { badge.textContent = n; badge.hidden = n === 0; }
            onChange();
        }

        // Ô nhập giá trị và danh sách toán tử theo kiểu của cột đang chọn
        function buildValue(c) {
            var f = fields[c.field.selectedIndex], el;
            if (f.type === 'choice') {
                el = document.createElement('select');
                el.innerHTML = '<option value="">Chọn giá trị</option>' + f.values.map(function (v) { return '<option>' + esc(v) + '</option>'; }).join('');
                el.addEventListener('change', changed);
            } else {
                el = document.createElement('input');
                el.type = f.type === 'date' ? 'date' : (f.type === 'money' || f.type === 'number') ? 'number' : 'text';
                el.placeholder = f.type === 'text' ? 'Giá trị...' : ''; el.autocomplete = 'off';
                el.addEventListener('input', changed);
            }
            if (c.value) c.value.replaceWith(el);
            c.value = el;
            c.op.innerHTML = options(OPS[f.type]);
        }

        // Dòng đầu hiện chữ "Khi", các dòng sau hiện ô chọn Và / Hoặc
        function layout() {
            conds.forEach(function (c, i) { c.join.hidden = i === 0; c.lead.hidden = i !== 0; });
        }

        function addCondition() {
            var row = document.createElement('div');
            row.className = 'adv-row';
            var c = { row: row, value: null };
            c.join = document.createElement('select');
            c.join.className = 'adv-join';
            c.join.innerHTML = '<option value="and">Và</option><option value="or">Hoặc</option>';
            c.join.addEventListener('change', changed);
            c.lead = document.createElement('span');
            c.lead.className = 'adv-lead';
            c.lead.textContent = 'Khi';
            c.field = document.createElement('select');
            c.field.innerHTML = fields.map(function (f, i) { return '<option value="' + i + '">' + esc(f.label) + '</option>'; }).join('');
            c.op = document.createElement('select');
            c.del = document.createElement('button');
            c.del.type = 'button'; c.del.className = 'adv-del'; c.del.setAttribute('aria-label', 'Xóa điều kiện'); c.del.textContent = '×';
            buildValue(c);
            c.field.addEventListener('change', function () { buildValue(c); row.insertBefore(c.value, c.del); changed(); });
            c.op.addEventListener('change', changed);
            c.del.addEventListener('click', function () {
                if (conds.length === 1) { c.value.value = ''; } else { conds.splice(conds.indexOf(c), 1); row.remove(); layout(); }
                changed();
            });
            row.appendChild(c.join); row.appendChild(c.lead); row.appendChild(c.field); row.appendChild(c.op); row.appendChild(c.value); row.appendChild(c.del);
            conds.push(c); list.appendChild(row); layout();
        }

        foot.querySelector('.js-adv-add').addEventListener('click', addCondition);
        foot.querySelector('.js-adv-reset').addEventListener('click', function () {
            conds.slice(1).forEach(function (c) { c.row.remove(); });
            conds.length = 1;
            conds[0].value.value = '';
            conds[0].join.value = 'and';
            changed();
        });
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            panel.hidden = !panel.hidden;
            btn.setAttribute('aria-expanded', String(!panel.hidden));
        });
        addCondition();

        // Dòng thỏa biểu thức: các điều kiện đang nhập tách thành nhóm tại mỗi "Hoặc"; trong nhóm phải đúng hết (Và), đúng một nhóm là đủ (Hoặc)
        function rowMatches(tr) {
            var groups = [], current = null;
            conds.forEach(function (c) {
                if (!isActive(c)) return;
                if (current === null || c.join.value === 'or') { current = []; groups.push(current); }
                current.push(c);
            });
            if (groups.length === 0) return true;
            return groups.some(function (g) {
                return g.every(function (c) {
                    var f = fields[c.field.selectedIndex];
                    var text = tr.children[f.index] ? tr.children[f.index].textContent.trim() : '';
                    return test(f.type, c.op.value, c.value.value, text);
                });
            });
        }
        return { matches: rowMatches, count: count };
    }

    return { fold: fold, matches: matches, parseNumber: parseNumber, parseDate: parseDate, advancedPanel: advancedPanel };
})();
