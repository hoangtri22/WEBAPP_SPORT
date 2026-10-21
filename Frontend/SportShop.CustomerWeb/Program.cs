using Microsoft.Extensions.FileProviders;
using SportShop.CustomerWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// appsettings.Local.json (nếu có) ghi đè cấu hình từng máy, ví dụ mật khẩu MySQL; file này không đưa lên GitHub
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();   // biến môi trường vẫn được ưu tiên cao nhất

builder.Services.AddControllersWithViews();

// Phiên làm việc (session) dùng để nhớ khách đang đăng nhập
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// Cờ cho biết đã kết nối cơ sở dữ liệu hay chưa
builder.Services.AddSingleton<DbStatus>();
// Đọc / ghi dữ liệu khách hàng trong MySQL
builder.Services.AddScoped<CustomerStore>();
// Cửa hàng: sản phẩm, giỏ hàng, đặt hàng, đơn hàng của tôi
builder.Services.AddScoped<ShopStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Ảnh sản phẩm: thư mục Backend/Images được phục vụ tại đường dẫn /images
// (khớp cột SAN_PHAM.hinh_anh = "/images/ten-anh.jpg"). Đổi thư mục ở appsettings.json → Paths:Images nếu cần.
var imagesDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Paths:Images"] ?? "../../Backend/Images"));
if (Directory.Exists(imagesDir))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(imagesDir), RequestPath = "/images" });
else
    app.Logger.LogWarning("Không tìm thấy thư mục ảnh sản phẩm: {Dir}", imagesDir);

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
