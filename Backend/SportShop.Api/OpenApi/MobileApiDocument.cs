using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace SportShop.Api.OpenApi;

internal static class MobileApiDocument
{
    public static void Complete(OpenApiDocument document)
    {
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "opaque",
            Description = "Access token từ /auth/login hoặc /auth/register. Phiên 15 phút; refresh token được xoay vòng và có hạn tuyệt đối 30 ngày."
        };
        foreach (var (path, item) in document.Paths)
        foreach (var (method, operation) in item.Operations ?? [])
        {
            var owned = path.StartsWith("/api/v1/me", StringComparison.Ordinal) ||
                path.StartsWith("/api/v1/cart", StringComparison.Ordinal) ||
                path.StartsWith("/api/v1/checkout", StringComparison.Ordinal) ||
                path.StartsWith("/api/v1/orders", StringComparison.Ordinal) || path == "/api/v1/auth/logout";
            if (owned)
            {
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
                Error(operation, document, "401", "UNAUTHENTICATED", "Thiếu token hoặc phiên không hợp lệ/hết hạn.");
                Error(operation, document, "403", "FORBIDDEN", "Yêu cầu tài khoản khách hàng.");
            }
            Error(operation, document, "500", "INTERNAL_ERROR", "Lỗi máy chủ; dùng traceId khi yêu cầu hỗ trợ.");
            if (!path.StartsWith("/api/v1/pages", StringComparison.Ordinal) && path != "/health/live")
                Error(operation, document, "503", "DATABASE_UNAVAILABLE", "Kết nối hoặc schema CSDL chưa sẵn sàng; kiểm tra migrations.");
            if (operation.RequestBody?.Content is { } content)
            {
                Error(operation, document, "400", "VALIDATION_ERROR", "Body, tham số hoặc header không hợp lệ.");
                if (content.TryGetValue("application/json", out var json) && RequestExamples.TryGetValue(path, out var example))
                    json.Example = JsonNode.Parse(example);
            }
        }
    }

    static void Error(OpenApiOperation operation, OpenApiDocument document, string status, string code, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(status, new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = new OpenApiSchemaReference("ProblemDetails", document),
                    Example = new JsonObject { ["type"] = "about:blank", ["status"] = int.Parse(status),
                        ["title"] = description, ["code"] = code, ["traceId"] = "request-trace-id" }
                }
            }
        });
    }

    static readonly Dictionary<string, string> RequestExamples = new()
    {
        ["/api/v1/auth/register"] = """{"username":"mobile_customer","email":"customer@example.com","phone":"0901234567","password":"replace-with-user-password"}""",
        ["/api/v1/auth/login"] = """{"username":"mobile_customer","password":"replace-with-user-password"}""",
        ["/api/v1/auth/refresh"] = """{"refreshToken":"<refresh-token-from-login>"}""",
        ["/api/v1/me"] = """{"fullName":"Nguyễn Minh An","email":"customer@example.com","phone":"0901234567","birth":"2000-01-01","gender":"Nam","interests":["Chạy bộ"]}""",
        ["/api/v1/me/password"] = """{"currentPassword":"<current-password>","newPassword":"<new-password>"}""",
        ["/api/v1/me/feedback"] = """{"productId":1,"stars":5,"content":"Sản phẩm phù hợp.","orderId":1}""",
        ["/api/v1/me/surveys/{id}/responses"] = """{"answers":[{"questionId":1,"options":[1],"text":null}]}""",
        ["/api/v1/cart/items"] = """{"variantId":1,"quantity":1}""",
        ["/api/v1/cart/items/{variantId}"] = """{"quantity":2}""",
        ["/api/v1/checkout/quote"] = """{"voucher":null}""",
        ["/api/v1/orders"] = """{"name":"Nguyễn Minh An","phone":"0901234567","address":"128 Nguyễn Trãi, TP.HCM","paymentMethodId":1,"voucher":null}"""
    };
}
