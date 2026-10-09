/* =====================================================================
   SportShop Admin - JavaScript giao diện (chưa gọi server/database)
   Các khối:
     1. Thu gọn / mở sidebar
     2. Thông báo nhỏ (toast) cho nút chưa có chức năng
     3. Bảng dữ liệu: tìm kiếm + bộ lọc + phân trang (.dtable)
     4. Phím tắt Ctrl+K
     5. POS: hóa đơn tạm khi bán hàng tại quầy
   ===================================================================== */

/* ---------- 1. Thu gọn / mở sidebar ---------- */
document.querySelectorAll('.js-nav-toggle').forEach(function (btn) {
    btn.addEventListener('click', function () {
        // Màn hình nhỏ: trượt sidebar ra/vào. Màn hình lớn: thu gọn còn icon.
        if (window.innerWidth <= 900) document.body.classList.toggle('nav-open');
        else document.body.classList.toggle('collapsed');
    });
});
// Bấm ra ngoài sidebar (trên điện thoại) thì đóng lại
document.addEventListener('click', function (e) {
    if (document.body.classList.contains('nav-open') && !e.target.closest('.sidebar') && !e.target.closest('.js-nav-toggle'))
        document.body.classList.remove('nav-open');
});

/* ---------- 2. Toast cho các nút chưa nối cơ sở dữ liệu ---------- */
var toastTimer;
function showToast(msg) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove('show'); }, 2600);
}
document.addEventListener('click', function (e) {
    var b = e.target.closest('.js-soon');
    if (b) showToast('Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu.');
});

/* ---------- 3. Bảng dữ liệu: tìm kiếm + lọc + phân trang ---------- */
document.querySelectorAll('.dtable').forEach(function (card) {
    var pageSize = parseInt(card.dataset.pagesize, 10) || 8;
    var rows = Array.prototype.slice.call(card.querySelectorAll('tbody tr'));
    var search = card.querySelector('.search input');
    var filterBtn = card.querySelector('.js-filter');
    var menu = card.querySelector('.filter-menu');
    var countEl = card.querySelector('.count');
    var pagerEl = card.querySelector('.pager');
    var state = { q: '', filter: '', page: 1 };

    // Danh sách thao tác (chỉ chạy khi thẻ có thanh công cụ/phân trang)
    if (!pagerEl) return;

    // Dựng menu bộ lọc từ các trạng thái (pill) có trong bảng
    var values = [];
    rows.forEach(function (tr) {
        var p = tr.querySelector('.pill');
        if (p && values.indexOf(p.textContent.trim()) < 0) values.push(p.textContent.trim());
    });
    if (filterBtn) {
        if (values.length === 0) { filterBtn.parentNode.style.display = 'none'; }
        else {
            menu.innerHTML = '';
            ['Tất cả'].concat(values).forEach(function (v, i) {
                var b = document.createElement('button');
                b.type = 'button'; b.textContent = v; if (i === 0) b.className = 'on';
                b.addEventListener('click', function () {
                    state.filter = i === 0 ? '' : v; state.page = 1;
                    menu.querySelectorAll('button').forEach(function (x) { x.classList.toggle('on', x === b); });
                    filterBtn.querySelector('.filter-label').textContent = i === 0 ? 'Bộ lọc' : v;
                    menu.hidden = true; render();
                });
                menu.appendChild(b);
            });
            filterBtn.addEventListener('click', function (e) { e.stopPropagation(); menu.hidden = !menu.hidden; });
            document.addEventListener('click', function () { menu.hidden = true; });
        }
    }

    if (search) search.addEventListener('input', function () { state.q = search.value.trim().toLowerCase(); state.page = 1; render(); });

    // Áp dụng tìm kiếm + lọc, rồi hiển thị đúng trang hiện tại
    function render() {
        var matched = rows.filter(function (tr) {
            var okQ = !state.q || tr.textContent.toLowerCase().indexOf(state.q) >= 0;
            var okF = !state.filter || Array.prototype.some.call(tr.querySelectorAll('.pill'), function (p) { return p.textContent.trim() === state.filter; });
            return okQ && okF;
        });
        var pages = Math.max(1, Math.ceil(matched.length / pageSize));
        if (state.page > pages) state.page = pages;
        var from = (state.page - 1) * pageSize;
        rows.forEach(function (tr) { tr.style.display = 'none'; });
        matched.slice(from, from + pageSize).forEach(function (tr) { tr.style.display = ''; });

        var shownTo = Math.min(matched.length, from + pageSize);
        countEl.textContent = matched.length === 0 ? 'Không có kết quả' : 'Hiển thị ' + (from + 1) + '–' + shownTo + ' trong ' + matched.length + ' kết quả';

        // Vẽ các nút trang: ‹ 1 2 3 ›
        pagerEl.innerHTML = '';
        function add(label, page, disabled, on) {
            var b = document.createElement('button'); b.type = 'button'; b.innerHTML = label; b.disabled = disabled;
            if (on) b.className = 'on';
            b.addEventListener('click', function () { state.page = page; render(); });
            pagerEl.appendChild(b);
        }
        add('&lsaquo;', state.page - 1, state.page === 1);
        for (var p = 1; p <= pages; p++) add(String(p), p, false, p === state.page);
        add('&rsaquo;', state.page + 1, state.page === pages);
    }
    render();
});

