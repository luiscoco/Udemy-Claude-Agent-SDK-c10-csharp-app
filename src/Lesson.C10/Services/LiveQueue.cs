using Anthropic.Models.Beta.Sessions.Events;
using Lesson.C10.Lessons;

namespace Lesson.C10.Services;

public sealed record QueueReceipt(string CommandId, string UserEventId, string Message, bool QueuedSeen, bool Processed, string Status, string Answer);

public sealed record LiveQueueResult(string Source, string SessionId, Outcome Outcome, string Summary, IReadOnlyList<QueueReceipt> Receipts,
    string? ForeignUserEventId, IReadOnlyList<string> EventTypes, IReadOnlyList<string> Steps, string Scope);

/// The lesson's queue against a real session: one writer sends one message at a time, matches the
/// accepted user event to its command, and advances only after the turn settles on idle/end_turn.
public sealed class LiveQueue(ManagedAgentSettings settings)
{
    public const int MaxMessages = 5;

    public bool Configured => settings.Configured;

    public const string SecondWriterMessage = "Second writer here: please also summarise this conversation.";

    /// `onStep` receives a plain-language explanation of each meaningful moment, as it happens.
    public async Task<LiveQueueResult> Run(IReadOnlyList<string> messages, bool competingWriter, Func<TeachingStep, Task>? onStep = null, CancellationToken cancellationToken = default)
    {
        var stepNumber = 0;
        Task Teach(TeachingStep? step) => step is null || onStep is null ? Task.CompletedTask : onStep(step with { Number = ++stepNumber });

        if (!settings.Configured) throw new InvalidOperationException("Configure the API key, agent ID and environment ID in .env.");
        if (messages.Count is 0 or > MaxMessages) throw new ArgumentException($"The live queue sends between 1 and {MaxMessages} messages.");
        var client = ManagedSessions.Client(settings);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60 * messages.Count));
        string? sessionId = null;
        SessionStream? events = null;
        try
        {
            var session = await ManagedSessions.CreateTextOnly(client, settings,
                "You are the agent in a multi-turn teaching demo about message queues. Reply to each user message in one short sentence. No tools are available.",
                deadline.Token);
            sessionId = session.ID;
            await Teach(Teaching.SessionCreated(session.ID));
            events = new SessionStream(client, session.ID, TimeSpan.FromSeconds(60 * messages.Count), deadline.Token);
            await Teach(Teaching.StreamOpened());

            HashSet<string> ownEventIds = [];
            List<QueueReceipt> receipts = [];
            List<string> eventTypes = [];
            string? foreignUserEventId = null;

            for (var index = 0; index < messages.Count; index++)
            {
                // Exactly one queued message per turn; the server-assigned event ID is the correlation key.
                var commandId = $"cmd-{index + 1}";
                var userEventId = await SendText(client, session.ID, messages[index], deadline.Token);
                ownEventIds.Add(userEventId);
                await Teach(Teaching.Sent(index + 1, commandId, messages[index], userEventId));
                if (competingWriter && index == 0)
                {
                    // A second writer posts into the same conversation. The queue never learns this ID.
                    await SendText(client, session.ID, SecondWriterMessage, deadline.Token);
                    await Teach(Teaching.SecondWriter(SecondWriterMessage));
                }

                var turn = new TurnTracker(userEventId, ownEventIds);
                var thinkingShown = false;
                await foreach (var item in events.ReadAll(deadline.Token))
                {
                    eventTypes.Add(item.Type);
                    var state = turn.Observe(SessionStream.ToSignal(item));
                    // Thinking can arrive as several events per turn; one explanation is enough.
                    if (turn.Last != Observation.Thinking || !thinkingShown)
                        await Teach(Teaching.Describe(turn.Last, turn, index + 1, commandId, index + 1 < messages.Count, turn.ForeignUserEventId));
                    thinkingShown |= turn.Last == Observation.Thinking;
                    if (state != TurnState.Waiting || eventTypes.Count > 500) break;
                }

                receipts.Add(new QueueReceipt(commandId, userEventId, messages[index], turn.QueuedSeen, turn.Processed, turn.State switch
                {
                    TurnState.Settled => "idle/end_turn",
                    // Claude may have answered, but the turn never got an end_turn idle of its own.
                    TurnState.Conflict => turn.Answered ? "answered, not settled: foreign user event" : "blocked: foreign user event",
                    TurnState.Stopped => $"stopped: {turn.StopReason}",
                    _ => "unsettled",
                }, turn.Answer));

                if (turn.State == TurnState.Conflict)
                {
                    foreignUserEventId = turn.ForeignUserEventId;
                    // No interrupt: that would be one more write into a conversation the queue no longer owns.
                    return Report(session.ID, Outcome.Conflict, "Another writer's user event reached the session, so the queue stopped advancing",
                        receipts, foreignUserEventId, eventTypes, "Open one shared stream reader", "Send exactly one queued message", "Observe a foreign user event ID", "Block queue advancement");
                }
                if (turn.State != TurnState.Settled)
                    return Report(session.ID, Outcome.Incomplete, $"Turn {index + 1} did not settle on idle/end_turn ({turn.StopReason ?? "stream ended"})",
                        receipts, null, eventTypes, "Open one shared stream reader", "Send exactly one queued message", "Wait for a settled idle", "Stop: the turn did not settle");
            }

            await Teach(Teaching.Final(messages.Count));
            return Report(session.ID, Outcome.Passed, "Serialized each admitted turn in one live conversation", receipts, null, eventTypes,
                "Open one shared stream reader", "Send exactly one queued message", "Match the accepted user event ID", "Confirm reusable idle before advancing");
        }
        catch (Exception error)
        {
            if (sessionId is not null) await ManagedSessions.TryInterrupt(client, sessionId);
            throw ManagedSessions.Failure(sessionId, error);
        }
        finally
        {
            if (events is not null) await events.DisposeAsync();
        }
    }

    static async Task<string> SendText(Anthropic.AnthropicClient client, string sessionId, string text, CancellationToken cancellationToken)
    {
        var sent = await client.Beta.Sessions.Events.Send(sessionId, new EventSendParams
        {
            Events = [new BetaManagedAgentsUserMessageEventParams { Content = [new BetaManagedAgentsTextBlock { Text = text }] }],
        }, cancellationToken);
        return (sent.Data ?? []).Select(item => item.TryPickBetaManagedAgentsUserMessageEvent(out var user) ? user.ID : null).OfType<string>().SingleOrDefault()
            ?? throw new InvalidOperationException("The send response did not include the accepted user event ID.");
    }

    static LiveQueueResult Report(string sessionId, Outcome outcome, string summary, IReadOnlyList<QueueReceipt> receipts, string? foreignUserEventId,
        IReadOnlyList<string> eventTypes, params string[] steps) =>
        new("live-session-queue", sessionId, outcome, summary, receipts, foreignUserEventId, eventTypes, steps,
            "A real Managed Agents session driven by this host. It shows ordering, attribution and settled completion for one in-process writer; it is not a durable distributed queue.");
}
