using System.Net.Http.Json;
using System.Text.Json;
using AgentFarm.Agents.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentFarm.Agents.Services;

/// <summary>
/// OmniRoute (OpenAI format) va to'g'ridan Anthropic API ni qo'llab-quvvatlaydi.
/// OmniRoute: http://localhost:20128/v1  → OpenAI format
/// Anthropic:  https://api.anthropic.com/v1 → Anthropic format
/// </summary>
public sealed class ClaudeApiClient
{
    private readonly HttpClient               _http;
    private readonly AnthropicOptions         _options;
    private readonly ILogger<ClaudeApiClient> _logger;

    public ClaudeApiClient(
        HttpClient                 http,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeApiClient>   logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        if (_options.UseOmniRoute)
        {
            // OmniRoute — OpenAI formatida ishlaydi
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
            _logger.LogInformation("OmniRoute rejimi: {BaseUrl}", _options.BaseUrl);
        }
        else
        {
            // To'g'ridan Anthropic API
            _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _logger.LogInformation("Anthropic API rejimi: {BaseUrl}", _options.BaseUrl);
        }
    }

    public async Task<(string Content, int TokensUsed)> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        return _options.UseOmniRoute
            ? await CompleteOpenAiFormatAsync(systemPrompt, userMessage, ct)
            : await CompleteAnthropicFormatAsync(systemPrompt, userMessage, ct);
    }

    /// <summary>OmniRoute uchun — OpenAI /v1/chat/completions formati.</summary>
    private async Task<(string Content, int TokensUsed)> CompleteOpenAiFormatAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        var body = new
        {
            model      = _options.Model,
            max_tokens = _options.MaxTokens,
            messages   = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            }
        };

        _logger.LogDebug("OmniRoute ga so'rov. Model={Model}", _options.Model);

        var response = await _http.PostAsJsonAsync("chat/completions", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OmniRoute xato: {Status} — {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var content    = json.GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString() ?? "";

        var inputTok  = json.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
        var outputTok = json.GetProperty("usage").GetProperty("completion_tokens").GetInt32();

        _logger.LogDebug("OmniRoute javob berdi. Tokenlar={Total}", inputTok + outputTok);

        return (content, inputTok + outputTok);
    }

    /// <summary>To'g'ridan Anthropic API uchun — /v1/messages formati.</summary>
    private async Task<(string Content, int TokensUsed)> CompleteAnthropicFormatAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
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

        _logger.LogDebug("Anthropic API ga so'rov. Model={Model}", _options.Model);

        var response = await _http.PostAsJsonAsync("messages", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic xato: {Status} — {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var content   = json.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        var inputTok  = json.GetProperty("usage").GetProperty("input_tokens").GetInt32();
        var outputTok = json.GetProperty("usage").GetProperty("output_tokens").GetInt32();

        _logger.LogDebug("Anthropic javob berdi. Tokenlar={Total}", inputTok + outputTok);

        return (content, inputTok + outputTok);
    }
}
