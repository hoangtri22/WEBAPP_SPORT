using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using SportShop.Api.Auth;
using SportShop.CustomerWeb.Services;
using SportShop.Api.Errors;
using SportShop.Api.Media;
using SportShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Ghi log ra console để chạy được cả với tài khoản không có quyền Windows Event Log.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();

// Cấu hình API độc lập với hai web, không đọc secret của chúng.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSportShopInfrastructure();
builder.Services.AddScoped<CustomerStore>();
builder.Services.AddScoped<ShopStore>();
builder.Services.AddSingleton<CartStore>();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MobileTokenStore>();
builder.Services.AddScoped<GuestDeviceStore>();
builder.Services.AddScoped<PasswordResetStore>();
builder.Services.AddScoped<GoogleAccountStore>();
builder.Services.AddSingleton<GoogleLoginTickets>();
builder.Services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
builder.Services.AddSingleton<IPasswordResetMailer, SmtpPasswordResetMailer>();
builder.Services.AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<MediaUrlResolver>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => ApiProblems.Enrich(context.ProblemDetails, context.HttpContext));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu yêu cầu không hợp lệ.",
            Type = "about:blank"
        };
        ApiProblems.Enrich(problem, context.HttpContext, "VALIDATION_ERROR");
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "SportShop Mobile API";
    document.Info.Version = "v1";
    document.Info.Description = "API dùng chung nghiệp vụ Web User: catalog, tài khoản, giỏ, checkout, đơn hàng, phản hồi, khảo sát và trang hỗ trợ. Thanh toán hiện mô phỏng như web.";
    SportShop.Api.OpenApi.MobileApiDocument.Complete(document);
    return Task.CompletedTask;
}));

var app = builder.Build();

// Khởi tạo một lần lúc chạy để kiểm tra cấu hình media mà không mở CSDL.
var media = app.Services.GetRequiredService<MediaUrlResolver>();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    var code = status == 404 ? "RESOURCE_NOT_FOUND" : "HTTP_ERROR";
    await ApiProblems.WriteAsync(context.HttpContext,
        ApiProblems.Create(context.HttpContext, status, code,
            status == 404 ? "Không tìm thấy tài nguyên." : "Không thể thực hiện yêu cầu."));
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

media.MapStaticFiles(app);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.Run();

// Để test HTTP chạy pipeline thật mà không cần hai web MVC.
public partial class Program { }
