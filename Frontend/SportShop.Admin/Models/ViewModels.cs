namespace SportShop.Admin.Models;

// Dữ liệu trang Tổng quan.
public record HomeVm(decimal RevToday, decimal RevYesterday, int OrdToday, int OrdYesterday, int StaffTotal, int StaffWorking,
    int Customers, int CustomersActive, int Unanswered, int FollowToday, List<Order> Recent, Dictionary<int, string> CustomerNames);

// Dữ liệu trang Phản hồi khách hàng.
public record FeedbackVm(List<FeedbackItem> Items, Dictionary<int, Customer> Customers, Dictionary<int, string> EmployeeNames);

// Dữ liệu trang Quản lý sản phẩm.
public record ProductsVm(List<string> Categories, List<Product> Products, Dictionary<int, List<Variant>> Variants, List<Row> VariantRows);

// Dữ liệu trang chi tiết tài khoản.
public record AccountDetailVm(Account Account, Employee? Employee, Customer? Customer);

// Dữ liệu trang chi tiết khách hàng.
public record CustomerDetailVm(Customer Customer, string Password, List<Order> Orders, List<CareNote> Notes, Dictionary<int, string> VariantNames, Dictionary<int, string> EmployeeNames);

// Dữ liệu trang chi tiết nhóm khách.
public record GroupDetailVm(CustomerGroup Group, List<Customer> Members);

// Dữ liệu trang chi tiết nhân viên.
public record EmployeeDetailVm(Employee Employee, List<Order> Orders, int Imports, int Care, int Replies, Dictionary<int, string> CustomerNames);

// Dữ liệu trang chi tiết đơn hàng.
public record OrderDetailVm(Order Order, Customer Customer, string EmployeeName, Dictionary<int, string> VariantNames);

// Dữ liệu trang chi tiết loại sản phẩm.
public record CategoryDetailVm(Category Category, List<Product> Products, Dictionary<int, List<Variant>> Variants);

// Dữ liệu trang chi tiết sản phẩm.
public record ProductDetailVm(Product Product, List<Variant> Variants, List<OrderLine> Sold);

// Dữ liệu trang chi tiết nhà cung cấp.
public record SupplierDetailVm(Supplier Supplier, List<ImportSlip> Slips);

// Dữ liệu trang chi tiết phiếu nhập.
public record ImportDetailVm(ImportSlip Slip, List<(string Name, int Qty, decimal Price, decimal Amount)> Lines, Supplier? Supplier);

// Dữ liệu trang chi tiết khuyến mãi.
public record PromotionDetailVm(Promotion Promotion, List<Order> Orders, List<Campaign> Campaigns, Dictionary<int, string> CustomerNames);

// Dữ liệu trang chi tiết chiến dịch.
public record CampaignDetailVm(Campaign Campaign, string Content, Promotion? Promotion, CustomerGroup? Group);
