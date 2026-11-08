namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One admissible unit. Each shift is modelled as its own class
/// (e.g. "Class 6 - Morning Shift" and "Class 6 - Day Shift" are two rows).
/// </summary>
public sealed class SchoolClass
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required int TotalSeats { get; set; }
    public required DateTime CreatedAt { get; init; }

    public ICollection<Hold> Holds { get; init; } = new List<Hold>();
}
