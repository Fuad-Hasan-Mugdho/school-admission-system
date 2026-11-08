namespace SchoolAdmission.Api.Services;

/// <summary>
/// Periodically persists EXPIRED on stale HELD rows. Seat accounting never depends on this
/// (reads and the hold-creation transaction evaluate expiry themselves); it only keeps the stored
/// status truthful for reporting, and it needs no API call from any client.
/// </summary>
public sealed class HoldExpirySweeper(IServiceScopeFactory scopeFactory, HoldOptions options, ILogger<HoldExpirySweeper> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.SweepIntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AdmissionService>();
                var expired = await service.SweepExpiredAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} hold(s).", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Expiry sweep failed; will retry.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
