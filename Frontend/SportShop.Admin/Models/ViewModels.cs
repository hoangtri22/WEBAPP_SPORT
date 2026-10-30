namespace SportShop.Admin.Models;

public record HomeVm(decimal RevToday, decimal RevYesterday, int OrdToday, int OrdYesterday, int StaffTotal, int StaffWorking,
    int Customers, int CustomersActive, int Unanswered, int FollowToday, List<Order> Recent, Dictionary<int, string> CustomerNames);

public record FeedbackVm(List<FeedbackItem> Items, Dictionary<int, Customer> Customers, Dictionary<int, string> EmployeeNames);

public record ProductsVm(List<string> Categories, List<Product> Products, Dictionary<int, List<Variant>> Variants, List<Row> VariantRows);

public record AccountDetailVm(Account Account, Employee? Employee, Customer? Customer);

public record CustomerDetailVm(Customer Customer, List<Order> Orders, List<CareNote> Notes, Dictionary<int, string> VariantNames, Dictionary<int, string> EmployeeNames);

public record GroupDetailVm(CustomerGroup Group, List<Customer> Members);

public record EmployeeDetailVm(Employee Employee, List<Order> Orders, int Imports, int Care, int Replies, Dictionary<int, string> CustomerNames);

public record OrderDetailVm(Order Order, Customer Customer, string EmployeeName, Dictionary<int, string> VariantNames);

public record CategoryDetailVm(Category Category, List<Product> Products, Dictionary<int, List<Variant>> Variants);

public record ProductDetailVm(Product Product, List<Variant> Variants, List<OrderLine> Sold);

public record SupplierDetailVm(Supplier Supplier, List<ImportSlip> Slips);

public record ImportDetailVm(ImportSlip Slip, List<(string Name, int Qty, decimal Price, decimal Amount)> Lines, Supplier? Supplier);

public record PromotionDetailVm(Promotion Promotion, List<Order> Orders, List<Campaign> Campaigns, Dictionary<int, string> CustomerNames);

public record CampaignDetailVm(Campaign Campaign, string Content, Promotion? Promotion, CustomerGroup? Group);
