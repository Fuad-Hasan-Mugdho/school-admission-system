namespace SchoolAdmission.Api.Domain;

/// <summary>
/// Lifecycle of a seat application ("hold"). Names are serialized verbatim,
/// so they must stay upper-case to match the API contract.
/// </summary>
public enum HoldStatus
{
    HELD,
    CONFIRMED,
    EXPIRED,
    CANCELLED
}
