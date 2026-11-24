// Biểu đồ trang Thống kê doanh thu: đọc thẳng số liệu (data-v, data-at) của bảng bên trái nên bảng và biểu đồ luôn cùng dữ liệu; kiểu vẽ theo data-chart của bảng.
(function () {
    var SERIES = ['#0f9688', '#ea6a0c', '#2563b8'], TOP = 10, NS = 'http://www.w3.org/2000/svg';

    // 1234567 -> "1.234.567đ", giống Fmt.Money().
    function money(v) { return Math.round(v).toLocaleString('vi-VN') + 'đ'; }

    // Rút gọn tiền cho trục và nhãn: 1.200.000 -> "1,2 tr", giống Fmt.Compact().
    function compact(v) {
        var a = Math.abs(v), f = function (n, d) { return n.toLocaleString('vi-VN', { maximumFractionDigits: d }); };
        return a >= 1e9 ? f(v / 1e9, 2) + ' tỷ' : a >= 1e6 ? f(v / 1e6, 1) + ' tr' : a >= 1e3 ? f(v / 1e3, 0) + 'k' : f(v, 0);
    }

    // Tạo phần tử HTML với class và chữ.
    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    // Tạo phần tử SVG với các thuộc tính.
    function svg(tag, attrs, parent, text) {
        var e = document.createElementNS(NS, tag);
        for (var k in attrs) e.setAttribute(k, attrs[k]);
        if (text != null) e.textContent = text;
        if (parent) parent.appendChild(e);
        return e;
    }

    // Ngày yyyy-MM-dd theo giờ máy.
    function day(s) { var p = s.split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function two(n) { return (n < 10 ? '0' : '') + n; }
    function dm(d) { return two(d.getDate()) + '/' + two(d.getMonth() + 1); }

    // Đọc bảng thành { cols, rows: [{ label, values, at }] }, gồm cả hàng đang ẩn vì phân trang.
    function readTable(table) {
        var rows = [];
        Array.prototype.forEach.call(table.tBodies[0].rows, function (tr) {
            var cells = tr.querySelectorAll('td[data-v]');
            if (cells.length === 0) return;
            rows.push({ label: tr.cells[0].textContent.trim(), at: tr.dataset.at || '', values: Array.prototype.map.call(cells, function (c) { return +c.dataset.v; }) });
        });
        return {
            cols: Array.prototype.map.call(table.tHead.querySelectorAll('th.num'), function (th) { return th.textContent.trim(); }),
            rows: rows, from: table.dataset.from, to: table.dataset.to
        };
    }

    // Gộp hóa đơn theo thời gian: một ngày thì theo giờ, đến 92 ngày theo ngày, đến 400 ngày theo tuần, dài hơn theo tháng; mốc không có hóa đơn ghi 0.
    function timeBuckets(data) {
        var from = day(data.from), to = day(data.to), days = Math.round((to - from) / 864e5) + 1;
        var unit = days === 1 ? 'giờ' : days <= 92 ? 'ngày' : days <= 400 ? 'tuần' : 'tháng', list = [], index = {};
        // Mốc bắt đầu của kỳ chứa thời điểm d.
        function startOf(d) {
            if (unit === 'giờ') return new Date(d.getFullYear(), d.getMonth(), d.getDate(), d.getHours());
            if (unit === 'tháng') return new Date(d.getFullYear(), d.getMonth(), 1);
            var s = new Date(d.getFullYear(), d.getMonth(), d.getDate());
            if (unit === 'tuần') s.setDate(s.getDate() - (s.getDay() + 6) % 7);
            return s;
        }
        // Thêm một mốc; nhãn kỳ đầu và cuối không vượt khoảng ngày đang chọn.
        function add(s) {
            var tip, label;
            if (unit === 'giờ') { label = s.getHours() + 'h'; tip = label + ' ngày ' + dm(s); }
            else if (unit === 'tháng') { label = (s.getMonth() + 1) + '/' + s.getFullYear(); tip = 'Tháng ' + label; }
            else if (unit === 'tuần') {
                var e = new Date(s.getFullYear(), s.getMonth(), s.getDate() + 6), a = s < from ? from : s, z = e > to ? to : e;
                label = dm(a); tip = 'Tuần ' + dm(a) + ' – ' + dm(z) + '/' + z.getFullYear();
            }
            else { label = dm(s); tip = 'Ngày ' + dm(s) + '/' + s.getFullYear(); }
            index[+s] = list.length;
            list.push({ label: label, tip: tip, value: 0, count: 0 });
        }
        var last = unit === 'giờ' ? new Date(to.getFullYear(), to.getMonth(), to.getDate(), 23) : to;
        for (var s = startOf(from); s <= last;) {
            add(s);
            s = unit === 'giờ' ? new Date(s.getFullYear(), s.getMonth(), s.getDate(), s.getHours() + 1)
                : unit === 'tháng' ? new Date(s.getFullYear(), s.getMonth() + 1, 1)
                : new Date(s.getFullYear(), s.getMonth(), s.getDate() + (unit === 'tuần' ? 7 : 1));
        }
        data.rows.forEach(function (r) {
            var b = list[index[+startOf(new Date(r.at))]];
            if (b) { b.value += r.values[0]; b.count++; }
        });
        return { unit: unit, list: list };
    }

    // Chuyển bảng thành dữ liệu vẽ: { type: line, points, series } hoặc { type: bars, items }.
    function model(table) {
        var data = readTable(table), kind = table.dataset.chart;
        if (!data.rows.some(function (r) { return r.values.some(Boolean); })) return null;
        if (kind === 'time') {
            var t = timeBuckets(data);
            return {
                type: 'line', sub: 'Tổng tiền hóa đơn theo ' + t.unit,
                points: t.list.map(function (b) { return { label: b.label, tip: b.tip, note: b.count + ' hóa đơn' }; }),
                series: [{ name: 'Tổng tiền hóa đơn', color: SERIES[0], values: t.list.map(function (b) { return b.value; }) }]
            };
        }
        var last = data.cols.length - 1, cols = data.cols.slice(0, last);
        if (kind === 'lines' && cols.length > 1)
            return {
                type: 'line', sub: 'Đơn vị: đồng, theo ' + cols[0].split(' ')[0].toLowerCase().replace(/^\d+$/, 'năm'),
                points: cols.map(function (c) { return { label: c.replace('Tháng ', 'T').replace('Quý ', 'Q'), tip: /^\d+$/.test(c) ? 'Năm ' + c : c }; }),
                series: data.rows.map(function (r, i) { return { name: r.label, color: SERIES[i], values: r.values.slice(0, last) }; })
            };
        var ranked = kind === 'bars', total = data.rows.reduce(function (s, r) { return s + r.values[last]; }, 0);
        var items = data.rows.map(function (r, i) {
            return { name: r.label, value: r.values[last], color: ranked ? '' : SERIES[i], parts: cols.length > 1 ? cols.map(function (c, k) { return [c, r.values[k]]; }) : [] };
        });
        if (ranked && items.length > TOP + 1) {
            var rest = items.slice(TOP);
            items = items.slice(0, TOP);
            items.push({
                name: 'Còn lại (' + rest.length + ')', other: true,
                value: rest.reduce(function (s, r) { return s + r.value; }, 0),
                parts: cols.length > 1 ? cols.map(function (c, k) { return [c, rest.reduce(function (s, r) { return s + r.parts[k][1]; }, 0)]; }) : []
            });
        }
        return { type: 'bars', items: items, total: ranked ? total : 0, sub: ranked ? (items.length > TOP ? TOP + ' mục cao nhất và phần còn lại, trên tổng ' + compact(total) : 'Tổng cộng ' + money(total)) : cols[0] };
    }

    // Các mốc trục tiền tròn số, luôn có 0.
    function ticks(min, max) {
        var span = Math.max(max, 0) - Math.min(min, 0) || 1, raw = span / 4, p = Math.pow(10, Math.floor(Math.log(raw) / Math.LN10)), n = raw / p;
        var step = (n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10) * p, out = [];
        for (var v = Math.floor(Math.min(min, 0) / step) * step; v < Math.max(max, 0) + step - 1e-9; v += step) out.push(Math.round(v));
        return out;
    }

    // Đặt hộp chú giải cạnh điểm (x, y) và giữ trong khung biểu đồ.
    function placeTip(tip, box, x, y) {
        tip.hidden = false;
        var w = tip.offsetWidth, h = tip.offsetHeight, left = x + 14;
        if (left + w > box.clientWidth) left = x - w - 14;
        tip.style.left = Math.max(0, Math.min(left, box.clientWidth - w)) + 'px';
        tip.style.top = Math.max(0, Math.min(y - h / 2, box.clientHeight - h)) + 'px';
    }

    // Nội dung hộp chú giải: tiêu đề và các dòng [giá trị, tên, màu].
    function fillTip(tip, title, lines) {
        tip.textContent = '';
        tip.appendChild(el('small', '', title));
        lines.forEach(function (l) {
            var row = el('div', 'sc-tip-row');
            if (l[2]) { var key = el('i'); key.style.background = l[2]; row.appendChild(key); }
            row.appendChild(el('b', '', l[0]));
            if (l[1]) row.appendChild(el('span', '', l[1]));
            tip.appendChild(row);
        });
    }

    // Biểu đồ đường: lưới, đường, vạch dò theo con trỏ hoặc phím mũi tên, chú giải mọi chuỗi tại mốc đang chọn.
    function drawLine(box, m) {
        var W = Math.max(box.clientWidth, 260), H = 320, L = 58, R = 18, T = 22, B = 34, n = m.points.length;
        var all = [].concat.apply([], m.series.map(function (s) { return s.values; })), ys = ticks(Math.min.apply(null, all), Math.max.apply(null, all));
        var y0 = ys[0], y1 = ys[ys.length - 1];
        // Tọa độ ngang của mốc i.
        function X(i) { return n === 1 ? (L + W - R) / 2 : L + i * (W - L - R) / (n - 1); }
        // Tọa độ dọc của giá trị v.
        function Y(v) { return T + (y1 - v) * (H - T - B) / (y1 - y0 || 1); }
        var root = svg('svg', { 'class': 'sc-svg', width: W, height: H, viewBox: '0 0 ' + W + ' ' + H, tabindex: 0, role: 'img', 'aria-label': m.sub + '. Số liệu đầy đủ ở bảng bên cạnh.' }, box);
        ys.forEach(function (v) {
            svg('line', { x1: L, x2: W - R, y1: Y(v), y2: Y(v), 'class': v === 0 ? 'sc-zero' : 'grid-line' }, root);
            svg('text', { x: L - 10, y: Y(v) + 4, 'text-anchor': 'end', 'class': 'axis' }, root, compact(v));
        });
        var every = Math.max(1, Math.ceil(n / Math.max(2, Math.floor((W - L - R) / 62))));
        m.points.forEach(function (p, i) {
            if (i % every && !(i === n - 1 && n > 1 && i % every > every * .7)) return;
            svg('text', { x: X(i), y: H - 10, 'text-anchor': n > 1 && i === n - 1 ? 'end' : 'middle', 'class': 'axis' }, root, p.label);
        });
        var single = m.series.length === 1;
        m.series.forEach(function (s) {
            var d = s.values.map(function (v, i) { return (i ? 'L' : 'M') + X(i).toFixed(2) + ' ' + Y(v).toFixed(2); }).join('');
            if (single) svg('path', { d: d + 'L' + X(n - 1) + ' ' + Y(0) + 'L' + X(0) + ' ' + Y(0) + 'Z', fill: s.color, opacity: .1 }, root);
            svg('path', { d: d, 'class': 'sc-line', stroke: s.color }, root);
            if (n <= 12) s.values.forEach(function (v, i) { svg('circle', { cx: X(i), cy: Y(v), r: 4, fill: s.color, 'class': 'sc-dot' }, root); });
        });
        if (single) {
            var vs = m.series[0].values, top = vs.indexOf(Math.max.apply(null, vs)), tx = X(top);
            if (vs[top] > 0) {
                if (n > 12) svg('circle', { cx: tx, cy: Y(vs[top]), r: 4, fill: m.series[0].color, 'class': 'sc-dot' }, root);
                svg('text', { x: tx, y: Y(vs[top]) - 9, 'text-anchor': tx > W - R - 50 ? 'end' : tx < L + 50 ? 'start' : 'middle', 'class': 'sc-peak' }, root, compact(vs[top]));
            }
        }
        var cross = svg('line', { y1: T, y2: H - B, 'class': 'sc-cross', visibility: 'hidden' }, root);
        var marks = m.series.map(function (s) { return svg('circle', { r: 5, fill: s.color, 'class': 'sc-dot', visibility: 'hidden' }, root); });
        var tip = el('div', 'sc-tip'), at = -1;
        tip.hidden = true;
        box.appendChild(tip);
        // Chọn mốc i: vạch dò, điểm và chú giải.
        function pick(i) {
            at = i = Math.max(0, Math.min(n - 1, i));
            cross.setAttribute('x1', X(i)); cross.setAttribute('x2', X(i)); cross.setAttribute('visibility', 'visible');
            marks.forEach(function (c, k) { c.setAttribute('cx', X(i)); c.setAttribute('cy', Y(m.series[k].values[i])); c.setAttribute('visibility', 'visible'); });
            fillTip(tip, m.points[i].tip, m.series.map(function (s) { return [money(s.values[i]), single ? m.points[i].note : s.name, s.color]; }));
            placeTip(tip, box, X(i), T + (H - T - B) / 2);
        }
        // Bỏ chọn khi con trỏ hoặc tiêu điểm rời biểu đồ.
        function clear() { at = -1; tip.hidden = true; [cross].concat(marks).forEach(function (e) { e.setAttribute('visibility', 'hidden'); }); }
        root.addEventListener('pointermove', function (e) {
            var x = e.clientX - root.getBoundingClientRect().left;
            pick(n === 1 ? 0 : Math.round((x - L) * (n - 1) / (W - L - R)));
        });
        root.addEventListener('pointerleave', clear);
        root.addEventListener('focus', function () { if (at < 0) pick(n - 1); });
        root.addEventListener('blur', clear);
        root.addEventListener('keydown', function (e) {
            var to = { ArrowLeft: at - 1, ArrowRight: at + 1, Home: 0, End: n - 1 }[e.key];
            if (to == null) return;
            e.preventDefault();
            pick(to);
        });
    }

    // Biểu đồ thanh ngang: tên và giá trị ở trên, thanh ở dưới; giá trị âm vẽ sang trái mốc 0; dòng Còn lại chỉ ghi số để không làm lệch tỷ lệ các thanh.
    function drawBars(box, m) {
        var vals = m.items.filter(function (i) { return !i.other; }).map(function (i) { return i.value; }), max = Math.max.apply(null, vals.concat(0)), min = Math.min.apply(null, vals.concat(0)), span = max - min || 1;
        var list = el('div', 'sc-bars'), tip = el('div', 'sc-tip');
        tip.hidden = true;
        m.items.forEach(function (it) {
            var row = el('div', 'sc-row' + (it.other ? ' other' : '')), head = el('div', 'sc-row-head'), track = el('div', 'sc-track'), bar = el('i');
            var share = m.total > 0 ? (it.value / m.total * 100).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + '%' : '';
            row.tabIndex = 0;
            head.appendChild(el('span', 'sc-name', it.name));
            head.appendChild(el('b', '', compact(it.value)));
            if (share) head.appendChild(el('small', '', share));
            bar.style.left = (Math.min(it.value, 0) - min) / span * 100 + '%';
            bar.style.width = Math.abs(it.value) / span * 100 + '%';
            if (it.color) bar.style.background = it.color;
            track.appendChild(bar);
            row.appendChild(head);
            row.appendChild(track);
            // Hiện giá trị đầy đủ, tỷ trọng và số tiền từng kỳ của thanh.
            function show() {
                fillTip(tip, it.name, [[money(it.value), share ? share + ' tổng' : '']].concat(it.parts.map(function (p) { return [money(p[1]), p[0]]; })));
                placeTip(tip, box, box.clientWidth / 2, row.offsetTop + row.offsetHeight + tip.offsetHeight / 2 + 4);
            }
            // Ẩn chú giải.
            function hide() { tip.hidden = true; }
            row.addEventListener('pointerenter', show);
            row.addEventListener('pointerleave', hide);
            row.addEventListener('focus', show);
            row.addEventListener('blur', hide);
            list.appendChild(row);
        });
        box.appendChild(list);
        box.appendChild(tip);
    }

    // Vẽ biểu đồ của một cặp bảng - biểu đồ và vẽ lại khi khung đổi bề rộng.
    function setup(pair) {
        var table = pair.querySelector('table[data-chart]'), card = pair.querySelector('.stat-chart');
        if (!table || !card) return;
        var m = model(table), box = card.querySelector('.sc-plot'), legend = card.querySelector('.sc-legend'), width = 0;
        if (!m) {
            card.querySelector('.empty').hidden = false;
            box.hidden = true;
            return;
        }
        card.querySelector('.sc-sub').textContent = m.sub;
        if (m.type === 'line' && m.series.length > 1) {
            legend.hidden = false;
            m.series.forEach(function (s) {
                var key = el('span', ''), dot = el('i');
                dot.style.background = s.color;
                key.appendChild(dot);
                key.appendChild(document.createTextNode(s.name));
                legend.appendChild(key);
            });
        }
        // Vẽ lại toàn bộ theo bề rộng hiện tại của khung.
        function draw() {
            if (box.clientWidth === width || box.clientWidth === 0) return;
            width = box.clientWidth;
            box.textContent = '';
            (m.type === 'line' ? drawLine : drawBars)(box, m);
        }
        draw();
        if (window.ResizeObserver) new ResizeObserver(draw).observe(box);
        else window.addEventListener('resize', draw);
    }
    document.querySelectorAll('.stat-pair').forEach(setup);
})();
