using Anthropic;
using Anthropic.Models.Beta.Agents;
using Anthropic.Models.Beta.Environments;

namespace Lesson.C10.Services;

/// Optional one-time provisioning (`dotnet run -- setup`). Reuses IDs already present in .env.
public static class ResourceSetup
{
    public static async Task Run(string envPath)
    {
        if (!ManagedAgentSettings.Valid(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            throw new InvalidOperationException("Set your own ANTHROPIC_API_KEY in .env first.");
        var client = new AnthropicClient { MaxRetries = 0 };

        if (!ManagedAgentSettings.Valid(Environment.GetEnvironmentVariable("CLAUDE_MANAGED_ENVIRONMENT_ID")))
        {
            var environment = await client.Beta.Environments.Create(new EnvironmentCreateParams
            {
                Name = "C10 student environment",
                Config = new BetaCloudConfigParams { Networking = new BetaLimitedNetworkParams() },
            });
            // Save immediately so a later agent failure does not lose the environment ID.
            DotEnv.Save(envPath, "CLAUDE_MANAGED_ENVIRONMENT_ID", environment.ID);
        }

        if (!ManagedAgentSettings.Valid(Environment.GetEnvironmentVariable("CLAUDE_MANAGED_AGENT_ID")))
        {
            var agent = await client.Beta.Agents.Create(new AgentCreateParams
            {
                Name = "C10 text-only student agent",
                Model = new BetaManagedAgentsModelConfigParams { ID = Environment.GetEnvironmentVariable("CLAUDE_MODEL") is { Length: > 0 } model ? model : "claude-opus-5-5" },
                System = "Answer the student's question clearly and briefly.",
                Tools = [], McpServers = [], Skills = [],
            });
            DotEnv.Save(envPath, "CLAUDE_MANAGED_AGENT_ID", agent.ID);
        }

        Console.WriteLine("Prerequisite IDs are saved in your local .env. Existing configured IDs were reused.");
    }
}
