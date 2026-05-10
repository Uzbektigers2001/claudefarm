namespace AgentFarm.Agents.Options;

public sealed class AnthropicOptions
{
    public string ApiKey   { get; init; } = string.Empty;
    public string Model    { get; init; } = "claude-sonnet-4-20250514";
    public int    MaxTokens { get; init; } = 4096;
}