/* ---------- 4. Phím tắt Ctrl+K: nhảy vào ô tìm kiếm đầu tiên ---------- */
document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        var s = document.querySelector('.search input');
        if (s) { e.preventDefault(); s.focus(); }
    }
});

/* ---------- 5. POS: hóa đơn tạm khi bán tại quầy ---------- */
(function () {
    var list = document.getElementById('billList');
    if (!list) return;                      // chỉ chạy ở trang Bán hàng tại quầy
    var items = {};                         // id biến thể -> {name, variant, price, qty, stock, tone}
    var fmt = new Intl.NumberFormat('vi-VN');
    var cat = '';                           // loại đang chọn ('' = tất cả)

    // Vẽ lại toàn bộ hóa đơn tạm và tính tiền
    function render() {
        var ids = Object.keys(items), sub = 0, count = 0;
        list.innerHTML = '';
        if (ids.length === 0) list.innerHTML = '<li class="empty">Chưa có sản phẩm nào.<br>Bấm vào sản phẩm bên trái để thêm.</li>';
        ids.forEach(function (id) {
            var it = items[id];
            sub += it.price * it.qty; count += it.qty;
            var li = document.createElement('li');
            li.className = 'bill-line';
            li.innerHTML =
                '<span class="tile ' + it.tone + '">' + it.name.charAt(0) + '</span>' +
                '<div class="bl-main"><div class="bl-top"><span>' + it.name + '</span><span>' + fmt.format(it.price * it.qty) + 'đ</span></div>' +
                '<small>' + it.variant + '</small>' +
                '<div class="qty"><button type="button" data-act="dec" data-id="' + id + '">&minus;</button><span>' + it.qty +
                '</span><button type="button" data-act="inc" data-id="' + id + '">+</button></div></div>' +
                '<button type="button" class="bl-x" data-act="del" data-id="' + id + '" title="Bỏ khỏi hóa đơn">&times;</button>';
            list.appendChild(li);
        });
        var pct = parseFloat(document.getElementById('billDiscount').value) || 0;
        var disc = Math.round(sub * pct / 100);
        document.getElementById('billCount').textContent = count + ' sản phẩm';
        document.getElementById('billSub').textContent = fmt.format(sub) + 'đ';
        document.getElementById('billDisc').textContent = '-' + fmt.format(disc) + 'đ';
        document.getElementById('billTotal').textContent = fmt.format(sub - disc) + 'đ';
    }

    // Bấm sản phẩm -> thêm 1 (không vượt quá tồn kho)
    document.querySelectorAll('.pitem').forEach(function (b) {
        b.addEventListener('click', function () {
            var id = b.dataset.id, stock = parseInt(b.dataset.stock, 10);
            if (!items[id]) items[id] = { name: b.dataset.name, variant: b.dataset.variant, price: parseFloat(b.dataset.price), qty: 0, stock: stock, tone: b.dataset.tone };
            if (items[id].qty < stock) items[id].qty++; else showToast('Đã hết số lượng tồn của sản phẩm này.');
            render();
        });
    });

    // Nút + / - / xóa trong từng dòng
    list.addEventListener('click', function (e) {
        var b = e.target.closest('[data-act]'); if (!b) return;
        var it = items[b.dataset.id]; if (!it) return;
        if (b.dataset.act === 'inc') { if (it.qty < it.stock) it.qty++; else showToast('Đã hết số lượng tồn của sản phẩm này.'); }
        else if (b.dataset.act === 'dec') { it.qty--; if (it.qty <= 0) delete items[b.dataset.id]; }
        else delete items[b.dataset.id];
        render();
    });

    document.getElementById('billDiscount').addEventListener('change', render);
    document.getElementById('billClear').addEventListener('click', function () { items = {}; render(); });

    // Lọc sản phẩm theo ô tìm kiếm và loại
    function filterProducts() {
        var q = document.getElementById('posSearch').value.trim().toLowerCase();
        document.querySelectorAll('#posGrid .pitem').forEach(function (b) {
            var ok = (!cat || b.dataset.cat === cat) && (!q || b.textContent.toLowerCase().indexOf(q) >= 0);
            b.style.display = ok ? '' : 'none';
        });
    }
    document.getElementById('posSearch').addEventListener('input', filterProducts);
    document.querySelectorAll('#posChips .chip-btn').forEach(function (c) {
        c.addEventListener('click', function () {
            document.querySelectorAll('#posChips .chip-btn').forEach(function (x) { x.classList.toggle('active', x === c); });
            cat = c.dataset.cat; filterProducts();
        });
    });
    render();
})();
