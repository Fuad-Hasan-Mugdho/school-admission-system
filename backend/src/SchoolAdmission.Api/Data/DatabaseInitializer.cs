using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Applies EF Core migrations at startup. PostgreSQL may still be booting when the API
/// starts (even with compose health checks), so we retry; and because several API
/// processes may start at once (bonus: two backends on one database) we serialise the
/// migration with a session-level advisory lock.
/// </summary>
public static class DatabaseInitializer
{
    private const long MigrationLockKey = 727_272_001;

    public static async Task MigrateWithRetryAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            attempt++;
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

                await db.Database.OpenConnectionAsync(ct);
                try
                {
                    await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})", ct);
                    try
                    {
                        await db.Database.MigrateAsync(ct);
                    }
                    finally
                    {
                        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", ct);
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                logger.LogInformation("Database schema is up to date (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}. Retrying in {Delay}s...",
                    attempt, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
            }
        }
    }
}
