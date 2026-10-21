/* =====================================================================
   SportShop Admin - JavaScript giao diện (chưa gọi server/database)
   Các khối:
     1. Thu gọn / mở sidebar
     2. Thông báo nhỏ (toast) cho nút chưa có chức năng
     3. Bảng dữ liệu: tìm kiếm + bộ lọc + phân trang (.dtable)
     4. Phím tắt Ctrl+K
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

