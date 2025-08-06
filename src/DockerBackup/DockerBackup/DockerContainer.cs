namespace DockerBackup;

public record DockerContainer(
    string Id,
    Dictionary<string, string> Labels,
    string State)
{
    public bool? IsBackupEnabled 
        => string.Equals(
            Labels.GetValueOrDefault("dk.vsnt.backup.enabled"), 
            "true", 
            StringComparison.OrdinalIgnoreCase);
    
    public bool? IsRunning 
        => string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);
}