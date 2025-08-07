using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace DockerBackup;

public abstract class CronScheduledBackgroundService : BackgroundService
{
    private readonly ILogger? logger;
    private readonly CrontabSchedule schedule;

    public CronScheduledBackgroundService(
        string cronPattern, 
        ILogger? logger = null)
    {
        this.schedule = CrontabSchedule.Parse(cronPattern);
        this.logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime nextOccurrence = schedule.GetNextOccurrence(utcNow);
                
                this.logger?.LogInformation("Next scheduled execution at: {NextOccurrence}", nextOccurrence);
                await Task.Delay(nextOccurrence - utcNow, cancellationToken)
                    .ConfigureAwait(false);
                
                this.logger?.LogInformation("Running scheduled execution at: {NextOccurrence}", nextOccurrence);
                
                await ScheduledExecutionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            this.logger?.LogInformation("Scheduled background service is stopping.");
        }
    }
    
    public abstract Task ScheduledExecutionAsync(CancellationToken cancellationToken);
}