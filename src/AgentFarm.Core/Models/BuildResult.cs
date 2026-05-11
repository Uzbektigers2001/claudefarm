namespace AgentFarm.Core.Models;

/// <summary>
/// dotnet build natijasi
/// </summary>
public class BuildResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Output { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}
