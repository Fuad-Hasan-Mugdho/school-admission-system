namespace SchoolAdmission.Api.Services;

public sealed class HoldOptions
{
    /// <summary>How long a HELD seat stays reserved. Env: HOLD_DURATION_SECONDS (default 600).</summary>
    public int HoldDurationSeconds { get; set; } = 600;

    /// <summary>How often the sweeper persists EXPIRED. Env: HOLD_SWEEP_INTERVAL_SECONDS (default 1).</summary>
    public double SweepIntervalSeconds { get; set; } = 1;

    public TimeSpan HoldDuration => TimeSpan.FromSeconds(HoldDurationSeconds);

    public static HoldOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new HoldOptions();

        var duration = configuration["HOLD_DURATION_SECONDS"];
        if (!string.IsNullOrWhiteSpace(duration))
        {
            if (!int.TryParse(duration, out var seconds) || seconds <= 0)
            {
                throw new InvalidOperationException(
                    $"HOLD_DURATION_SECONDS must be a positive integer, got '{duration}'.");
            }
            options.HoldDurationSeconds = seconds;
        }

        var sweep = configuration["HOLD_SWEEP_INTERVAL_SECONDS"];
        if (!string.IsNullOrWhiteSpace(sweep) &&
            double.TryParse(sweep, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var interval) && interval > 0)
        {
            options.SweepIntervalSeconds = interval;
        }

        return options;
    }
}
