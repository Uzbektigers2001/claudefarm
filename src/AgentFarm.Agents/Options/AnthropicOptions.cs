namespace AgentFarm.Agents.Options;

public sealed class AnthropicOptions
{
    public string ApiKey    { get; init; } = string.Empty;
    public string Model     { get; init; } = "kr/claude-sonnet-4.5";
    public int    MaxTokens { get; init; } = 1500;

    /// <summary>
    /// OmniRoute ishlatilsa: http://localhost:20128/v1
    /// To'g'ridan Claude API: https://api.anthropic.com/v1
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:20128/v1";

    /// <summary>OmniRoute ishlatilayaptimi?</summary>
    public bool UseOmniRoute => BaseUrl.Contains("localhost") || BaseUrl.Contains("omniroute");
}
