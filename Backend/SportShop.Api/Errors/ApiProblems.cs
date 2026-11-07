using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SportShop.Api.Errors;

// Tạo ProblemDetails thống nhất cho lỗi của API.
internal static class ApiProblems
{
    // Gắn instance, traceId và code vào ProblemDetails.
    public static void Enrich(ProblemDetails problem, HttpContext context, string? code = null)
    {
        problem.Instance ??= context.Request.Path;
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.TraceIdentifier);
        problem.Extensions.TryAdd("code", code ?? (problem.Status == 400 ? "VALIDATION_ERROR" : "HTTP_ERROR"));
    }

    // Tạo ProblemDetails; controller và handler lỗi của API gọi hàm này.
    public static ProblemDetails Create(HttpContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails { Type = "about:blank", Status = status, Title = title };
        Enrich(problem, context, code);
        return problem;
    }

    // Ghi ProblemDetails ra response dạng application/problem+json.
    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        return context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
