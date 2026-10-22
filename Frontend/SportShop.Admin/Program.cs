using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.FileProviders;
using MySqlConnector;
using SportShop.Admin.Models;

var builder = WebApplication.CreateBuilder(args);

// appsettings.Local.json (nếu có) ghi đè cấu hình từng máy, ví dụ mật khẩu MySQL; file này không đưa lên GitHub
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();   // biến môi trường vẫn được ưu tiên cao nhất

// Đọc dữ liệu từ MySQL (chuỗi kết nối: appsettings.json → ConnectionStrings:Default)
builder.Services.AddScoped<ShopData>();
// Ghi dữ liệu (thêm / sửa / xóa) vào MySQL
builder.Services.AddSingleton<ShopCommands>();

// Đăng nhập bằng cookie: chưa đăng nhập thì mọi trang đều chuyển về màn hình đăng nhập "/"
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AuthorizeFilter()));

var app = builder.Build();

// Dọn dữ liệu cũ: đơn đã hủy nhưng còn hóa đơn thì hoàn tiền lại cho khách (không tính vào tổng chi tiêu)
app.Services.GetRequiredService<ShopCommands>().ReconcileCancelled();

// Không kết nối được MySQL (hoặc chưa điền chuỗi kết nối) → hiện trang hướng dẫn thay vì lỗi 500 khó hiểu
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex) when (!ctx.Response.HasStarted && (ex is MySqlException || ex.Message.Contains("ConnectionStrings:Default")))
    {
        var cs = new MySqlConnectionStringBuilder();
        try { cs.ConnectionString = app.Configuration.GetConnectionString("Default") ?? ""; } catch { }
        ctx.Response.StatusCode = 503;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync($"""
            <!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"><title>Lỗi kết nối CSDL</title>
            <style>body{"{"}font-family:system-ui,sans-serif;max-width:720px;margin:48px auto;padding:0 16px;line-height:1.6{"}"}
            code{"{"}background:#f1f5f4;padding:2px 6px;border-radius:4px{"}"}</style></head><body>
            <h1>Không kết nối được MySQL</h1>
            <p>Web đang kết nối tới <code>{WebUtility.HtmlEncode(cs.Server)}:{cs.Port}</code>, CSDL <code>{WebUtility.HtmlEncode(cs.Database)}</code>, user <code>{WebUtility.HtmlEncode(cs.UserID)}</code>.</p>
            <p>Lỗi: <code>{WebUtility.HtmlEncode(ex.Message)}</code></p>
            <ol>
              <li>Kiểm tra MySQL đã chạy (dịch vụ <b>MySQL80</b> trong Services).</li>
              <li>Đã chạy script <code>Backend/Database/sports_shop.sql</code> để tạo CSDL.</li>
              <li>Kiểm tra user / mật khẩu trong <code>ConnectionStrings:Default</code> của <code>appsettings.json</code>.</li>
            </ol></body></html>
            """);
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Trang gốc "/" là màn hình đăng nhập
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Account", action = "Login" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
