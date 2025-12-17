using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ESB.DeveloperExperience;

public class EsbService(
    ILogger<EsbService> logger,
    IEsbWorker esbWorker
) : BackgroundService
{
    private const long MillisecondThreshold = 5000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(MillisecondThreshold));

        try
        {
            do
            {
                await esbWorker.ProcessSession(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The service is stopping, we can ignore this exception.
        }

        logger.LogInformation("{Service} is stopping...", nameof(EsbService));
    }
}
