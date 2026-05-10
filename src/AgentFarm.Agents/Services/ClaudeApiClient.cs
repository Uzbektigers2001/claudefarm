using System.Net.Http.Json;
using System.Text.Json;
using AgentFarm.Agents.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Anthropic Claude API bilan muloqot qiladi.
/// </summary>
public sealed class ClaudeApiClient
{
    private readonly HttpClient              _http;
    private readonly AnthropicOptions        _options;
    private readonly ILogger<ClaudeApiClient> _logger;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public ClaudeApiClient(
        HttpClient                   http,
        IOptions<AnthropicOptions>   options,
        ILogger<ClaudeApiClient>     logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;

        _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    /// <summary>
    /// Agentga so'rov yuboradi va javob matnini qaytaradi.
    /// </summary>
    public async Task<(string Content, int TokensUsed)> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var body = new
        {
            model      = _options.Model,
            max_tokens = _options.MaxTokens,
            system     = systemPrompt,
            messages   = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        _logger.LogDebug("Claude API ga so'rov. Model={Model}", _options.Model);

        var response = await _http.PostAsJsonAsync(ApiUrl, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var content    = json.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        var inputTok   = json.GetProperty("usage").GetProperty("input_tokens").GetInt32();
        var outputTok  = json.GetProperty("usage").GetProperty("output_tokens").GetInt32();

        _logger.LogDebug("Claude javob berdi. Tokenlar={Total}", inputTok + outputTok);

        return (content, inputTok + outputTok);
    }
}
