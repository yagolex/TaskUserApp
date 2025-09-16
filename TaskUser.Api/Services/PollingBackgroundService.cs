namespace TaskUser.Api.Services
{
    public class PollingBackgroundService(ILogger<PollingBackgroundService> logger, IServiceScopeFactory scopeFactory) : BackgroundService
    {
        private readonly PeriodicTimer _timer = new(TimeSpan.FromMinutes(2));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    logger.LogInformation("Running job at: {time}", DateTimeOffset.Now);
                    
                    using var scope = scopeFactory.CreateScope();
                    var reassigner = scope.ServiceProvider.GetRequiredService<ITaskReassigner>();
                    await reassigner.ReassignAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { logger.LogError(ex, "Reassign round failed"); }
            }
        }
    }
}