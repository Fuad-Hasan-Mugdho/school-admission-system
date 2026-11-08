using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Boots the real API (Program.cs, real PostgreSQL) in-process. Several factories can share one
/// database, which is how the "two backend processes" test is built.
/// </summary>
/// <remarks>
/// Settings are passed with <see cref="IWebHostBuilder.UseSetting"/> rather than
/// ConfigureAppConfiguration: Program.cs reads DATABASE_URL / HOLD_DURATION_SECONDS before
/// <c>builder.Build()</c>, and only UseSetting values (forwarded as command-line args) are visible that early.
/// </remarks>
public sealed class ApiFactory(string connectionString, int holdDurationSeconds = 600, double sweepIntervalSeconds = 0.25)
    : WebApplicationFactory<Program>
{
    /// <summary>
    /// Each factory gets its own connection pool. Many factories are created during a run, so cap the
    /// pool (requests simply queue for a connection) to stay well under PostgreSQL's max_connections.
    /// </summary>
    private const int MaxPoolSizePerFactory = 16;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DATABASE_URL", WithPoolLimit(connectionString));
        builder.UseSetting("HOLD_DURATION_SECONDS", holdDurationSeconds.ToString());
        builder.UseSetting("HOLD_SWEEP_INTERVAL_SECONDS",
            sweepIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // Idle pooled connections would otherwise linger for minutes after the host is gone.
        NpgsqlConnection.ClearAllPools();
    }

    private static string WithPoolLimit(string raw)
    {
        var normalized = SchoolAdmission.Api.Data.ConnectionStrings.Normalize(raw);
        var builder = new NpgsqlConnectionStringBuilder(normalized);
        builder.MaxPoolSize = Math.Min(builder.MaxPoolSize, MaxPoolSizePerFactory);
        return builder.ConnectionString;
    }
}
