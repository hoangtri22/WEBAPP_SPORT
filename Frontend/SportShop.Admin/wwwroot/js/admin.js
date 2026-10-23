/* =====================================================================
   SportShop Admin - JavaScript giao diện
   Các khối:
     1. Thu gọn / mở sidebar
     2. Thông báo nhỏ (toast)
     2b. Thêm / sửa / xem / xóa qua ngăn trượt bên phải (gọi CrudController)
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

/* ---------- 2. Thông báo nhỏ (toast) ---------- */
var toastTimer;
function showToast(msg, isError) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.classList.toggle('error', !!isError);
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove('show'); }, isError ? 6000 : 3200);
}
// Sau khi thao tác xong trang được tải lại, thông báo kết quả được giữ qua sessionStorage
try {
    var flash = sessionStorage.getItem('flash');
    if (flash) { sessionStorage.removeItem('flash'); setTimeout(function () { showToast(flash); }, 150); }
} catch (e) { }

/* ---------- 2b. Thêm / sửa / xem / xóa (ngăn trượt + gọi server) ---------- */
function antiToken() {
    var t = document.querySelector('input[name="__RequestVerificationToken"]');
    return t ? t.value : '';
}
// Gửi dữ liệu dạng form lên server, nhận JSON { ok, message }
function postForm(url, data) {
    if (!data.has('__RequestVerificationToken')) data.append('__RequestVerificationToken', antiToken());
    return fetch(url, { method: 'POST', body: data, credentials: 'same-origin' })
        .then(function (r) {
            if (r.redirected || r.status === 401) { location.reload(); throw new Error('login'); }
            return r.json();
        })
        .catch(function (e) {
            if (e.message === 'login') throw e;
            return { ok: false, message: 'Không kết nối được máy chủ, vui lòng thử lại.' };
        });
}
// Thành công thì tải lại trang để thấy dữ liệu mới; thất bại thì báo lỗi
function handleResult(res, onFail) {
    if (res.ok) {
        try { sessionStorage.setItem('flash', res.message); } catch (e) { }
        location.reload();
    } else if (onFail) onFail(res.message);
    else showToast(res.message, true);
}
var money = function (n) { return (Math.round(n) || 0).toLocaleString('vi-VN') + 'đ'; };

var drawer = document.getElementById('drawer'), backdrop = document.getElementById('drawerBackdrop');
function closeDrawer() {
    if (!drawer) return;
    drawer.classList.remove('open'); backdrop.classList.remove('open');
    setTimeout(function () {
        if (drawer.classList.contains('open')) return;
        drawer.hidden = true; backdrop.hidden = true; drawer.innerHTML = '';
    }, 200);
}
function openDrawer(entity, id, parent, mode) {
    if (!drawer) return;
    var q = 'entity=' + encodeURIComponent(entity) + '&id=' + (id || 0) + '&parent=' + (parent || 0) + '&mode=' + (mode || 'create');
    fetch('/Crud/Form?' + q, { credentials: 'same-origin' })
        .then(function (r) { return r.text().then(function (t) { return { html: t, type: r.headers.get('Content-Type') || '' }; }); })
        .then(function (r) {
            if (r.type.indexOf('text/plain') === 0) { showToast(r.html, true); return; }
            drawer.innerHTML = r.html;
            drawer.hidden = false; backdrop.hidden = false;
            setTimeout(function () { drawer.classList.add('open'); backdrop.classList.add('open'); }, 20);
            initForm(drawer.querySelector('form'));
        })
        .catch(function () { showToast('Không mở được biểu mẫu, vui lòng thử lại.', true); });
}
if (backdrop) backdrop.addEventListener('click', closeDrawer);
document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeDrawer(); });

