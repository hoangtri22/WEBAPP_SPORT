// Mỗi tab một phiên đăng nhập (Services/TabCookies.cs): mã tab giữ trong sessionStorage, ghi vào cookie khi dùng tab và gắn header X-Tab-Id cho fetch; data-cookie trùng tên đăng ký ở Program.cs.
(function () {
    var script = document.currentScript, COOKIE = script && script.dataset.cookie;
    if (!COOKIE) return;
    var KEY = 'sportshop.tab.' + COOKIE, RELOAD = KEY + '.reload', id;

    // Sinh mã tab 32 ký tự hex.
    function newId() {
        var bytes = new Uint8Array(16);
        (window.crypto || window.msCrypto).getRandomValues(bytes);
        return Array.prototype.map.call(bytes, function (b) { return ('0' + b.toString(16)).slice(-2); }).join('');
    }
    // Đọc mã tab đang lưu trong cookie.
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
    } catch (e) { return; }

    // Cookie đang thuộc tab khác thì nhận lại rồi tải lại một lần để máy chủ trả đúng phiên của tab này.
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

    // Nhận lại cookie khi tab được xem lại hoặc được chọn.
    document.addEventListener('visibilitychange', function () { if (!document.hidden) claim(); });
    window.addEventListener('focus', claim);
    window.addEventListener('pageshow', claim);
    // Trang ở tab khác vừa tải xong có thể giành lại cookie: nhận lại ngay khi người dùng bấm / gõ phím / chạm trong tab này, trước khi mở liên kết hoặc gửi biểu mẫu
    ['mousedown', 'keydown', 'touchstart', 'submit'].forEach(function (type) { document.addEventListener(type, claim, true); });
})();
