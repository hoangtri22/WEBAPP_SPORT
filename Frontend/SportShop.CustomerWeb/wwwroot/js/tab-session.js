// Mỗi tab trình duyệt một phiên đăng nhập riêng (xem Models/TabCookies.cs, TabCookieMiddleware).
// Trình duyệt dùng chung cookie cho mọi tab của cùng một trang web, nên mỗi tab tự giữ một mã riêng trong sessionStorage
// (mất khi đóng tab, không chia sẻ giữa các tab) và "nhận" cookie mã tab này mỗi khi tab được dùng:
//   - mở tab mới: chưa có mã -> tạo mã mới -> máy chủ chưa có phiên của mã này nên hiện trang đăng nhập;
//   - quay lại tab cũ: nhận lại mã của tab đó -> máy chủ trả đúng tài khoản của tab đó;
//   - gọi fetch: tự gắn mã tab vào header X-Tab-Id nên tab chạy nền không bị lẫn sang tab khác.
// Nhúng ở đầu <head> với data-cookie là tên cookie mã tab, trùng với tên đăng ký ở Program.cs.
(function () {
    var script = document.currentScript, COOKIE = script && script.dataset.cookie;
    if (!COOKIE) return;
    var KEY = 'sportshop.tab.' + COOKIE, RELOAD = KEY + '.reload', id;

    function newId() {
        var bytes = new Uint8Array(16);
        (window.crypto || window.msCrypto).getRandomValues(bytes);
        return Array.prototype.map.call(bytes, function (b) { return ('0' + b.toString(16)).slice(-2); }).join('');
    }
    function current() {
        var m = document.cookie.match(new RegExp('(?:^|;\\s*)' + COOKIE + '=([^;]*)'));
        return m ? m[1] : '';
    }
    // Ghi cookie mã tab của tab này (nếu đang là của tab khác); trả về true khi cookie đã đúng
    function claim() {
        if (current() !== id) document.cookie = COOKIE + '=' + id + '; path=/; SameSite=Lax';
        return current() === id;
    }

    try {
        id = sessionStorage.getItem(KEY);
        if (!id) { id = newId(); sessionStorage.setItem(KEY, id); }
    } catch (e) { return; }   // trình duyệt chặn sessionStorage: giữ cách dùng chung cookie như cũ

    // Trang vừa tải mà cookie đang thuộc tab khác (tab mới, hoặc vừa dùng tab khác): nhận lại cookie rồi tải lại một lần
    // để máy chủ trả đúng phiên của tab này (không tải lại liên tục nếu trình duyệt chặn cookie).
    var reloaded = sessionStorage.getItem(RELOAD) === '1';
    sessionStorage.removeItem(RELOAD);
    if (current() !== id) {
        if (claim() && !reloaded) { sessionStorage.setItem(RELOAD, '1'); location.reload(); return; }
    }

    var nativeFetch = window.fetch;
    if (nativeFetch) {
        window.fetch = function (input, init) {
            try {
                var url = new URL(typeof input === 'string' ? input : input.url, location.href);
                if (url.origin === location.origin) {
                    init = init || {};
                    var headers = new Headers(init.headers || (typeof input === 'string' ? undefined : input.headers));
                    headers.set('X-Tab-Id', id);
                    init.headers = headers;
                }
            } catch (e) { }
            return nativeFetch.call(this, input, init);
        };
    }

    document.addEventListener('visibilitychange', function () { if (!document.hidden) claim(); });
    window.addEventListener('focus', claim);
    window.addEventListener('pageshow', claim);
    // Trang ở tab khác vừa tải xong có thể giành lại cookie: nhận lại ngay khi người dùng bấm / gõ phím / chạm trong tab này, trước khi mở liên kết hoặc gửi biểu mẫu
    ['mousedown', 'keydown', 'touchstart', 'submit'].forEach(function (type) { document.addEventListener(type, claim, true); });
})();
