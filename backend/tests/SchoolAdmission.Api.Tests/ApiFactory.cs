using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Boots the real API (Program.cs, real PostgreSQL) in-process. Several factories can share one
/// database, which is how the "two backend processes" test is built.
/// </summary>
public sealed class ApiFactory(string connectionString, int holdDurationSeconds = 600, double sweepIntervalSeconds = 0.25)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = connectionString,
                ["HOLD_DURATION_SECONDS"] = holdDurationSeconds.ToString(),
                ["HOLD_SWEEP_INTERVAL_SECONDS"] = sweepIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        });
    }
}
