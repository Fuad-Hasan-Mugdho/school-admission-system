namespace SchoolAdmission.Api.Domain;

/// <summary>
/// A student's application for one seat in one class.
/// While <see cref="Status"/> is HELD and <see cref="ExpiresAt"/> is in the future the seat is reserved.
/// </summary>
public sealed class Hold
{
    public required string Id { get; init; }
    public required string ClassId { get; init; }
    public SchoolClass? Class { get; init; }
    public required string StudentId { get; init; }
    public required HoldStatus Status { get; set; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// The status a caller should see at <paramref name="now"/>.
    /// A HELD row whose deadline passed is EXPIRED even if the background sweeper
    /// has not persisted that yet - this is what makes expiry "automatic" without any API call.
    /// </summary>
    public HoldStatus EffectiveStatus(DateTime now) =>
        Status == HoldStatus.HELD && ExpiresAt <= now ? HoldStatus.EXPIRED : Status;

    public bool IsActiveAt(DateTime now) =>
        EffectiveStatus(now) is HoldStatus.HELD or HoldStatus.CONFIRMED;
}
