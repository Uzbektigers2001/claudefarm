using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentFarm.Agents.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentFarm.Agents.Services;

public sealed class ClaudeApiClient
{
    private readonly HttpClient               _http;
    private readonly AnthropicOptions         _options;
    private readonly ILogger<ClaudeApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower
    };

    public ClaudeApiClient(
        HttpClient                 http,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeApiClient>   logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;

        if (_options.UseOmniRoute)
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
            _logger.LogInformation("OmniRoute rejimi: {BaseUrl}", _options.BaseUrl);
        }
        else
        {
            _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _logger.LogInformation("Anthropic API rejimi: {BaseUrl}", _options.BaseUrl);
        }
    }

    public async Task<(string Content, int TokensUsed)> CompleteAsync(
        string systemPrompt,
        string userMessage,
        int? maxTokens = null,
        bool jsonMode  = false,
        CancellationToken ct = default)
    {
        return _options.UseOmniRoute
            ? await CompleteOpenAiFormatAsync(systemPrompt, userMessage, maxTokens, jsonMode, ct)
            : await CompleteAnthropicFormatAsync(systemPrompt, userMessage, maxTokens, ct);
    }

    private async Task<(string Content, int TokensUsed)> CompleteOpenAiFormatAsync(
        string systemPrompt,
        string userMessage,
        int? maxTokens,
        bool jsonMode,
        CancellationToken ct)
    {
        var url = _options.BaseUrl.TrimEnd('/') + "/chat/completions";

        var combinedMessage = $"{systemPrompt}\n\n---\n\n{userMessage}";

        var body = new
        {
            model           = _options.Model,
            max_tokens      = maxTokens ?? _options.MaxTokens,
            stream          = false,
            response_format = jsonMode ? new { type = "json_object" } : null,
            messages        = new[]
            {
                new { role = "user", content = combinedMessage }
            }
        };

        _logger.LogDebug("OmniRoute: {Url}, Model={Model}", url, _options.Model);

        _logger.LogInformation("=== REQUEST TO OMNIROUTE ===\n{Json}",
            JsonSerializer.Serialize(body,
                new JsonSerializerOptions { WriteIndented = true }));

        using var reqContent = JsonContent.Create(body, options: JsonOpts);
        var response = await _http.PostAsync(url, reqContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OmniRoute xato {Status}: {Error}", response.StatusCode, error);
            throw new HttpRequestException($"OmniRoute: {response.StatusCode} — {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var text      = json.GetProperty("choices")[0]
                           .GetProperty("message")
                           .GetProperty("content")
                           .GetString() ?? "";
        var inputTok  = json.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
        var outputTok = json.GetProperty("usage").GetProperty("completion_tokens").GetInt32();

        _logger.LogDebug("OmniRoute javob. Tokenlar={Total}", inputTok + outputTok);
        return (text, inputTok + outputTok);
    }

    private async Task<(string Content, int TokensUsed)> CompleteAnthropicFormatAsync(
        string systemPrompt,
        string userMessage,
        int? maxTokens,
        CancellationToken ct)
    {
        var url = _options.BaseUrl.TrimEnd('/') + "/messages";

        var body = new
        {
            model      = _options.Model,
            max_tokens = maxTokens ?? _options.MaxTokens,
            stream     = false,
            system     = systemPrompt,
            messages   = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        _logger.LogDebug("Anthropic: {Url}, Model={Model}", url, _options.Model);

        using var reqContent = JsonContent.Create(body, options: JsonOpts);
        var response = await _http.PostAsync(url, reqContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic xato {Status}: {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Anthropic: {response.StatusCode} — {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var text      = json.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        var inputTok  = json.GetProperty("usage").GetProperty("input_tokens").GetInt32();
        var outputTok = json.GetProperty("usage").GetProperty("output_tokens").GetInt32();

        _logger.LogDebug("Anthropic javob. Tokenlar={Total}", inputTok + outputTok);
        return (text, inputTok + outputTok);
    }
}
