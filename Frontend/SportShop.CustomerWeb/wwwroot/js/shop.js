
var DB_MESSAGE = 'Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu.';
var toastTimer;
// Hiện thông báo nhỏ (toast); không truyền msg thì báo chưa kết nối CSDL.
function showToast(msg) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg || DB_MESSAGE;
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove('show'); }, 2800);
}

var SOON_MESSAGE = 'Chức năng này đang được phát triển.';
// Nút có data-needs-db: chưa kết nối CSDL (body data-db) thì báo lỗi; đã kết nối thì chỉ nút submit được chạy, nút khác báo đang phát triển.
document.addEventListener('click', function (e) {
    var el = e.target.closest('[data-needs-db]');
    if (!el) return;
    var connected = document.body.dataset.db === 'true';
    if (connected && el.type === 'submit') return;
    e.preventDefault();
    e.stopPropagation();
    showToast(connected ? SOON_MESSAGE : DB_MESSAGE);
}, true);

// Thanh tìm kiếm: mở/đóng bằng nút hoặc Ctrl+K; Enter chuyển đến /Shop?q=... (ShopController.Index).
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
    input.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        if (document.body.dataset.db === 'true') { if (input.value.trim()) location.href = '/Shop?q=' + encodeURIComponent(input.value.trim()); }
        else showToast();
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') toggle(false);
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); toggle(true); }
    });
})();

document.querySelectorAll('.heart').forEach(function (b) {
    b.addEventListener('click', function () { b.classList.toggle('liked'); });
});
document.querySelectorAll('#productTabs button').forEach(function (b) {
    b.addEventListener('click', function () {
        document.querySelectorAll('#productTabs button').forEach(function (x) { x.classList.toggle('active', x === b); });
    });
});
