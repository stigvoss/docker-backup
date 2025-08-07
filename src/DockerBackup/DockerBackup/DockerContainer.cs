namespace DockerBackup;

public record DockerContainer(
    string Id,
    Dictionary<string, string> Labels,
    string State)
{
    public string? GetValueAsString(string key) => Labels.GetValueOrDefault(key);
    
    public int? GetValueAsInt(string key) => Labels.GetValueOrDefault(key) 
        is string value && int.TryParse(value, out int result) 
            ? result 
            : null;
    
    public bool? GetValueAsBool(string key) => Labels.GetValueOrDefault(key) 
        is string value && bool.TryParse(value, out bool result) 
            ? result 
            : null;

    public bool? IsRunning 
        => string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);
}