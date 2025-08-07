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
        IEnumerable<DockerContainer> containers = dockerClient.GetContainersAsync(cancellationToken)
            .ToBlockingEnumerable(cancellationToken)
            .Where(container => container.IsRunning ?? false)
            .Where(container => container.Image.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            .Where(container => container.GetValueAsBool("dk.vsnt.backup.postgres.enabled") ?? false);
        
        foreach (DockerContainer container in containers)
        {
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