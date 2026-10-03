using Commerce.Application.Auth;

namespace Commerce.Api.Auth;

/// <summary>Closes the sessions of the pages that left and did not come back.</summary>
public sealed class PendingLogoutWorker(SessionClosing closing, ILogger<PendingLogoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await closing.CloseDueAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not close the pending sessions");
            }
        }
    }
}
