using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Application.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Authorize(Roles = "Customer"), Route("api/v1/me/surveys")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
public sealed class SurveysController(CustomerStore customers, ICatalogClock clock) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lists surveys assigned to the customer, including completion and availability.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<SurveyResponse>>(200)]
    public IActionResult List([Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 20)
    {
        var all = customers.Surveys(CustomerId);
        return Ok(new PagedResponse<SurveyResponse>(all.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).Select(Map).ToList(), page, pageSize, all.Count));
    }

    /// <summary>Loads an assigned survey's questions and choices. Unassigned and draft surveys return 404.</summary>
    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType<SurveyDetailResponse>(200), ProducesResponseType<ProblemDetails>(404)]
    public IActionResult Get(int id)
    {
        var survey = customers.Surveys(CustomerId).FirstOrDefault(s => s.Id == id);
        return survey is null ? Error(404, "RESOURCE_NOT_FOUND", "Không tìm thấy khảo sát.") : Ok(new SurveyDetailResponse(Map(survey),
            customers.Questions(id).Select(q => new SurveyQuestionResponse(q.Id, q.Title, q.Type, q.Required,
                q.Options.Select(o => new SurveyOptionResponse(o.Id, o.Text)).ToList())).ToList()));
    }

    /// <summary>Submits answers once. Validates ownership, dates, required answers and choice membership atomically.</summary>
    [HttpPost("{id:int:min(1)}/responses")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
    public IActionResult Submit(int id, SubmitSurveyRequest request)
    {
        if (!customers.Surveys(CustomerId).Any(s => s.Id == id)) return Error(404, "RESOURCE_NOT_FOUND", "Không tìm thấy khảo sát.");
        if (request.Answers.Any(a => a is null || a.Options is null) || request.Answers.Select(a => a.QuestionId).Distinct().Count() != request.Answers.Count)
            return Error(400, "VALIDATION_ERROR", "Câu trả lời bị trùng hoặc không hợp lệ.");
        var error = customers.SubmitSurvey(id, CustomerId, request.Answers.ToDictionary(a => a.QuestionId, a => (a.Options, a.Text)));
        return error is null ? NoContent() : Error(409, "SURVEY_SUBMISSION_FAILED", error);
    }

    IActionResult Error(int status, string code, string title) => StatusCode(status, ApiProblems.Create(HttpContext, status, code, title));

    SurveyResponse Map(SportShop.CustomerWeb.Models.SurveyItem s) => new(s.Id, s.Title, s.Description, clock.WithOffset(s.Deadline),
        s.Status, s.CompletedAt.HasValue ? clock.WithOffset(s.CompletedAt.Value) : null, s.QuestionCount, s.IsOpen, s.Done);
}
