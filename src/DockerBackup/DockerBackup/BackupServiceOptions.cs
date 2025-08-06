namespace DockerBackup;

public class BackupServiceOptions
{
    /// <summary>
    /// Gets or sets the interval for scheduled backups.
    /// </summary>
    public string Cron { get; set; } = "*/5 * * * *"; // Default to every 5 minutes
}