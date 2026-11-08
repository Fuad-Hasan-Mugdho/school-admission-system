using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

// ---- Requests ----

public sealed record CreateClassRequest(string? Name, int? TotalSeats);

// ---- Responses (field names below are part of the public contract; only additions are allowed) ----

public sealed record ClassCreatedResponse(string Id, string Name, int TotalSeats);

public sealed record ClassSeatResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

public sealed record HoldCreatedResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTime ExpiresAt,
    // extra, not in the spec: lets the UI compute a drift-free countdown
    DateTime ServerTime);

public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTime ExpiresAt,
    // extras
    string ClassName,
    string StudentId,
    DateTime ServerTime);

public sealed record HoldConfirmedResponse(string HoldId, string Status);

public sealed record MyHoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTime ExpiresAt,
    // extras
    DateTime CreatedAt,
    DateTime ServerTime);

public sealed record ErrorResponse(string Error, string Message)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Details { get; init; }
}

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string StudentIdRequired = "STUDENT_ID_REQUIRED";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string SeatUnavailable = "SEAT_UNAVAILABLE";
    public const string AlreadyApplied = "ALREADY_APPLIED";
    public const string NotOwner = "NOT_OWNER";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldConfirmed = "HOLD_CONFIRMED";
    public const string InternalError = "INTERNAL_ERROR";
}
