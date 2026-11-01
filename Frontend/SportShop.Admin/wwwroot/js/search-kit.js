// Bộ công cụ tìm kiếm dùng chung cho trang quản trị: tìm không phân biệt hoa thường và dấu tiếng Việt, nhiều từ khóa (phải khớp đủ),
// và bảng "Tìm kiếm nâng cao" tự dựng theo các cột của bảng (chữ, danh sách chọn, khoảng ngày, khoảng tiền / số).
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

    // Dựng bảng tìm kiếm nâng cao cho một .dtable: nút .js-adv mở / đóng khung .adv-panel, mỗi cột một ô lọc.
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
            fields.push({ index: i, label: th.textContent.trim(), type: kind.type, values: kind.values || [], a: null, b: null });
        });
        if (fields.length === 0) { btn.hidden = true; return null; }

        var grid = document.createElement('div');
        grid.className = 'adv-grid';
        fields.forEach(function (f) {
            var wrap = document.createElement('label');
            wrap.className = 'adv-field';
            var cap = document.createElement('span'); cap.textContent = f.label; wrap.appendChild(cap);
            function box(placeholder, type) {
                var el = document.createElement('input'); el.type = type || 'text'; el.placeholder = placeholder || ''; el.autocomplete = 'off';
                el.addEventListener('input', changed); return el;
            }
            if (f.type === 'choice') {
                var sel = document.createElement('select');
                sel.innerHTML = '<option value="">Tất cả</option>' + f.values.map(function (v) { return '<option>' + v.replace(/&/g, '&amp;').replace(/</g, '&lt;') + '</option>'; }).join('');
                sel.addEventListener('change', changed); f.a = sel; wrap.appendChild(sel);
            } else if (f.type === 'date') {
                var pair = document.createElement('div'); pair.className = 'adv-pair'; wrap.classList.add('adv-span2');
                f.a = box('', 'date'); f.b = box('', 'date'); pair.appendChild(f.a); pair.appendChild(f.b); wrap.appendChild(pair);
            } else if (f.type === 'money' || f.type === 'number') {
                var p2 = document.createElement('div'); p2.className = 'adv-pair'; wrap.classList.add('adv-span2');
                f.a = box('Từ', 'number'); f.b = box('Đến', 'number'); p2.appendChild(f.a); p2.appendChild(f.b); wrap.appendChild(p2);
            } else {
                f.a = box('Chứa...'); wrap.appendChild(f.a);
            }
            grid.appendChild(wrap);
        });
        var foot = document.createElement('div');
        foot.className = 'adv-foot';
        foot.innerHTML = '<button type="button" class="btn-outline js-adv-reset">Xóa bộ lọc</button>';
        panel.appendChild(grid); panel.appendChild(foot);

        function active(f) { return f.a && (f.a.value !== '' || (f.b && f.b.value !== '')); }
        function count() { return fields.filter(active).length; }
        function changed() {
            var n = count(), badge = btn.querySelector('.adv-count');
            if (badge) { badge.textContent = n; badge.hidden = n === 0; }
            onChange();
        }
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            panel.hidden = !panel.hidden;
            btn.setAttribute('aria-expanded', String(!panel.hidden));
        });
        foot.querySelector('.js-adv-reset').addEventListener('click', function () {
            fields.forEach(function (f) { if (f.a) f.a.value = ''; if (f.b) f.b.value = ''; });
            changed();
        });

        // Dòng khớp khi thỏa mọi ô lọc đang nhập
        function rowMatches(tr) {
            return fields.every(function (f) {
                if (!active(f)) return true;
                var text = tr.children[f.index] ? tr.children[f.index].textContent.trim() : '';
                if (f.type === 'choice') return text === f.a.value;
                if (f.type === 'date') {
                    var d = parseDate(text); if (d === null) return false;
                    var lo = inputDate(f.a.value), hi = inputDate(f.b.value);
                    return (lo === null || d >= lo) && (hi === null || d <= hi);
                }
                if (f.type === 'money' || f.type === 'number') {
                    var n = parseNumber(text); if (n === null) return false;
                    return (f.a.value === '' || n >= parseFloat(f.a.value)) && (f.b.value === '' || n <= parseFloat(f.b.value));
                }
                return matches(text, f.a.value);
            });
        }
        return { matches: rowMatches, count: count };
    }

    return { fold: fold, matches: matches, parseNumber: parseNumber, parseDate: parseDate, advancedPanel: advancedPanel };
})();
