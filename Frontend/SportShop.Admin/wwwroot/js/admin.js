
document.querySelectorAll('.js-nav-toggle').forEach(function (btn) {
    btn.addEventListener('click', function () {
        if (window.innerWidth <= 900) document.body.classList.toggle('nav-open');
        else document.body.classList.toggle('collapsed');
    });
});
document.querySelectorAll('.brand-mark').forEach(function (logo) {
    logo.addEventListener('click', function () {
        if (window.innerWidth > 900 && document.body.classList.contains('collapsed')) document.body.classList.remove('collapsed');
    });
});
document.addEventListener('click', function (e) {
    if (document.body.classList.contains('nav-open') && !e.target.closest('.sidebar') && !e.target.closest('.js-nav-toggle'))
        document.body.classList.remove('nav-open');
});

var toastTimer;
// Hiện thông báo nhỏ (toast); isError = true thì hiện kiểu lỗi và lâu hơn.
function showToast(msg, isError) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.classList.toggle('error', !!isError);
    t.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove('show'); }, isError ? 6000 : 3200);
}
// Hiện lại thông báo lưu trong sessionStorage ("flash") sau khi trang tải lại.
try {
    var flash = sessionStorage.getItem('flash');
    if (flash) { sessionStorage.removeItem('flash'); setTimeout(function () { showToast(flash); }, 150); }
} catch (e) { }

function antiToken() {
    var t = document.querySelector('input[name="__RequestVerificationToken"]');
    return t ? t.value : '';
}
// Gửi FormData (kèm token chống giả mạo) đến các action của CrudController bằng fetch, nhận JSON {ok, message}.
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
// Xử lý JSON trả về: thành công thì lưu thông báo vào sessionStorage và tải lại trang, thất bại thì hiện lỗi.
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
// Mở ngăn trượt biểu mẫu: tải HTML từ /Crud/Form (CrudController.Form) rồi khởi tạo bằng initForm().
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

