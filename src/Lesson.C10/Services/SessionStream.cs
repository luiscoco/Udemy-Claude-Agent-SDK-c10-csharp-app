using System.Threading.Channels;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Sessions;
using Anthropic.Models.Beta.Sessions.Events;

namespace Lesson.C10.Services;

/// Session plumbing shared by the live commentary and the live queue.
public static class ManagedSessions
{
    public static AnthropicClient Client(ManagedAgentSettings settings) =>
        new() { ApiKey = settings.ApiKey, MaxRetries = 0, Timeout = TimeSpan.FromSeconds(15) };

    /// A session on the configured agent with tools, MCP servers and skills removed, capped at 10 USD.
    public static Task<BetaManagedAgentsSession> CreateTextOnly(AnthropicClient client, ManagedAgentSettings settings, string system, CancellationToken cancellationToken) =>
        client.Beta.Sessions.Create(new SessionCreateParams
        {
            Agent = new BetaManagedAgentsAgentWithOverridesParams
            {
                ID = settings.AgentId!,
                System = system,
                Tools = [], McpServers = [], Skills = [],
            },
            EnvironmentID = settings.EnvironmentId!,
            Budget = new BetaManagedAgentsBudgetLimit { MaxListCost = new() { Amount = "10", Currency = "USD" } },
        }, cancellationToken);

    public static async Task TryInterrupt(AnthropicClient client, string sessionId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.Beta.Sessions.Events.Send(sessionId, new EventSendParams { Events = [new BetaManagedAgentsUserInterruptEventParams()] }, timeout.Token);
        }
        catch { /* The caller reports the stop as unconfirmed. */ }
    }

    public static InvalidOperationException Failure(string? sessionId, Exception error) =>
        new($"Live observation failed{(sessionId is not null ? $" for {sessionId}; stop unconfirmed" : " before a session ID was received; creation uncertain")}: {Describe(error)}", error);

    /// A student-readable cause, most specific first. Errors from the stream reader can arrive wrapped.
    public static string Describe(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case AnthropicUnauthorizedException: return "the API key was rejected (401). Check ANTHROPIC_API_KEY in .env and restart the app.";
                case AnthropicForbiddenException: return "this API key is not allowed to use Managed Agents (403).";
                case AnthropicNotFoundException: return "the agent or environment was not found (404). Check the IDs in .env, or run the setup command again.";
                case AnthropicRateLimitException: return "the API is rate limiting this key (429). Wait a minute, then try again.";
                case Anthropic5xxException: return "the API had a temporary server problem (5xx). Try again in a moment.";
                case AnthropicBadRequestException: return $"the API rejected the request (400): {current.Message}";
                case AnthropicIOException: return "the API could not be reached. Check your network connection.";
                case AnthropicApiException: return current.Message;
                case OperationCanceledException: return "the session did not finish before the time limit.";
            }
        }
        return error.Message;
    }
}

/// Opens a session's event stream before anything is sent. SSE has no replay, and the SDK stream is a
/// lazy IAsyncEnumerable, so a background reader starts the request and buffers every event.
public sealed class SessionStream : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly Task _reader;
    private readonly Channel<BetaManagedAgentsStreamSessionEvents> _events = Channel.CreateUnbounded<BetaManagedAgentsStreamSessionEvents>();

    public SessionStream(AnthropicClient client, string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var streamClient = client.WithOptions(options => options with { Timeout = timeout });
        _reader = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in streamClient.Beta.Sessions.Events.StreamStreaming(sessionId, new EventStreamParams(), _stop.Token))
                    await _events.Writer.WriteAsync(item, _stop.Token);
                _events.Writer.TryComplete();
            }
            catch (Exception error) { _events.Writer.TryComplete(error); }
        });
    }

    public IAsyncEnumerable<BetaManagedAgentsStreamSessionEvents> ReadAll(CancellationToken cancellationToken) => _events.Reader.ReadAllAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _reader.ContinueWith(_ => { }, TaskScheduler.Default);
        _stop.Dispose();
    }

    /// Maps the SDK union to the few fields the queue tracker reads.
    public static SessionSignal ToSignal(BetaManagedAgentsStreamSessionEvents item)
    {
        if (item.TryPickUserMessageEvent(out var user)) return new(item.Type, user.ID, Processed: user.ProcessedAt is not null);
        if (item.TryPickAgentMessageEvent(out var message)) return new(item.Type, message.ID, Text: AnswerText(message));
        if (item.TryPickSessionStatusIdleEvent(out var idle)) return new(item.Type, idle.ID, StopReason: idle.StopReason.Type);
        return new(item.Type);
    }

    public static string AnswerText(BetaManagedAgentsAgentMessageEvent message) =>
        string.Join("\n", message.Content.Select(block => block.TryPickBetaManagedAgentsTextBlock(out var text) ? text.Text : null).OfType<string>());
}
