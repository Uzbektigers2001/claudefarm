using System.Diagnostics;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Services;

/// <summary>
/// ArchitectPlan dan real .NET project yaratadi
/// </summary>
public sealed class ProjectBuilderService
{
    private readonly ILogger<ProjectBuilderService> _logger;

    public ProjectBuilderService(ILogger<ProjectBuilderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// dotnet CLI mavjudligini tekshirish
    /// </summary>
    public async Task<bool> IsDotnetInstalledAsync()
    {
        try
        {
            var result = await RunCommandAsync("dotnet", "--version", "/tmp");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Architect plan dan to'liq .NET project yaratish
    /// </summary>
    public async Task<string> BuildProjectAsync(ArchitectPlan plan, string workDir, CancellationToken ct = default)
    {
        _logger.LogInformation("🏗️ Project yaratish boshlandi: {ProjectName} @ {WorkDir}", plan.ProjectName, workDir);

        // 1. Work directory yaratish
        Directory.CreateDirectory(workDir);

        // 2. Solution yaratish
        await RunCommandAsync("dotnet", $"new sln -n {plan.ProjectName}", workDir, ct);
        _logger.LogInformation("✅ Solution yaratildi: {SolutionFile}", plan.SolutionFile);

        // 3. Har bir project yaratish
        foreach (var project in plan.Projects)
        {
            var projectDir = Path.Combine(workDir, project.Path);
            Directory.CreateDirectory(projectDir);

            // dotnet new {type} -n {name}
            await RunCommandAsync("dotnet", $"new {project.Type} -n {project.Name} -o {project.Path}", workDir, ct);
            _logger.LogInformation("✅ Project yaratildi: {ProjectName} ({Type})", project.Name, project.Type);

            // Solution ga qo'shish
            await RunCommandAsync("dotnet", $"sln add {project.Path}/{project.Name}.csproj", workDir, ct);
        }

        // 4. Project references qo'shish
        foreach (var project in plan.Projects)
        {
            foreach (var refProjectName in project.References)
            {
                var refProject = plan.Projects.FirstOrDefault(p => p.Name == refProjectName);
                if (refProject != null)
                {
                    await RunCommandAsync("dotnet",
                        $"add {project.Path}/{project.Name}.csproj reference {refProject.Path}/{refProject.Name}.csproj",
                        workDir, ct);
                    _logger.LogInformation("✅ Reference qo'shildi: {Project} -> {RefProject}", project.Name, refProjectName);
                }
            }
        }

        // 5. NuGet packages qo'shish
        foreach (var (projectName, packages) in plan.NugetPackages)
        {
            var project = plan.Projects.FirstOrDefault(p => p.Name == projectName);
            if (project == null) continue;

            foreach (var package in packages)
            {
                await RunCommandAsync("dotnet",
                    $"add {project.Path}/{project.Name}.csproj package {package}",
                    workDir, ct);
                _logger.LogInformation("✅ Package qo'shildi: {Package} -> {Project}", package, projectName);
            }
        }

        _logger.LogInformation("🎉 Project yaratildi: {WorkDir}", workDir);
        return workDir;
    }

    /// <summary>
    /// Bash command ishga tushirish
    /// </summary>
    private async Task<(int ExitCode, string Output)> RunCommandAsync(
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var result = output + error;
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Command failed: {Command} {Args}\n{Output}", command, arguments, result);
        }

        return (process.ExitCode, result);
    }
}
