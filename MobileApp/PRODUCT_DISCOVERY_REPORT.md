# Sản phẩm và nội dung trên MobileApp

Phạm vi theo xác nhận của người dùng: app MAUI Android. Rà soát ngày 08/11/2026.

| Hạng mục | Trước khi bổ sung | Sau khi bổ sung |
|---|---|---|
| Danh sách sản phẩm | API thật, tìm kiếm, làm mới, tải thêm và thử lại | Giữ các chức năng này; thẻ sản phẩm mở chi tiết |
| Danh mục và sắp xếp | Client cố định `new` và chỉ còn hàng | Lọc danh mục từ API, mới nhất/giá tăng/giá giảm/tên, bật/tắt chỉ còn hàng; giữ điều kiện đã áp dụng khi phân trang |
| Trang chủ | Danh mục, sản phẩm và ưu đãi mẫu; nhiều nút chưa nối | Danh mục/sản phẩm từ API; nối sản phẩm, ưu đãi, thông tin, giỏ hàng và tài khoản |
| Chi tiết sản phẩm | Chưa có | Mô tả, ảnh, giá, tồn kho và lựa chọn từng biến thể size/màu, kể cả biến thể hết hàng |
| Số lượng và mua hàng | Chưa có từ màn hình sản phẩm | Giới hạn 1 đến tồn kho, tối đa 999; thêm giỏ/mua ngay dùng `ICommerceApi` và checkout hiện có |
| Khuyến mãi | Chưa có màn hình | Voucher đang hiệu lực, mức giảm/tối đa, hạng, thời hạn, lượt dùng; sao chép mã để nhập khi thanh toán |
| Trang thông tin | Chưa có | Danh sách và nội dung hỗ trợ/giới thiệu từ API; mở hướng dẫn size ngay tại sản phẩm |
| Xem đánh giá | Chưa có API công khai hoặc màn hình | Đánh giá theo sản phẩm, phân trang, trung bình sao, trả lời của cửa hàng; ẩn danh người viết |
| Gửi và xem đánh giá của mình | Backend đã có, app chưa nối | Đăng nhập và đã nhận sản phẩm mới được gửi; 1–5 sao, 1–1000 ký tự; lịch sử riêng gồm trạng thái và trả lời |

## Giữ tương thích

- Không đổi schema hoặc dữ liệu cửa hàng. API công khai chỉ đọc `PHAN_HOI` có `an_hien=1`, loại `danh_gia`, sao hợp lệ và sản phẩm còn hiển thị, ngoài danh mục lưu trữ.
- API mới: `GET /api/v1/products/{productId}/reviews?page=1&pageSize=20` trả items, tổng số và sao trung bình trong cùng snapshot. Không trả hồ sơ hoặc ID tài khoản/đơn hàng.
- `GET /api/v1/me/feedback` bổ sung bộ lọc `productId` tùy chọn; lời gọi cũ giữ nguyên kết quả.
- Thêm giỏ, checkout, đơn hàng, đăng nhập, refresh token và guest tiếp tục qua service hiện có. Xung đột tồn kho tải lại chi tiết và không tự gửi lại thao tác thêm giỏ.
- Mỗi lần vào phiên có trang catalog mới để không giữ `Parent` của cây điều hướng phiên cũ; trạng thái catalog dùng chung chỉ chứa dữ liệu sản phẩm công khai.
- Khuyến mãi trong luồng này là voucher do API hiện có cung cấp. Điều kiện sử dụng và giá thanh toán được backend xác nhận, không tự tính mức giảm mới trong app.

## Kiểm tra

- Mobile client: **96/96 PASS, 0 FAIL, 0 SKIP**. Gồm test xác thực/hồ sơ/giỏ/đơn hiện có và test mới cho lọc/phân trang, biến thể/tồn kho/số lượng, trang chủ, nội dung và đánh giá. [TRX](../.test-artifacts/product-discovery/product-discovery-mobile.trx).
- Toàn bộ API: **166/166 PASS, 0 FAIL, 0 SKIP** trên instance MySQL riêng cổng 13384, có test tương thích web khách hàng và Admin. Review mới được kiểm tra visibility, loại phản hồi, tổng/sao trung bình, phân trang, quyền truy cập và sản phẩm ẩn/lưu trữ. [TRX](../.test-artifacts/mobile-api-20261108-152516-0b8ce2c2/mobile-api.trx).
- Build solution thành công, **0 lỗi**; có một cảnh báo nullable có sẵn trong `MainActivity.cs`. Build Android ARM64 riêng cũng thành công; có thêm cảnh báo công cụ đọc chuỗi phiên bản JDK local.
- Lệnh kiểm tra: `dotnet test MobileApp/SportShop.Mobile.Tests --no-restore`; `powershell -NoProfile -File Backend/scripts/Test-MobileApi.ps1 -Port 13384`; Android build dùng JDK 21 và Android SDK local theo `run-android.ps1`.

## Kiểm tra trên thiết bị

Đã cài và chạy bản kiểm thử riêng `vn.sportshop.mobile.discoverytest` trên **Joy 4 / Android 11**, giữ app hiện có. Dữ liệu đi qua API riêng cổng 5084 và MySQL test cổng 13384, không dùng CSDL cửa hàng.

Sau lượt kiểm tra đã dừng hai máy chủ thử nghiệm và gỡ ánh xạ ADB tạm. Bản `SportShop Test` trên điện thoại là bản kiểm thử riêng; APK chuẩn để chạy cùng backend của dự án được dẫn ở cuối báo cáo.

- Đăng nhập demo và mở trang chủ có danh mục/sản phẩm thật; tìm `Nike` trả 4 sản phẩm.
- Mở Quần short bóng đá Nike, đổi size M → L; tồn kho trên giao diện đổi từ 5 → 4. Tăng số lượng lên 2 và thêm giỏ thành công. [Ảnh chi tiết](../.test-artifacts/product-discovery/detail-added.png).
- Xem đánh giá công khai kèm trả lời của cửa hàng. Tài khoản đã nhận hàng gửi được đánh giá 5 sao `UI_08112026`; nội dung xuất hiện trong lịch sử với trạng thái chờ xử lý. [Ảnh lịch sử](../.test-artifacts/product-discovery/review-history.png).
- Mở hướng dẫn chọn size từ chi tiết, hiển thị đầy đủ các đoạn nội dung từ API. [Ảnh hướng dẫn](../.test-artifacts/product-discovery/size-guide.png).
- Mở khuyến mãi, hiển thị mã, mức giảm, điều kiện hạng/lượt dùng và hạn voucher. [Ảnh khuyến mãi](../.test-artifacts/product-discovery/promotions.png).
- Sau khi kiểm tra ảnh, chỉnh màu tiêu đề/nút quay lại từ xám nhạt sang màu chữ chính để đọc rõ trên nền trắng.

Lượt native là smoke cho các màn hình nêu trên; chưa nghiệm thu toàn bộ luồng trên mọi thiết bị, bản Release hoặc đặt/thanh toán một đơn thật. Các trường hợp lọc/sắp xếp, phân trang, hết hàng, lỗi mạng và chặn bấm trùng có kiểm thử client/API. Không dùng kết quả test tự động để khẳng định mọi tương tác native đều đã được thao tác.

## Chạy bản cập nhật

Khởi động lại API từ source mới để có endpoint đánh giá công khai, sau đó chạy `MobileApp/run-android.ps1` theo README. Luồng sản phẩm này không yêu cầu migration mới. APK Debug ARM64 được lưu tại [SportShop-Product-Discovery.apk](../.test-artifacts/product-discovery/SportShop-Product-Discovery.apk).
