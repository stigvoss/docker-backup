using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCrontab;

namespace DockerBackup;

public class BackupService : BackgroundService
{
    private readonly ILogger<BackupService> logger;
    private readonly DockerClient dockerClient;
    private readonly BackupServiceOptions options;
    
    public BackupService(
        ILogger<BackupService> logger,
        DockerClient dockerClient,
        IOptions<BackupServiceOptions> options)
    {
        this.logger = logger;
        this.dockerClient = dockerClient;
        this.options = options.Value;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Backup scheduler service is starting.");
        
        try
        {
            CrontabSchedule schedule = CrontabSchedule.Parse(options.Cron);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime nextOccurrence = schedule.GetNextOccurrence(utcNow);
                
                this.logger.LogInformation("Next scheduled backup at: {NextOccurrence}", nextOccurrence);
                await Task.Delay(nextOccurrence - utcNow, stoppingToken);
                
                this.logger.LogInformation("Running scheduled backup at: {Time}", DateTimeOffset.Now);
                
                await PerformBackupsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("Backup scheduler service is stopping.");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred in the backup scheduler service.");
            throw;
        }
    }
    
    private async Task PerformBackupsAsync()
    {
        try
        {
            await foreach (DockerContainer container in dockerClient.GetContainersAsync())
            {
                if (container.IsBackupEnabled is not true || container.IsRunning is not true)
                {
                    continue;
                }
                
                try
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    string command = $"pg_dumpall --clean --if-exists --username postgres | gzip > /var/backups/backup-{timestamp}.sql.gz";
                    
                    logger.LogInformation("Backing up {ContainerId}", container.Id);
                    
                    string? result = await dockerClient.ExecuteCommandAsync(container, command);
                    
                    logger.LogInformation("Backup completed for {ContainerId}", container.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to backup {ContainerId}", container.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during backup operation");
        }
    }
}