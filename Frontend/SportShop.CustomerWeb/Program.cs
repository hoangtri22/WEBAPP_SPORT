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
    o.Cookie.SameSite = SameSiteMode.Lax;   // không gửi cookie phiên kèm yêu cầu từ trang web khác
});

// Cờ cho biết đã kết nối cơ sở dữ liệu hay chưa
builder.Services.AddSingleton<DbStatus>();
// Đọc / ghi dữ liệu khách hàng trong MySQL
builder.Services.AddScoped<CustomerStore>();
// Cửa hàng: sản phẩm, giỏ hàng, đặt hàng, đơn hàng của tôi
builder.Services.AddScoped<ShopStore>();
// Giỏ hàng của từng tài khoản, lưu trong file App_Data/carts.json (còn nguyên ở các lần đăng nhập sau)
builder.Services.AddSingleton<CartStore>();
// Chống dò mật khẩu: khóa tạm khi đăng nhập sai nhiều lần
builder.Services.AddSingleton<LoginThrottle>();
// Cookie chống giả mạo yêu cầu (antiforgery): chỉ gửi trong cùng trang web
builder.Services.AddAntiforgery(o => o.Cookie.SameSite = SameSiteMode.Strict);

var app = builder.Build();

// Header bảo mật: chặn đoán kiểu file, chặn nhúng trang vào khung của trang khác, hạn chế gửi địa chỉ trang khi chuyển liên kết
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

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

// Ảnh sản phẩm tải lên từ thiết bị (do web admin lưu) ở Frontend/Uploads, phục vụ tại đường dẫn /uploads
var uploadsDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Paths:Uploads"] ?? "../Uploads"));
Directory.CreateDirectory(uploadsDir);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploadsDir), RequestPath = "/uploads" });

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