// Nút trên trang: thêm / xem / sửa / xóa và các thao tác nhanh
document.addEventListener('click', function (e) {
    if (e.target.closest('.js-drawer-close')) { closeDrawer(); return; }

    var crud = e.target.closest('[data-crud]');
    if (crud) {
        var d = crud.dataset;
        if (d.crud === 'delete') {
            // Có cảnh báo hệ quả (ví dụ xóa loại sản phẩm) thì hiện thêm trong hộp xác nhận
            var warn = d.warn ? '\n\n' + d.warn : '';
            if (!confirm('Bạn chắc chắn muốn xóa "' + (d.name || 'mục này') + '"?' + warn)) return;
            var fd = new FormData(); fd.append('entity', d.entity); fd.append('id', d.id);
            postForm('/Crud/Delete', fd).then(handleResult);
        } else {
            openDrawer(d.entity, parseInt(d.id || '0', 10), parseInt(d.parent || '0', 10), d.crud === 'view' ? 'view' : (d.id ? 'edit' : 'create'));
        }
        return;
    }

    var act = e.target.closest('[data-act]');
    if (act) {
        if (act.dataset.confirm && !confirm(act.dataset.confirm)) return;
        var f = new FormData(); f.append('action', act.dataset.act); f.append('id', act.dataset.id);
        // Trả lời phản hồi: lấy nội dung trong khung trả lời gần nhất
        if (act.dataset.act === 'feedback_reply') {
            var box = act.closest('.reply-box'), ta = box && box.querySelector('textarea');
            f.append('noi_dung', ta ? ta.value : '');
        }
        postForm('/Crud/Act', f).then(handleResult);
        return;
    }

    // Đổi nhanh một trường (ví dụ trạng thái đơn hàng ở trang chi tiết)
    var quick = e.target.closest('[data-quick]');
    if (quick) {
        var q = new FormData();
        q.append(quick.dataset.field, document.querySelector(quick.dataset.from).value);
        postForm('/Crud/Save?entity=' + quick.dataset.quick + '&id=' + quick.dataset.id, q).then(handleResult);
    }
});

