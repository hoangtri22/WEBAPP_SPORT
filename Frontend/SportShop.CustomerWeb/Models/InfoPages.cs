namespace SportShop.CustomerWeb.Models;

public record InfoSection(string Heading, string[] Paragraphs);

public record InfoPage(string Eyebrow, string Title, string Intro, InfoSection[] Sections);

public static class InfoPages
{
    public static readonly Dictionary<string, InfoPage> All = new()
    {
        ["doi-tra"] = new("HỖ TRỢ", "Chính sách đổi trả",
            "SPORTSHOP hỗ trợ đổi trả trong vòng 30 ngày kể từ ngày nhận hàng.",
        [
            new("Điều kiện đổi trả", ["Sản phẩm còn nguyên tem, nhãn mác, chưa qua sử dụng và chưa giặt.", "Có hóa đơn hoặc mã đơn hàng của SPORTSHOP."]),
            new("Trường hợp được đổi trả", ["Sản phẩm bị lỗi do nhà sản xuất hoặc hư hỏng trong quá trình vận chuyển.", "Giao sai sản phẩm, sai size hoặc sai màu so với đơn đặt.", "Đổi size khi còn hàng trong kho."]),
            new("Cách thực hiện", ["Liên hệ SPORTSHOP qua hotline 1900 6868 hoặc email support@sportshop.vn kèm mã đơn hàng.", "Nhân viên xác nhận và hướng dẫn gửi lại sản phẩm. Tiền hoàn trả được xử lý trong 3–5 ngày làm việc."]),
        ]),
        ["giao-hang"] = new("HỖ TRỢ", "Giao hàng",
            "SPORTSHOP giao hàng toàn quốc, theo dõi trạng thái ngay trong mục \"Đơn hàng của tôi\".",
        [
            new("Phí giao hàng", ["Miễn phí giao hàng cho đơn từ 500.000₫.", "Đơn dưới 500.000₫ có phí giao hàng 30.000₫."]),
            new("Thời gian giao hàng", ["Nội thành TP.HCM: 1–2 ngày làm việc.", "Các tỉnh thành khác: 3–5 ngày làm việc."]),
            new("Theo dõi đơn hàng", ["Đơn hàng đi qua các bước: chờ xác nhận, đang chuẩn bị, đang giao, đã giao. Khi đơn đang giao bạn sẽ thấy đơn vị vận chuyển và mã vận đơn."]),
        ]),
        ["chon-size"] = new("HỖ TRỢ", "Hướng dẫn chọn size",
            "Chọn đúng size giúp bạn thoải mái hơn khi vận động.",
        [
            new("Giày thể thao", ["Đo chiều dài bàn chân (từ gót đến ngón dài nhất) vào cuối ngày, khi chân giãn nở nhất.", "Nên chọn size dư khoảng 0,5–1 cm so với chiều dài bàn chân để chân không bị chật khi chạy."]),
            new("Quần áo thể thao", ["Đo vòng ngực, vòng eo và vòng mông, đối chiếu với bảng size ghi ở trang sản phẩm.", "Nếu số đo nằm giữa hai size, nên chọn size lớn hơn cho đồ tập thoải mái."]),
            new("Cần tư vấn thêm?", ["Liên hệ hotline 1900 6868 (8:00–22:00) để được nhân viên tư vấn size."]),
        ]),
        ["lien-he"] = new("HỖ TRỢ", "Liên hệ",
            "Chúng tôi luôn sẵn sàng lắng nghe và hỗ trợ bạn.",
        [
            new("Thông tin liên hệ", ["Hotline: 1900 6868 (8:00–22:00 mỗi ngày).", "Email: support@sportshop.vn", "Địa chỉ: 128 Nguyễn Trãi, Quận 1, TP.HCM."]),
            new("Góp ý và khiếu nại", ["Bạn có thể gửi phản hồi về sản phẩm đã mua ngay trong tài khoản, mục \"Gửi phản hồi về sản phẩm\". Mỗi phản hồi đều được đội ngũ SPORTSHOP đọc và trả lời."]),
        ]),
        ["ve-chung-toi"] = new("SPORTSHOP", "Về chúng tôi",
            "SPORTSHOP đồng hành cùng bạn trên hành trình sống khỏe và chinh phục mọi giới hạn.",
        [
            new("Chúng tôi là ai", ["SPORTSHOP là cửa hàng đồ thể thao chính hãng cho các môn chạy bộ, bóng đá, bóng rổ, cầu lông và tập luyện tại phòng gym."]),
            new("Cam kết", ["100% sản phẩm chính hãng.", "Đổi trả trong 30 ngày.", "Miễn phí giao hàng cho đơn từ 500.000₫.", "Hỗ trợ tận tâm, phản hồi của khách hàng luôn được lắng nghe."]),
        ]),
        ["he-thong-cua-hang"] = new("SPORTSHOP", "Hệ thống cửa hàng",
            "Ghé thăm cửa hàng để trải nghiệm và thử sản phẩm trực tiếp.",
        [
            new("Cửa hàng trung tâm", ["128 Nguyễn Trãi, Quận 1, TP.HCM.", "Giờ mở cửa: 8:00–22:00 mỗi ngày.", "Hotline: 1900 6868."]),
            new("Mua online", ["Bạn cũng có thể mua hàng trực tiếp trên website và nhận hàng tại nhà."]),
        ]),
        ["tuyen-dung"] = new("SPORTSHOP", "Tuyển dụng",
            "Gia nhập đội ngũ SPORTSHOP và cùng lan tỏa tinh thần thể thao.",
        [
            new("Vị trí đang tuyển", ["Nhân viên bán hàng tại cửa hàng.", "Nhân viên chăm sóc khách hàng.", "Nhân viên kho và giao nhận."]),
            new("Cách ứng tuyển", ["Gửi hồ sơ về email support@sportshop.vn với tiêu đề \"Ứng tuyển - tên vị trí\"."]),
        ]),
    };
}
