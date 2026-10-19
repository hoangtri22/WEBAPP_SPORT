/* =====================================================================
   SPORTSHOP - web khách hàng: JavaScript giao diện
   Các khối:
     1. Thông báo nhỏ (toast)
     2. Chặn các nút cần cơ sở dữ liệu khi chưa có CSDL
     3. Thanh tìm kiếm của cửa hàng
     4. Nút tim, tab sản phẩm
   ===================================================================== */

/* ---------- 1. Thông báo nhỏ ---------- */
var DB_MESSAGE = 'Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu.';
var toastTimer;
function showToast(msg) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg || DB_MESSAGE;
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove('show'); }, 2800);
}

/* ---------- 2. Nút cần cơ sở dữ liệu ----------
   Nút/liên kết có thuộc tính data-needs-db:
     - body[data-db="false"] (chưa kết nối MySQL): chặn thao tác và hiện thông báo
     - body[data-db="true"]  (đã kết nối MySQL): để nút chạy bình thường, không hiện thông báo */
document.addEventListener('click', function (e) {
    var el = e.target.closest('[data-needs-db]');
    if (!el) return;
    if (document.body.dataset.db === 'true') return;
    e.preventDefault();
    e.stopPropagation();
    showToast();
}, true);

/* ---------- 3. Thanh tìm kiếm ---------- */
(function () {
    var bar = document.getElementById('shopSearch');
    if (!bar) return;
    var input = document.getElementById('shopSearchInput');
    function toggle(open) {
        bar.hidden = !(open === undefined ? bar.hidden : open);
        if (!bar.hidden) input.focus();
    }
    ['searchToggle', 'searchToggleMobile'].forEach(function (id) {
        var b = document.getElementById(id);
        if (b) b.addEventListener('click', function () { toggle(); });
    });
    // Enter trong ô tìm kiếm: tìm sản phẩm cần cơ sở dữ liệu
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && document.body.dataset.db !== 'true') showToast();
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') toggle(false);
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); toggle(true); }
    });
})();

/* ---------- 4. Nút tim và tab sản phẩm (chỉ đổi giao diện) ---------- */
document.querySelectorAll('.heart').forEach(function (b) {
    b.addEventListener('click', function () { b.classList.toggle('liked'); });
});
document.querySelectorAll('#productTabs button').forEach(function (b) {
    b.addEventListener('click', function () {
        document.querySelectorAll('#productTabs button').forEach(function (x) { x.classList.toggle('active', x === b); });
    });
});
