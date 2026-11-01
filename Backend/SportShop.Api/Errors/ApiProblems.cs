using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SportShop.Api.Errors;

internal static class ApiProblems
{
    public static void Enrich(ProblemDetails problem, HttpContext context, string? code = null)
    {
        problem.Instance ??= context.Request.Path;
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.TraceIdentifier);
        problem.Extensions.TryAdd("code", code ?? (problem.Status == 400 ? "VALIDATION_ERROR" : "HTTP_ERROR"));
    }

    public static ProblemDetails Create(HttpContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails { Type = "about:blank", Status = status, Title = title };
        Enrich(problem, context, code);
        return problem;
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        return context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
