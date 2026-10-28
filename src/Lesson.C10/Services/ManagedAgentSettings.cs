namespace Lesson.C10.Services;

public sealed record ManagedAgentSettings(string? ApiKey, string? AgentId, string? EnvironmentId)
{
    public static ManagedAgentSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
        Environment.GetEnvironmentVariable("CLAUDE_MANAGED_AGENT_ID"),
        Environment.GetEnvironmentVariable("CLAUDE_MANAGED_ENVIRONMENT_ID"));

    public bool Configured => new[] { ApiKey, AgentId, EnvironmentId }.All(Valid);

    public static bool Valid(string? value) => !string.IsNullOrEmpty(value) && !value.StartsWith("your_");
}
