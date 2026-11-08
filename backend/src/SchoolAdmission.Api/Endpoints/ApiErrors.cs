using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class ApiErrors
{
    public static IResult Problem(int status, string code, string message, IDictionary<string, string>? details = null) =>
        Results.Json(new ErrorResponse(code, message) { Details = details }, statusCode: status);

    public static IResult BadRequest(string message, IDictionary<string, string>? details = null) =>
        Problem(StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, message, details);

    public static IResult StudentIdRequired() =>
        Problem(StatusCodes.Status400BadRequest, ErrorCodes.StudentIdRequired,
            $"The {StudentId.HeaderName} header is required and must be the student's 17-digit birth registration number.");

    public static IResult FromOutcome(Outcome outcome) => outcome switch
    {
        Outcome.ClassNotFound => Problem(404, ErrorCodes.ClassNotFound, "No class exists with this id."),
        Outcome.HoldNotFound => Problem(404, ErrorCodes.HoldNotFound, "No application exists with this id."),
        Outcome.SeatUnavailable => Problem(409, ErrorCodes.SeatUnavailable, "No seats are available in this class right now."),
        Outcome.AlreadyApplied => Problem(409, ErrorCodes.AlreadyApplied, "This student already has an active application or a confirmed admission in this class."),
        Outcome.NotOwner => Problem(403, ErrorCodes.NotOwner, "This application belongs to a different student."),
        Outcome.HoldExpired => Problem(409, ErrorCodes.HoldExpired, "This application has expired; the seat has been released. Please apply again."),
        Outcome.HoldCancelled => Problem(409, ErrorCodes.HoldCancelled, "This application was cancelled and can no longer be confirmed."),
        Outcome.HoldConfirmed => Problem(409, ErrorCodes.HoldConfirmed, "Admission is already confirmed for this application; it cannot be cancelled here."),
        _ => Problem(500, ErrorCodes.InternalError, "Unexpected error."),
    };
}
