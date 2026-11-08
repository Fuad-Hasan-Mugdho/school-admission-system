using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

/// <summary>Outcome of an operation; endpoints translate these into HTTP responses.</summary>
public enum Outcome
{
    Ok,
    ClassNotFound,
    HoldNotFound,
    SeatUnavailable,
    AlreadyApplied,
    NotOwner,
    HoldExpired,
    HoldCancelled,
    HoldConfirmed
}

public readonly record struct Result<T>(Outcome Outcome, T? Value)
{
    public static Result<T> Success(T value) => new(Outcome.Ok, value);
    public static Result<T> Fail(Outcome outcome) => new(outcome, default);
    public bool IsSuccess => Outcome == Outcome.Ok;
}

public sealed class AdmissionService(AdmissionDbContext db, HoldOptions options, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ classes

    public async Task<ClassCreatedResponse> CreateClassAsync(string name, int totalSeats, CancellationToken ct)
    {
        var entity = new SchoolClass
        {
            Id = Ids.NewClassId(),
            Name = name.Trim(),
            TotalSeats = totalSeats,
            CreatedAt = Now,
        };
        db.Classes.Add(entity);
        await db.SaveChangesAsync(ct);
        return new ClassCreatedResponse(entity.Id, entity.Name, entity.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassSeatResponse>> GetClassesAsync(CancellationToken ct)
    {
        var now = Now;
        var rows = await db.Classes
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                Held = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > now),
                Confirmed = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED),
            })
            .ToListAsync(ct);

        return rows.Select(r => ToSeatResponse(r.Id, r.Name, r.TotalSeats, r.Held, r.Confirmed)).ToList();
    }

    public async Task<ClassSeatResponse?> GetClassAsync(string classId, CancellationToken ct)
    {
        var now = Now;
        var row = await db.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                Held = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > now),
                Confirmed = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED),
            })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : ToSeatResponse(row.Id, row.Name, row.TotalSeats, row.Held, row.Confirmed);
    }

    private static ClassSeatResponse ToSeatResponse(string id, string name, int total, int held, int confirmed) =>
        new(id, name, total, held, confirmed, Math.Max(0, total - held - confirmed));

    // ------------------------------------------------------------------ holds

    /// <summary>
    /// Reserve one seat. Correctness under concurrency relies on a per-class row lock
    /// (SELECT ... FOR UPDATE) taken inside a transaction: every competing request for the
    /// same class - from any process sharing the database - queues on that lock, so the
    /// "count active, compare with total, insert" sequence is serialised and
    /// held + confirmed can never exceed totalSeats.
    /// </summary>
    public async Task<Result<HoldCreatedResponse>> CreateHoldAsync(string classId, string studentId, CancellationToken ct)
    {
        var now = Now;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1. Lock the class row (or learn that it does not exist).
        var locked = await db.Classes
            .FromSqlInterpolated($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);
        var schoolClass = locked.SingleOrDefault();
        if (schoolClass is null)
        {
            return Result<HoldCreatedResponse>.Fail(Outcome.ClassNotFound);
        }

        // 2. Persist expiry for this class' stale holds so the partial unique index
        //    and the count below only see truly active rows.
        await db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);

        // 3. One active application / confirmed admission per student per class.
        var alreadyActive = await db.Holds.AnyAsync(
            h => h.ClassId == classId && h.StudentId == studentId &&
                 (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED), ct);
        if (alreadyActive)
        {
            return Result<HoldCreatedResponse>.Fail(Outcome.AlreadyApplied);
        }

        // 4. Capacity check.
        var occupied = await db.Holds.CountAsync(
            h => h.ClassId == classId && (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED), ct);
        if (occupied >= schoolClass.TotalSeats)
        {
            return Result<HoldCreatedResponse>.Fail(Outcome.SeatUnavailable);
        }

        // 5. Reserve.
        var hold = new Hold
        {
            Id = Ids.NewHoldId(),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.HELD,
            CreatedAt = now,
            ExpiresAt = now.Add(options.HoldDuration),
        };
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Should be unreachable thanks to the row lock; kept as a safety net.
            return Result<HoldCreatedResponse>.Fail(Outcome.AlreadyApplied);
        }

        return Result<HoldCreatedResponse>.Success(new HoldCreatedResponse(
            hold.Id, hold.ClassId, hold.StudentId, hold.Status.ToString(), hold.ExpiresAt, now));
    }

    public async Task<HoldResponse?> GetHoldAsync(string holdId, CancellationToken ct)
    {
        var now = Now;
        var hold = await db.Holds.AsNoTracking().Include(h => h.Class)
            .SingleOrDefaultAsync(h => h.Id == holdId, ct);
        return hold is null ? null : ToHoldResponse(hold, now);
    }

    private static HoldResponse ToHoldResponse(Hold hold, DateTime now) => new(
        hold.Id, hold.ClassId, hold.EffectiveStatus(now).ToString(), hold.ExpiresAt,
        hold.Class?.Name ?? string.Empty, hold.StudentId, now);

    /// <summary>
    /// Pay the admission fee. Only the owner may confirm, only while the hold is still HELD and
    /// unexpired. The state change is a single conditional UPDATE, so two simultaneous confirms
    /// (or a confirm racing the expiry sweeper) cannot both succeed; a repeat confirm of an
    /// already CONFIRMED hold is accepted and changes nothing (confirmedSeats grows once).
    /// </summary>
    public async Task<Result<HoldConfirmedResponse>> ConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        var hold = await db.Holds.AsNoTracking().SingleOrDefaultAsync(h => h.Id == holdId, ct);
        if (hold is null) return Result<HoldConfirmedResponse>.Fail(Outcome.HoldNotFound);
        if (hold.StudentId != studentId) return Result<HoldConfirmedResponse>.Fail(Outcome.NotOwner);

        var now = Now;
        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.CONFIRMED:
                return Result<HoldConfirmedResponse>.Success(new HoldConfirmedResponse(hold.Id, "CONFIRMED"));
            case HoldStatus.EXPIRED:
                return Result<HoldConfirmedResponse>.Fail(Outcome.HoldExpired);
            case HoldStatus.CANCELLED:
                return Result<HoldConfirmedResponse>.Fail(Outcome.HoldCancelled);
        }

        var updated = await db.Holds
            .Where(h => h.Id == holdId && h.StudentId == studentId &&
                        h.Status == HoldStatus.HELD && h.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HoldStatus.CONFIRMED)
                .SetProperty(h => h.ConfirmedAt, now), ct);

        if (updated == 1)
        {
            return Result<HoldConfirmedResponse>.Success(new HoldConfirmedResponse(hold.Id, "CONFIRMED"));
        }

        // Lost a race: somebody confirmed/cancelled it, or it expired a moment ago. Re-read to report why.
        var fresh = await db.Holds.AsNoTracking().SingleAsync(h => h.Id == holdId, ct);
        return fresh.EffectiveStatus(Now) switch
        {
            HoldStatus.CONFIRMED => Result<HoldConfirmedResponse>.Success(new HoldConfirmedResponse(hold.Id, "CONFIRMED")),
            HoldStatus.CANCELLED => Result<HoldConfirmedResponse>.Fail(Outcome.HoldCancelled),
            _ => Result<HoldConfirmedResponse>.Fail(Outcome.HoldExpired),
        };
    }

    /// <summary>
    /// Cancel an application. HELD -> CANCELLED frees the seat immediately. Cancelling a hold that is
    /// already EXPIRED or CANCELLED is a no-op success (DELETE is idempotent). A CONFIRMED admission
    /// is not an "application" any more and cannot be cancelled through this endpoint.
    /// </summary>
    public async Task<Result<bool>> CancelAsync(string holdId, string studentId, CancellationToken ct)
    {
        var hold = await db.Holds.AsNoTracking().SingleOrDefaultAsync(h => h.Id == holdId, ct);
        if (hold is null) return Result<bool>.Fail(Outcome.HoldNotFound);
        if (hold.StudentId != studentId) return Result<bool>.Fail(Outcome.NotOwner);

        var now = Now;
        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.CONFIRMED:
                return Result<bool>.Fail(Outcome.HoldConfirmed);
            case HoldStatus.EXPIRED:
            case HoldStatus.CANCELLED:
                return Result<bool>.Success(true);
        }

        await db.Holds
            .Where(h => h.Id == holdId && h.StudentId == studentId && h.Status == HoldStatus.HELD)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HoldStatus.CANCELLED)
                .SetProperty(h => h.CancelledAt, now), ct);

        // If 0 rows were updated the hold was confirmed concurrently; report that honestly.
        var fresh = await db.Holds.AsNoTracking().SingleAsync(h => h.Id == holdId, ct);
        return fresh.Status == HoldStatus.CONFIRMED
            ? Result<bool>.Fail(Outcome.HoldConfirmed)
            : Result<bool>.Success(true);
    }

    public async Task<IReadOnlyList<MyHoldResponse>> GetMyHoldsAsync(string studentId, CancellationToken ct)
    {
        var now = Now;
        var holds = await db.Holds.AsNoTracking()
            .Include(h => h.Class)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return holds.Select(h => new MyHoldResponse(
            h.Id, h.ClassId, h.Class?.Name ?? string.Empty, h.EffectiveStatus(now).ToString(),
            h.ExpiresAt, h.CreatedAt, now)).ToList();
    }

    /// <summary>Persist EXPIRED for every stale HELD row. Returns the number of rows touched.</summary>
    public async Task<int> SweepExpiredAsync(CancellationToken ct)
    {
        var now = Now;
        return await db.Holds
            .Where(h => h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.EXPIRED), ct);
    }
}
