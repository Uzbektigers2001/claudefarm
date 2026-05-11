using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Agent response dan kodni ajratib faylga yozadi va build qiladi
/// </summary>
public sealed class CodeWriterService
{
    private readonly ILogger<CodeWriterService> _logger;

    public CodeWriterService(ILogger<CodeWriterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Agent response dan faqat kodni ajratib oladi (markdown code block dan)
    /// </summary>
    public string ExtractCode(string agentResponse)
    {
        if (string.IsNullOrWhiteSpace(agentResponse))
            return string.Empty;

        // Markdown code block ni topish: ```csharp ... ``` yoki ```json ... ```
        var codeBlockPattern = @"```(?:csharp|cs|json|yaml|yml|sql|dockerfile)?\s*\n(.*?)\n```";
        var match = Regex.Match(agentResponse, codeBlockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Agar code block yo'q bo'lsa, butun contentni qaytarish
        return agentResponse.Trim();
    }

    /// <summary>
    /// Faylni to'g'ri path ga yozish
    /// </summary>
    public async Task WriteFileAsync(string workDir, string filePath, string code, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(workDir, filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, code, ct);
        _logger.LogInformation("📝 Fayl yozildi: {FilePath}", filePath);
    }

    /// <summary>
    /// Project ni build qilish (dotnet build)
    /// </summary>
    public async Task<BuildResult> BuildAsync(string workDir, string projectPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var fullProjectPath = Path.Combine(workDir, projectPath);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{fullProjectPath}\" --nologo --verbosity quiet",
                WorkingDirectory = workDir,
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
            sw.Stop();

            var fullOutput = output + error;
            var success = process.ExitCode == 0;

            if (success)
            {
                _logger.LogInformation("✅ Build muvaffaqiyatli: {ProjectPath}", projectPath);
                return new BuildResult
                {
                    Success = true,
                    Output = fullOutput,
                    Duration = sw.Elapsed
                };
            }
            else
            {
                var errors = ExtractBuildErrors(fullOutput);
                _logger.LogWarning("❌ Build xatosi: {ProjectPath}\n{Errors}", projectPath, string.Join("\n", errors));
                return new BuildResult
                {
                    Success = false,
                    Errors = errors,
                    Output = fullOutput,
                    Duration = sw.Elapsed
                };
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Build exception: {ProjectPath}", projectPath);
            return new BuildResult
            {
                Success = false,
                Errors = new List<string> { ex.Message },
                Output = ex.ToString(),
                Duration = sw.Elapsed
            };
        }
    }

    /// <summary>
    /// dotnet build output dan xatolarni ajratib olish
    /// </summary>
    private List<string> ExtractBuildErrors(string output)
    {
        var errors = new List<string>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // "error CS1234:" yoki ": error" ni qidirish
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(line.Trim());
            }
        }

        // Agar aniq xato topilmasa, butun outputni qaytarish
        if (errors.Count == 0)
        {
            errors.Add(output.Length > 500 ? output[..500] + "..." : output);
        }

        return errors;
    }
}