// Dựng hành vi cho biểu mẫu vừa tải: hiện/ẩn ô theo điều kiện, dòng hàng, câu hỏi khảo sát, gửi form
function initForm(form) {
    if (!form) return;
    var err = form.querySelector('.form-error');

    // Ô chỉ hiện khi ô khác có giá trị nhất định (data-showwhen="tên=giá_trị1|giá_trị2")
    function applyShowWhen() {
        form.querySelectorAll('[data-showwhen]:not([data-showwhen=""])').forEach(function (w) {
            var p = w.dataset.showwhen.split('='), src = form.elements[p[0]];
            var show = !!src && p[1].split('|').indexOf(src.value) >= 0;
            w.hidden = !show;
            w.querySelectorAll('input,select,textarea').forEach(function (i) { i.disabled = !show; });
        });
    }
    form.addEventListener('change', applyShowWhen);
    applyShowWhen();

    // Khối nhiều dòng hàng: đơn hàng (giá bán, tồn) và phiếu nhập (đơn giá nhập)
    form.querySelectorAll('.lines').forEach(function (box) {
        var kind = box.dataset.kind, body = box.querySelector('.lines-body'),
            tpl = box.querySelector('.line-tpl'), totalEl = box.querySelector('.js-lines-total');
        // Thông tin biến thể đang chọn: "giá bán|tồn kho|giá vốn"
        function info(line) {
            var o = line.querySelector('.js-variant').selectedOptions[0], p = ((o && o.dataset.info) || '').split('|');
            return { price: parseFloat(p[0]) || 0, stock: parseInt(p[1], 10) || 0, cost: parseFloat(p[2]) || 0, isNew: p[3] === 'new' };
        }
        function recalc() {
            var total = 0;
            body.querySelectorAll('.line').forEach(function (line) {
                var qty = parseInt(line.querySelector('.js-qty').value, 10) || 0, amount;
                if (kind === 'order') {
                    amount = info(line).price * qty;
                    line.querySelector('.line-amount').textContent = money(amount);
                } else amount = qty * (parseFloat(line.querySelector('.js-cost').value) || 0);
                total += amount;
            });
            totalEl.textContent = money(total);
        }
        function add() {
            body.appendChild(tpl.content.cloneNode(true));
            var line = body.lastElementChild;
            line.querySelector('.js-variant').addEventListener('change', function () {
                if (kind === 'import') {
                    line.querySelector('.js-cost').value = info(line).cost || '';
                    // Sản phẩm chưa có biến thể: hiện ô nhập giá bán để hệ thống tự tạo biến thể mặc định khi lưu phiếu
                    var sell = line.querySelector('.js-sell');
                    sell.hidden = !info(line).isNew;
                    if (sell.hidden) sell.value = '';
                }
                if (kind === 'order') line.querySelector('.js-qty').max = info(line).stock || '';
                recalc();
            });
            line.addEventListener('input', recalc);
            line.querySelector('.js-remove-line').addEventListener('click', function () {
                if (body.children.length > 1) line.remove();
                else { line.querySelector('select').value = ''; line.querySelectorAll('input').forEach(function (i) { i.value = ''; }); }
                recalc();
            });
        }
        box.querySelector('.js-add-line').addEventListener('click', add);
        add();
    });

    // Đơn hàng: chưa thanh toán thì chọn được chờ xác nhận / đang chuẩn bị / đang giao; "Đã giao" và "Đã hủy" chỉ mở sau khi chọn "Đã thanh toán"
    var stSel = form.elements['trang_thai_don_hang'], paySel = form.elements['trang_thai_thanh_toan'];
    if (form.dataset.entity === 'order' && stSel && paySel) {
        function syncStatus() {
            var unpaid = paySel.value !== 'da_thanh_toan';
            Array.prototype.forEach.call(stSel.options, function (o) { o.disabled = unpaid && (o.value === 'da_giao' || o.value === 'da_huy'); });
            // Đang chọn "Đã giao" / "Đã hủy" mà đổi về chưa thanh toán thì đưa về "Chờ xác nhận"
            if (stSel.selectedOptions[0] && stSel.selectedOptions[0].disabled) stSel.value = 'cho_xac_nhan';
            applyShowWhen();
        }
        paySel.addEventListener('change', syncStatus);
        syncStatus();
    }

    // Chọn khách hàng thì tự điền thông tin người nhận (tên | số điện thoại | địa chỉ mặc định)
    var cust = form.elements['ma_khach_hang'];
    if (cust && form.dataset.entity === 'order') cust.addEventListener('change', function () {
        var p = ((cust.selectedOptions[0] && cust.selectedOptions[0].dataset.info) || '').split('|');
        ['ten_nguoi_nhan', 'sdt_nguoi_nhan', 'dia_chi_giao_hang'].forEach(function (n, i) {
            if (form.elements[n]) form.elements[n].value = p[i] || '';
        });
    });

    // Câu hỏi khảo sát: thêm / bỏ câu hỏi
    form.querySelectorAll('.questions').forEach(function (qs) {
        var body = qs.querySelector('.q-body'), tpl = qs.querySelector('.q-tpl'), addBtn = qs.querySelector('.js-add-q');
        function bind(row) {
            row.querySelector('.js-remove-q').addEventListener('click', function () { row.remove(); });
        }
        body.querySelectorAll('.qrow').forEach(bind);
        // Khảo sát đã có người trả lời: khóa phần câu hỏi
        if (qs.dataset.locked === '1') {
            qs.querySelectorAll('input,select,textarea,button').forEach(function (i) { i.disabled = true; });
            return;
        }
        if (addBtn) addBtn.addEventListener('click', function () {
            body.appendChild(tpl.content.cloneNode(true));
            bind(body.lastElementChild);
        });
        if (!body.children.length && addBtn) addBtn.click();
    });

    // Ảnh sản phẩm: chọn file từ thiết bị thì hiện ảnh xem trước (kiểm tra loại và dung lượng trước khi gửi)
    var imageFile = form.querySelector('.js-image-file');
    if (imageFile) imageFile.addEventListener('change', function () {
        var preview = form.querySelector('.img-preview'), file = imageFile.files[0];
        if (!file) { preview.hidden = true; return; }
        if (!/^image\//.test(file.type) || file.size > 5 * 1024 * 1024) {
            showToast('Chỉ nhận ảnh JPG, PNG, WEBP, GIF tối đa 5 MB.', true);
            imageFile.value = ''; preview.hidden = true; return;
        }
        preview.src = URL.createObjectURL(file); preview.hidden = false;
    });

    // Gửi biểu mẫu
    form.addEventListener('submit', function (e) {
        e.preventDefault();
        if (form.dataset.mode === 'view') return;
        // Ô "Bắt buộc" của câu hỏi: giá trị = số thứ tự câu hỏi
        form.querySelectorAll('.qrow').forEach(function (row, i) {
            var c = row.querySelector('input[type=checkbox]');
            if (c) c.value = i;
        });
        var btn = form.querySelector('button[type=submit]');
        btn.disabled = true; err.hidden = true;
        postForm('/Crud/Save?entity=' + form.dataset.entity + '&id=' + form.dataset.id, new FormData(form)).then(function (res) {
            btn.disabled = false;
            handleResult(res, function (m) { err.textContent = m; err.hidden = false; err.scrollIntoView({ block: 'nearest' }); });
        });
    });
}

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
