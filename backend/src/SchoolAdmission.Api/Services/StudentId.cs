namespace SchoolAdmission.Api.Services;

/// <summary>
/// The X-Student-Id header carries the student's 17-digit birth registration number.
/// </summary>
public static class StudentId
{
    public const string HeaderName = "X-Student-Id";
    public const int Length = 17;

    public static bool IsValid(string? value) =>
        value is { Length: Length } && value.All(char.IsAsciiDigit);
}
