namespace AgentFarm.Core.Models;

/// <summary>
/// Architect agent tomonidan yaratilgan loyiha rejasi
/// </summary>
public class ArchitectPlan
{
    public string ProjectName { get; set; } = string.Empty;
    public string SolutionFile { get; set; } = string.Empty;
    public List<ArchitectProject> Projects { get; set; } = new();
    public List<ArchitectFile> Files { get; set; } = new();
    public Dictionary<string, List<string>> NugetPackages { get; set; } = new();
}

/// <summary>
/// .NET project (classlib, webapi, console)
/// </summary>
public class ArchitectProject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // classlib, webapi, console, xunit
    public List<string> References { get; set; } = new();
}

/// <summary>
/// Agent tomonidan yoziladigan fayl
/// </summary>
public class ArchitectFile
{
    public string Path { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string AssignTo { get; set; } = string.Empty;
    public int Instance { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
}
