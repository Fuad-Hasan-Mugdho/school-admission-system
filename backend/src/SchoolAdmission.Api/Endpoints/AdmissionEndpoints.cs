using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class AdmissionEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }))
            .WithTags("Health");

        var classes = app.MapGroup("/classes").WithTags("Classes");

        classes.MapPost("/", CreateClass)
            .Produces<ClassCreatedResponse>(201)
            .Produces<ErrorResponse>(400);

        classes.MapGet("/", async (AdmissionService service, CancellationToken ct) =>
                Results.Ok(await service.GetClassesAsync(ct)))
            .Produces<IReadOnlyList<ClassSeatResponse>>(200);

        classes.MapGet("/{classId}", async (string classId, AdmissionService service, CancellationToken ct) =>
            {
                var cls = await service.GetClassAsync(classId, ct);
                return cls is null ? ApiErrors.FromOutcome(Outcome.ClassNotFound) : Results.Ok(cls);
            })
            .Produces<ClassSeatResponse>(200)
            .Produces<ErrorResponse>(404);

        classes.MapPost("/{classId}/holds", CreateHold)
            .Produces<HoldCreatedResponse>(201)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(409);

        var holds = app.MapGroup("/holds").WithTags("Holds");

        holds.MapGet("/{holdId}", async (string holdId, AdmissionService service, CancellationToken ct) =>
            {
                var hold = await service.GetHoldAsync(holdId, ct);
                return hold is null ? ApiErrors.FromOutcome(Outcome.HoldNotFound) : Results.Ok(hold);
            })
            .Produces<HoldResponse>(200)
            .Produces<ErrorResponse>(404);

        holds.MapPost("/{holdId}/confirm", ConfirmHold)
            .Produces<HoldConfirmedResponse>(200)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(409);

        holds.MapDelete("/{holdId}", CancelHold)
            .Produces(204)
            .Produces<ErrorResponse>(400)
            .Produces<ErrorResponse>(403)
            .Produces<ErrorResponse>(404)
            .Produces<ErrorResponse>(409);

        app.MapGet("/me/holds", MyHolds)
            .WithTags("Me")
            .Produces<IReadOnlyList<MyHoldResponse>>(200)
            .Produces<ErrorResponse>(400);

        return app;
    }

    private static async Task<IResult> CreateClass(
        [FromBody] CreateClassRequest? body, AdmissionService service, CancellationToken ct)
    {
        if (body is null)
        {
            return ApiErrors.BadRequest("Request body is required: { \"name\": string, \"totalSeats\": number }.");
        }

        var details = new Dictionary<string, string>();
        var name = body.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            details["name"] = "Class name is required.";
        }
        else if (name.Length > 200)
        {
            details["name"] = "Class name must be at most 200 characters.";
        }

        if (body.TotalSeats is null)
        {
            details["totalSeats"] = "totalSeats is required.";
        }
        else if (body.TotalSeats <= 0)
        {
            details["totalSeats"] = "totalSeats must be a positive whole number.";
        }
        else if (body.TotalSeats > 100_000)
        {
            details["totalSeats"] = "totalSeats is unrealistically large (max 100000).";
        }

        if (details.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid class data.", details);
        }

        var created = await service.CreateClassAsync(name!, body.TotalSeats!.Value, ct);
        return Results.Created($"/classes/{created.Id}", created);
    }

    private static async Task<IResult> CreateHold(
        string classId, HttpRequest request, AdmissionService service, CancellationToken ct)
    {
        if (!TryGetStudentId(request, out var studentId)) return ApiErrors.StudentIdRequired();

        var result = await service.CreateHoldAsync(classId, studentId, ct);
        return result.IsSuccess
            ? Results.Created($"/holds/{result.Value!.HoldId}", result.Value)
            : ApiErrors.FromOutcome(result.Outcome);
    }

    private static async Task<IResult> ConfirmHold(
        string holdId, HttpRequest request, AdmissionService service, CancellationToken ct)
    {
        if (!TryGetStudentId(request, out var studentId)) return ApiErrors.StudentIdRequired();

        var result = await service.ConfirmAsync(holdId, studentId, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiErrors.FromOutcome(result.Outcome);
    }

    private static async Task<IResult> CancelHold(
        string holdId, HttpRequest request, AdmissionService service, CancellationToken ct)
    {
        if (!TryGetStudentId(request, out var studentId)) return ApiErrors.StudentIdRequired();

        var result = await service.CancelAsync(holdId, studentId, ct);
        return result.IsSuccess ? Results.NoContent() : ApiErrors.FromOutcome(result.Outcome);
    }

    private static async Task<IResult> MyHolds(HttpRequest request, AdmissionService service, CancellationToken ct)
    {
        if (!TryGetStudentId(request, out var studentId)) return ApiErrors.StudentIdRequired();
        return Results.Ok(await service.GetMyHoldsAsync(studentId, ct));
    }

    private static bool TryGetStudentId(HttpRequest request, out string studentId)
    {
        studentId = request.Headers[StudentId.HeaderName].ToString().Trim();
        return StudentId.IsValid(studentId);
    }
}