// Nút data-crud (thêm/sửa/xem → openDrawer(), xóa → /Crud/Delete), data-act (→ /Crud/Act) và data-quick (→ /Crud/Save, kèm lý do hủy khi ô lý do đang hiện).
document.addEventListener('click', function (e) {
    if (e.target.closest('.js-drawer-close')) { closeDrawer(); return; }

    var crud = e.target.closest('[data-crud]');
    if (crud) {
        var d = crud.dataset;
        if (d.crud === 'delete') {
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
        if (act.dataset.act === 'feedback_reply') {
            var box = act.closest('.reply-box'), ta = box && box.querySelector('textarea');
            f.append('noi_dung', ta ? ta.value : '');
        }
        postForm('/Crud/Act', f).then(handleResult);
        return;
    }

    var quick = e.target.closest('[data-quick]');
    if (quick) {
        var q = new FormData();
        q.append(quick.dataset.field, document.querySelector(quick.dataset.from).value);
        if (quick.dataset.field2) q.append(quick.dataset.field2, document.querySelector(quick.dataset.from2).value);
        var extra = quick.dataset.field3 && document.querySelector(quick.dataset.from3);
        if (extra && !extra.hidden) q.append(quick.dataset.field3, extra.value.trim());
        postForm('/Crud/Save?entity=' + quick.dataset.quick + '&id=' + quick.dataset.id, q).then(handleResult);
    }
});

// Ô cập nhật trạng thái đơn (#quickStatus, trang chi tiết đơn và khung xem nhanh): chọn "Đã hủy" thì hiện ô nhập lý do hủy (bắt buộc), chọn trạng thái khác thì ẩn đi.
document.addEventListener('change', function (e) {
    if (!e.target.matches || !e.target.matches('#quickStatus')) return;
    var reason = document.getElementById('quickReason'); if (!reason) return;
    reason.hidden = e.target.value !== 'da_huy';
    if (!reason.hidden) reason.focus();
});

// Ô chọn thanh toán của đơn (#quickPay): chỉ khi chọn "Đã thanh toán" mới chọn được trạng thái "Đã giao" / "Đã hủy" trong #quickStatus.
document.addEventListener('change', function (e) {
    if (!e.target.matches || !e.target.matches('#quickPay')) return;
    var status = document.getElementById('quickStatus'); if (!status) return;
    var paid = e.target.value === 'da_thanh_toan';
    Array.prototype.forEach.call(status.options, function (o) { if (o.value === 'da_giao' || o.value === 'da_huy') o.disabled = !paid; });
    if (status.selectedOptions[0] && status.selectedOptions[0].disabled) {
        var orig = Array.prototype.find.call(status.options, function (o) { return o.defaultSelected; });
        if (orig) { status.value = orig.value; status.dispatchEvent(new Event('change', { bubbles: true })); }
    }
});

// Khởi tạo biểu mẫu trong ngăn trượt: ẩn/hiện ô theo data-showwhen, dòng hàng của đơn / phiếu nhập và tính tổng, dòng biến thể, câu hỏi khảo sát, xem trước ảnh; submit gửi đến /Crud/Save (CrudController.Save).
function initForm(form) {
    if (!form) return;
    var err = form.querySelector('.form-error');

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

    form.querySelectorAll('.lines').forEach(function (box) {
        var kind = box.dataset.kind, body = box.querySelector('.lines-body'),
            tpl = box.querySelector('.line-tpl'), totalEl = box.querySelector('.js-lines-total');
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

    form.querySelectorAll('.vlines').forEach(function (box) {
        var body = box.querySelector('.vlines-body'), tpl = box.querySelector('.vline-tpl'), seq = 0;
        function add() {
            body.appendChild(tpl.content.cloneNode(true));
            var line = body.lastElementChild, no = ++seq;
            var noInput = line.querySelector('.js-vline-no'), file = line.querySelector('.js-vline-file'), preview = line.querySelector('.vline-preview');
            noInput.value = no; file.name = 'bt_hinh_anh_file_' + no;
            file.addEventListener('change', function () {
                var f = file.files[0];
                if (!f) { preview.hidden = true; return; }
                if (!/^image\//.test(f.type) || f.size > 5 * 1024 * 1024) {
                    showToast('Chỉ nhận ảnh JPG, PNG, WEBP, GIF tối đa 5 MB.', true);
                    file.value = ''; preview.hidden = true; return;
                }
                preview.src = URL.createObjectURL(f); preview.hidden = false;
            });
            line.querySelector('.js-remove-vline').addEventListener('click', function () {
                if (body.children.length > 1) line.remove();
                else {
                    line.querySelectorAll('input:not(.js-vline-no), select').forEach(function (i) { i.value = ''; });
                    preview.hidden = true;
                }
            });
        }
        box.querySelector('.js-add-vline').addEventListener('click', add);
        add();
    });

    var stSel = form.elements['trang_thai_don_hang'], paySel = form.elements['trang_thai_thanh_toan'];
    if (form.dataset.entity === 'order' && stSel && paySel) {
        function syncStatus() {
            var unpaid = paySel.value !== 'da_thanh_toan';
            Array.prototype.forEach.call(stSel.options, function (o) { o.disabled = unpaid && (o.value === 'da_giao' || o.value === 'da_huy'); });
            if (stSel.selectedOptions[0] && stSel.selectedOptions[0].disabled) stSel.value = 'cho_xac_nhan';
            applyShowWhen();
        }
        paySel.addEventListener('change', syncStatus);
        syncStatus();
    }

    var cust = form.elements['ma_khach_hang'];
    if (cust && form.dataset.entity === 'order') cust.addEventListener('change', function () {
        var p = ((cust.selectedOptions[0] && cust.selectedOptions[0].dataset.info) || '').split('|');
        ['ten_nguoi_nhan', 'sdt_nguoi_nhan', 'dia_chi_giao_hang'].forEach(function (n, i) {
            if (form.elements[n]) form.elements[n].value = p[i] || '';
        });
    });

    form.querySelectorAll('.questions').forEach(function (qs) {
        var body = qs.querySelector('.q-body'), tpl = qs.querySelector('.q-tpl'), addBtn = qs.querySelector('.js-add-q');
        function bind(row) {
            row.querySelector('.js-remove-q').addEventListener('click', function () { row.remove(); });
        }
        body.querySelectorAll('.qrow').forEach(bind);
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

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        if (form.dataset.mode === 'view') return;
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

// Bảng dữ liệu (.dtable): tìm kiếm (SearchKit.matches), tìm kiếm nâng cao theo từng cột (SearchKit.advancedPanel) và phân trang ngay trên trình duyệt, không gọi máy chủ.
document.querySelectorAll('.dtable').forEach(function (card) {
    var pageSize = parseInt(card.dataset.pagesize, 10) || 8;
    var rows = Array.prototype.slice.call(card.querySelectorAll('tbody tr'));
    var search = card.querySelector('.search input');
    var countEl = card.querySelector('.count');
    var pagerEl = card.querySelector('.pager');
    var state = { q: '', page: 1 };

    if (!pagerEl) return;
    var adv = SearchKit.advancedPanel(card, rows, function () { state.page = 1; render(); });

    if (search) search.addEventListener('input', function () { state.q = search.value; state.page = 1; render(); });

    function render() {
        var matched = rows.filter(function (tr) {
            var okQ = SearchKit.matches(tr.textContent, state.q);
            var okA = !adv || adv.matches(tr);
            return okQ && okA;
        });
        var pages = Math.max(1, Math.ceil(matched.length / pageSize));
        if (state.page > pages) state.page = pages;
        var from = (state.page - 1) * pageSize;
        rows.forEach(function (tr) { tr.style.display = 'none'; });
        matched.slice(from, from + pageSize).forEach(function (tr) { tr.style.display = ''; });

        var shownTo = Math.min(matched.length, from + pageSize);
        countEl.textContent = matched.length === 0 ? 'Không có kết quả' : 'Hiển thị ' + (from + 1) + '–' + shownTo + ' trong ' + matched.length + ' kết quả';

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

document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        var s = document.querySelector('.search input');
        if (s) { e.preventDefault(); s.focus(); }
    }
});

// Bấm vào một dòng có trang chi tiết (data-peek) thì mở trang chi tiết; bỏ qua khi bấm nút, link, ô nhập hoặc khi đang bôi đen chữ.
document.addEventListener('click', function (e) {
    var tr = e.target.closest('tr[data-peek]:not([data-peek=""])');
    if (!tr || e.target.closest('a, button, input, select, textarea, label')) return;
    if (String(window.getSelection())) return;
    if (e.ctrlKey || e.metaKey) window.open(tr.getAttribute('data-peek'), '_blank');
    else location.href = tr.getAttribute('data-peek');
});

// Xem nhanh dòng có trang chi tiết (data-peek): rê chuột vào dòng thì fetch trang chi tiết, lấy phần nội dung qua build() rồi hiện ở khung bên phải (màn hình dưới 1600px tự thu gọn menu trái trong lúc xem).
(function () {
    var rows = document.querySelectorAll('tr[data-peek]:not([data-peek=""])');
    if (rows.length === 0) return;
    var wide = window.matchMedia('(min-width: 1200px)');
    var panel = document.createElement('aside');
    panel.className = 'peek'; panel.hidden = true; panel.setAttribute('aria-label', 'Xem chi tiết');
    document.body.appendChild(panel);
    var cache = {}, current = '', showTimer = 0, hideTimer = 0, autoCollapsed = false;

    // Lấy phần nội dung của trang chi tiết (giữ nút "Chỉnh sửa" và ô cập nhật trạng thái; bỏ nút quay lại, in hóa đơn và dòng chữ xanh phía trên tiêu đề)
    function build(url, html) {
        var main = new DOMParser().parseFromString(html, 'text/html').querySelector('main.content');
        if (!main) return null;
        var edit = main.querySelector('.head-actions [data-crud="edit"]');
        main.querySelectorAll('.back, .head-actions, .eyebrow, script').forEach(function (n) { n.remove(); });
        var bar = document.createElement('div');
        bar.className = 'peek-bar';
        bar.innerHTML = '<div class="peek-links"><a class="btn-outline" href="' + url + '">Mở trang chi tiết</a></div><button type="button" class="peek-close" aria-label="Đóng">&times;</button>';
        if (edit) bar.firstChild.insertBefore(edit, bar.firstChild.firstChild);
        main.insertBefore(bar, main.firstChild);
        return main.innerHTML;
    }

    function close() {
        clearTimeout(showTimer); clearTimeout(hideTimer);
        panel.hidden = true; current = '';
        document.body.classList.remove('peek-open');
        if (autoCollapsed) { document.body.classList.remove('collapsed'); autoCollapsed = false; }
        rows.forEach(function (r) { r.classList.remove('peek-on'); });
    }
    function scheduleHide() { clearTimeout(hideTimer); hideTimer = setTimeout(close, 350); }

    function show(url) {
        panel.innerHTML = cache[url];
        panel.scrollTop = 0; panel.hidden = false;
        document.body.classList.add('peek-open');
        if (window.innerWidth < 1600 && !document.body.classList.contains('collapsed')) { document.body.classList.add('collapsed'); autoCollapsed = true; }
    }
    function open(tr) {
        var url = tr.getAttribute('data-peek');
        rows.forEach(function (r) { r.classList.toggle('peek-on', r === tr); });
        if (url === current && !panel.hidden) return;
        current = url;
        if (cache[url]) { show(url); return; }
        fetch(url, { credentials: 'same-origin' })
            .then(function (res) { return res.ok ? res.text() : Promise.reject(); })
            .then(function (html) {
                cache[url] = build(url, html);
                if (current !== url) return;
                if (cache[url]) show(url); else close();
            })
            .catch(function () { if (current === url) close(); });
    }

    rows.forEach(function (tr) {
        tr.addEventListener('mouseenter', function () {
            if (!wide.matches) return;
            clearTimeout(hideTimer); clearTimeout(showTimer);
            showTimer = setTimeout(function () { open(tr); }, 120);
        });
        tr.addEventListener('mouseleave', function () { clearTimeout(showTimer); scheduleHide(); });
    });
    panel.addEventListener('mouseenter', function () { clearTimeout(hideTimer); });
    panel.addEventListener('mouseleave', scheduleHide);
    panel.addEventListener('click', function (e) { if (e.target.closest('.peek-close')) close(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !panel.hidden) close(); });
    wide.addEventListener('change', function () { if (!wide.matches) close(); });
})();
