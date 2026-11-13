// Tìm kiếm dùng chung của trang quản trị: không phân biệt hoa thường và dấu, nhiều từ khóa; bảng "Tìm kiếm nâng cao" tự dựng theo cột. admin.js và các trang Sản phẩm, Phản hồi cùng gọi SearchKit.matches().
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
        // Kiểm tra đa số giá trị của cột thỏa điều kiện test.
        function most(test) { return filled.filter(test).length >= Math.max(1, Math.ceil(filled.length * 0.6)); }
        if (most(function (t) { return parseDate(t) !== null; })) return { type: 'date' };
        if (most(function (t) { return /\d\s*[₫đ]/.test(t); })) return { type: 'money' };
        if (most(function (t) { var m = /^-?(\d[\d.,]*)(\s*\S+(\s\S+)?)?$/.exec(t); return !!m && t.length <= 24 && m[1].replace(/[.,]/g, '').length <= 8; })) return { type: 'number' };
        var distinct = filled.filter(function (t, i) { return filled.indexOf(t) === i; });
        if (distinct.length >= 2 && distinct.length <= 8 && filled.length > distinct.length) return { type: 'choice', values: distinct.sort() };
        return { type: 'text' };
    }

    // Toán tử quan hệ dùng ngầm theo kiểu cột (không có ô chọn trên giao diện): số / tiền / ngày dùng ≥ (nhập 1.000.000 thì ra mọi dòng từ 1.000.000 trở lên), danh sách chọn dùng = (bằng), chữ dùng "chứa".
    var DEFAULT_OP = { number: 'gte', money: 'gte', date: 'gte', choice: 'eq', text: 'contains' };

    // So sánh hai giá trị đã quy về số (số, tiền, mốc thời gian) theo toán tử quan hệ: =, ≠, >, ≥, <, ≤.
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

    // ===== Bộ lọc nâng cao theo từng trang: ô chọn, khoảng Từ - Đến, khoảng ngày, nút Lọc nhanh =====

    // Dữ liệu riêng của một dòng / thẻ (thuộc tính data-f, dạng JSON), đọc một lần rồi nhớ lại.
    function dataOf(el) {
        if (el._f === undefined) {
            try { el._f = JSON.parse(el.getAttribute('data-f') || 'null') || {}; } catch (e) { el._f = {}; }
        }
        return el._f;
    }

    // Giá trị chữ của một ô lọc trên dòng: lấy từ data-f (f.data) hoặc từ chữ trong cột bảng (f.col).
    function rawValue(el, f) {
        if (f.data) { var d = dataOf(el)[f.data]; return d == null ? '' : String(d); }
        var cell = el.children[f.col];
        return cell ? cell.textContent.trim() : '';
    }

    // "15.149.150đ" hoặc "1500" -> số; không có số thì null.
    function numberOf(raw) {
        if (/^-?\d+$/.test(raw)) return parseInt(raw, 10);
        return parseNumber(raw);
    }

    // "2026-10-14" hoặc "14/10/2026 · 10:05" -> mốc thời gian đầu ngày; không phải ngày thì null.
    function dateOf(raw) {
        return /^\d{4}-\d{2}-\d{2}/.test(raw) ? inputDate(raw.slice(0, 10)) : parseDate(raw);
    }

    // Một điều kiện lọc: kind (select | text | number | date), toán tử op (eq | contains | gte | lte), chuỗi giá trị `raw` của dòng và giá trị người dùng chọn.
    function ruleTest(kind, op, raw, value) {
        if (kind === 'number' || kind === 'date') {
            var left = kind === 'date' ? dateOf(raw) : numberOf(raw);
            var right = kind === 'date' ? inputDate(value) : parseFloat(value);
            if (left === null || right === null || isNaN(right)) return false;
            if (op === 'lte') return left <= right;
            if (op === 'eq') return left === right;
            return left >= right;
        }
        if (op === 'contains' || kind === 'text') return matches(raw, value);
        return fold(raw) === fold(value);
    }

    // Dựng khung "Lọc nhanh" + bảng bộ lọc cho một danh sách phần tử (dòng bảng hoặc thẻ) theo cấu hình spec; onChange() báo khi bộ lọc đổi.
    // Trả về { matches(el), count(), setResult(n) }: một phần tử thỏa khi mọi ô lọc đang nhập đều đúng (Và) và mọi nút Lọc nhanh đang bật đều đúng (Và).
    function filterPanel(btn, panel, items, spec, onChange) {
        var fields = spec.fields || [], quick = spec.quick || [];
        var storeKey = 'advfilter:' + spec.id;
        var byKey = {};
        fields.forEach(function (f) { byKey[f.key] = f; });
        panel.innerHTML = '';
        panel.classList.add('adv-modern');

        var chips = [], controls = [];
        var result;

        // Thoát ký tự HTML.
        function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); }
        // Các giá trị khác nhau của một ô chọn (lấy từ dữ liệu thật của trang khi cấu hình không ghi sẵn).
        function optionsOf(f) {
            if (f.options && f.options.length) return f.options;
            var seen = {}, list = [];
            items.forEach(function (el) {
                var v = rawValue(el, f);
                if (v && !seen[v]) { seen[v] = 1; list.push(v); }
            });
            return list.sort(function (a, b) { return a.localeCompare(b, 'vi'); });
        }
        // Điều kiện của nút Lọc nhanh đúng với phần tử không.
        function quickTest(el, rule) {
            var f = byKey[rule.key];
            var raw = f ? rawValue(el, f) : (dataOf(el)[rule.key] == null ? '' : String(dataOf(el)[rule.key]));
            var kind = f ? f.kind : (rule.op === 'contains' ? 'text' : (rule.op === 'eq' && isNaN(parseFloat(rule.value)) ? 'select' : 'number'));
            return ruleTest(kind, rule.op, raw, rule.value);
        }

        // Hàng "Lọc nhanh"
        if (quick.length) {
            var row = document.createElement('div');
            row.className = 'adv-quick';
            row.innerHTML = '<span>Lọc nhanh:</span>';
            quick.forEach(function (q) {
                var b = document.createElement('button');
                b.type = 'button'; b.className = 'adv-chip'; b.textContent = q.label;
                b.addEventListener('click', function () { b.classList.toggle('on'); changed(); });
                row.appendChild(b);
                chips.push({ el: b, rule: q });
            });
            panel.appendChild(row);
        }

        // Khung các ô lọc
        var box = document.createElement('div');
        box.className = 'adv-box';
        var grid = document.createElement('div');
        grid.className = 'adv-grid';
        fields.forEach(function (f) {
            var wrap = document.createElement('div');
            wrap.className = 'adv-f';
            if (f.kind === 'date' && fields.length > 4) wrap.classList.add('wide');
            var label = document.createElement('label');
            label.textContent = f.label;
            wrap.appendChild(label);
            var c = { field: f };
            if (f.kind === 'select') {
                var sel = document.createElement('select');
                sel.innerHTML = '<option value="">Tất cả</option>' + optionsOf(f).map(function (o) { return '<option value="' + esc(o) + '">' + esc(o) + '</option>'; }).join('');
                sel.addEventListener('change', changed);
                wrap.appendChild(sel);
                c.inputs = [sel];
            } else if (f.kind === 'text') {
                var txt = document.createElement('input');
                txt.type = 'text'; txt.autocomplete = 'off';
                txt.addEventListener('input', changed);
                wrap.appendChild(txt);
                c.inputs = [txt];
            } else {
                var pair = document.createElement('div');
                pair.className = 'adv-range';
                var a = document.createElement('input'), z = document.createElement('input'), dash = document.createElement('span');
                a.type = z.type = f.kind === 'date' ? 'date' : 'number';
                if (f.kind !== 'date') { a.min = z.min = '0'; a.placeholder = 'Từ'; z.placeholder = 'Đến'; }
                a.setAttribute('aria-label', f.label + ' từ'); z.setAttribute('aria-label', f.label + ' đến');
                dash.textContent = '–';
                a.addEventListener('input', changed); z.addEventListener('input', changed);
                pair.appendChild(a); pair.appendChild(dash); pair.appendChild(z);
                wrap.appendChild(pair);
                c.inputs = [a, z];
            }
            controls.push(c);
            grid.appendChild(wrap);
        });
        box.appendChild(grid);

        // Dòng kết quả và hai nút Xóa bộ lọc / Lưu bộ lọc
        var bar = document.createElement('div');
        bar.className = 'adv-bar';
        result = document.createElement('span');
        result.className = 'adv-result';
        var actions = document.createElement('div');
        actions.className = 'adv-actions';
        actions.innerHTML = '<button type="button" class="btn-outline js-adv-clear">Xóa bộ lọc</button>' +
            '<button type="button" class="btn-outline js-adv-save"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg> Lưu bộ lọc</button>';
        bar.appendChild(result); bar.appendChild(actions);
        box.appendChild(bar);
        panel.appendChild(box);

        // Ô lọc đang có giá trị chưa.
        function isActive(c) {
            return c.inputs.some(function (i) { return i.value !== ''; });
        }
        // Số điều kiện đang bật (ô lọc có giá trị + nút Lọc nhanh đang bật).
        function count() {
            return controls.filter(isActive).length + chips.filter(function (x) { return x.el.classList.contains('on'); }).length;
        }
        // Cập nhật số đếm trên nút và lọc lại danh sách khi điều kiện đổi.
        function changed() {
            var n = count(), badge = btn.querySelector('.adv-count');
            if (badge) { badge.textContent = n; badge.hidden = false; }
            onChange();
        }
        // Phần tử thỏa mọi điều kiện đang bật: toán tử logic Và (&&) giữa các điều kiện, toán tử quan hệ >=, <=, = trong từng ô.
        function rowMatches(el) {
            for (var i = 0; i < chips.length; i++) {
                if (chips[i].el.classList.contains('on') && !quickTest(el, chips[i].rule)) return false;
            }
            for (var j = 0; j < controls.length; j++) {
                var c = controls[j], f = c.field;
                if (!isActive(c)) continue;
                var raw = rawValue(el, f);
                if (f.kind === 'number' || f.kind === 'date') {
                    if (c.inputs[0].value !== '' && !ruleTest(f.kind, 'gte', raw, c.inputs[0].value)) return false;
                    if (c.inputs[1].value !== '' && !ruleTest(f.kind, 'lte', raw, c.inputs[1].value)) return false;
                } else if (!ruleTest(f.kind, 'eq', raw, c.inputs[0].value)) return false;
            }
            return true;
        }
        // Đặt lại toàn bộ ô lọc và nút Lọc nhanh.
        function reset() {
            controls.forEach(function (c) { c.inputs.forEach(function (i) { i.value = ''; }); });
            chips.forEach(function (x) { x.el.classList.remove('on'); });
        }
        // Ghi bộ lọc hiện tại vào trình duyệt để lần mở sau dùng lại.
        function save() {
            var state = { f: {}, q: [] };
            controls.forEach(function (c) { if (isActive(c)) state.f[c.field.key] = c.inputs.map(function (i) { return i.value; }); });
            chips.forEach(function (x) { if (x.el.classList.contains('on')) state.q.push(x.rule.label); });
            try { localStorage.setItem(storeKey, JSON.stringify(state)); } catch (e) { }
            if (typeof showToast === 'function') showToast('Đã lưu bộ lọc');
        }
        // Nạp lại bộ lọc đã lưu (nếu có).
        function restore() {
            var state = null;
            try { state = JSON.parse(localStorage.getItem(storeKey) || 'null'); } catch (e) { }
            if (!state) return;
            controls.forEach(function (c) {
                var v = state.f && state.f[c.field.key];
                if (v) c.inputs.forEach(function (i, k) { i.value = v[k] || ''; });
            });
            chips.forEach(function (x) { if ((state.q || []).indexOf(x.rule.label) >= 0) x.el.classList.add('on'); });
        }

        actions.querySelector('.js-adv-clear').addEventListener('click', function () {
            reset();
            try { localStorage.removeItem(storeKey); } catch (e) { }
            changed();
        });
        actions.querySelector('.js-adv-save').addEventListener('click', save);
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            panel.hidden = !panel.hidden;
            btn.setAttribute('aria-expanded', String(!panel.hidden));
            btn.classList.toggle('open', !panel.hidden);
        });
        restore();
        var badge0 = btn.querySelector('.adv-count');
        if (badge0) { badge0.textContent = count(); badge0.hidden = false; }

        return {
            matches: rowMatches,
            count: count,
            setResult: function (n) { result.innerHTML = 'Tìm thấy <b>' + n + '</b> ' + esc(spec.noun || 'kết quả'); }
        };
    }

    // Dựng bảng tìm kiếm nâng cao cho .dtable: nút .js-adv mở khung .adv-panel, điều kiện "[Không] cột – giá trị" (toán tử quan hệ theo DEFAULT_OP, toán tử logic Không / Và / Hoặc); onChange() báo khi lọc đổi, trả về { matches(tr), count() }.
    function advancedPanel(card, rows, onChange) {
        var btn = card.querySelector('.js-adv'), panel = card.querySelector('.adv-panel');
        if (!btn || !panel) return null;
        // Bảng có cấu hình bộ lọc theo mẫu thiết kế (data-filter) thì dùng filterPanel(); các bảng còn lại giữ bộ lọc cột – giá trị.
        if (card.dataset.filter) return filterPanel(btn, panel, rows, JSON.parse(card.dataset.filter), onChange);

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

        // Thoát ký tự HTML.
        function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;'); }
        // Điều kiện đã có giá trị lọc chưa.
        function isActive(c) { return c.value.value !== ''; }
        // Đếm số điều kiện đang có giá trị.
        function count() { return conds.filter(isActive).length; }
        // Cập nhật số đếm trên nút và lọc lại bảng khi điều kiện đổi.
        function changed() {
            var n = count(), badge = btn.querySelector('.adv-count');
            if (badge) { badge.textContent = n; badge.hidden = n === 0; }
            onChange();
        }

        // Dựng ô nhập giá trị theo kiểu của cột đang chọn.
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
        }

        // Sắp xếp nhãn nối của các dòng điều kiện.
        function layout() {
            conds.forEach(function (c, i) { c.join.hidden = i === 0; c.lead.hidden = i !== 0; });
        }

        // Thêm một dòng điều kiện lọc.
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
            c.not = document.createElement('select');
            c.not.className = 'adv-not';
            c.not.setAttribute('aria-label', 'Phủ định điều kiện');
            c.not.innerHTML = '<option value="">—</option><option value="not">Không</option>';
            c.not.addEventListener('change', changed);
            c.field = document.createElement('select');
            c.field.innerHTML = fields.map(function (f, i) { return '<option value="' + i + '">' + esc(f.label) + '</option>'; }).join('');
            c.del = document.createElement('button');
            c.del.type = 'button'; c.del.className = 'adv-del'; c.del.setAttribute('aria-label', 'Xóa điều kiện'); c.del.textContent = '×';
            buildValue(c);
            c.field.addEventListener('change', function () { buildValue(c); row.insertBefore(c.value, c.del); changed(); });
            c.del.addEventListener('click', function () {
                if (conds.length === 1) { c.value.value = ''; } else { conds.splice(conds.indexOf(c), 1); row.remove(); layout(); }
                changed();
            });
            row.appendChild(c.join); row.appendChild(c.lead); row.appendChild(c.not); row.appendChild(c.field); row.appendChild(c.value); row.appendChild(c.del);
            conds.push(c); list.appendChild(row); layout();
        }

        foot.querySelector('.js-adv-add').addEventListener('click', addCondition);
        foot.querySelector('.js-adv-reset').addEventListener('click', function () {
            conds.slice(1).forEach(function (c) { c.row.remove(); });
            conds.length = 1;
            conds[0].value.value = '';
            conds[0].join.value = 'and';
            conds[0].not.value = '';
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
                    var ok = test(f.type, DEFAULT_OP[f.type], c.value.value, text);
                    return c.not.value === 'not' ? !ok : ok;
                });
            });
        }
        return { matches: rowMatches, count: count };
    }

    return { fold: fold, matches: matches, parseNumber: parseNumber, parseDate: parseDate, advancedPanel: advancedPanel, filterPanel: filterPanel };
})();
