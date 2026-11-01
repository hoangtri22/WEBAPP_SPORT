using Microsoft.AspNetCore.Diagnostics;
using SportShop.Application;
using MySqlConnector;
using SportShop.Infrastructure;

namespace SportShop.Api.Errors;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var databaseUnavailable = exception is DatabaseUnavailableException ||
            exception is MySqlException mysql && MySqlConnectionFactory.IsUnavailable(mysql);
        if (exception is MySqlException { Number: 1062 })
        {
            await ApiProblems.WriteAsync(context, ApiProblems.Create(context, 409, "DUPLICATE_RESOURCE", "Dữ liệu đã tồn tại."));
            return true;
        }
        var status = databaseUnavailable ? 503 : 500;
        var code = databaseUnavailable ? "DATABASE_UNAVAILABLE" : "INTERNAL_ERROR";
        logger.LogError(exception, "API request failed: {Code}, request {RequestId}", code, context.TraceIdentifier);
        await ApiProblems.WriteAsync(context, ApiProblems.Create(context, status, code,
            databaseUnavailable ? "Cơ sở dữ liệu chưa sẵn sàng. Vui lòng thử lại sau."
                : "Đã xảy ra lỗi. Vui lòng thử lại sau."));
        return true;
    }
}
