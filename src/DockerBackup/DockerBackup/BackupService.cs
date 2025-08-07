using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCrontab;

namespace DockerBackup;

public class BackupService : CronScheduledBackgroundService
{
    private readonly ILogger<BackupService> logger;
    private readonly DockerClient dockerClient;
    
    public BackupService(
        ILogger<BackupService> logger,
        DockerClient dockerClient,
        IOptions<BackupServiceOptions> options) 
        : base(options.Value.Cron, logger)
    {
        this.logger = logger;
        this.dockerClient = dockerClient;
    }

    public override async Task ScheduledExecutionAsync(CancellationToken cancellationToken)
    {
        await foreach (DockerContainer container in dockerClient.GetContainersAsync(cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            if (container.GetValueAsBool("dk.vsnt.backup.postgres.enabled") is not true || container.IsRunning is not true)
            {
                continue;
            }
                
            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                string command = $"pg_dumpall --clean --if-exists --username postgres | gzip > /var/backups/backup-{timestamp}.sql.gz";
                    
                logger.LogInformation("Backing up {ContainerId}", container.Id);
                    
                await dockerClient.ExecuteCommandAsync(container, command);
                    
                logger.LogInformation("Backup completed for {ContainerId}", container.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to backup {ContainerId}", container.Id);
            }
        }
    }
}