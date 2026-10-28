namespace Lesson.C10.Services;

/// The parts of a session stream event the queue needs. Keeping the SDK union out of the
/// tracker lets the tests replay recorded event sequences without calling the API.
public sealed record SessionSignal(string Type, string? Id = null, bool Processed = false, string? StopReason = null, string? Text = null);

public enum TurnState { Waiting, Settled, Conflict, Stopped }

/// What one signal meant for the current turn. The timeline explains each of these to students.
public enum Observation { Ignored, OwnQueued, OwnProcessed, EarlierOwnEvent, Foreign, Thinking, Answer, StaleIdle, TransientIdle, Settled, Stopped }

/// Follows one queued turn: the accepted user event must be ours, and the turn only settles on an
/// idle with end_turn that arrives after the agent has processed our message.
public sealed class TurnTracker(string userEventId, IReadOnlySet<string> ownEventIds)
{
    public string UserEventId { get; } = userEventId;
    public bool QueuedSeen { get; private set; }
    public bool Processed { get; private set; }
    public bool Answered => Answer.Length > 0;
    public string Answer { get; private set; } = "";
    public string? StopReason { get; private set; }
    public string? ForeignUserEventId { get; private set; }
    public TurnState State { get; private set; } = TurnState.Waiting;
    public Observation Last { get; private set; } = Observation.Ignored;

    public TurnState Observe(SessionSignal signal)
    {
        Last = State == TurnState.Waiting ? Classify(signal) : Observation.Ignored;
        switch (Last)
        {
            case Observation.OwnQueued: QueuedSeen = true; break;
            case Observation.OwnProcessed: Processed = true; break;
            case Observation.Foreign:
                // Another writer reached the same conversation: attribution is no longer exclusive.
                ForeignUserEventId = signal.Id;
                State = TurnState.Conflict;
                break;
            case Observation.Answer: Answer = Answer.Length == 0 ? signal.Text ?? "" : Answer + "\n" + signal.Text; break;
            case Observation.TransientIdle: StopReason = signal.StopReason; break;
            case Observation.Settled: StopReason = signal.StopReason; State = TurnState.Settled; break;
            case Observation.Stopped: StopReason = signal.StopReason ?? "terminated"; State = TurnState.Stopped; break;
        }
        return State;
    }

    Observation Classify(SessionSignal signal) => signal.Type switch
    {
        "user.message" when signal.Id == UserEventId => signal.Processed ? Observation.OwnProcessed : Observation.OwnQueued,
        "user.message" when signal.Id is not null && ownEventIds.Contains(signal.Id) => Observation.EarlierOwnEvent,
        "user.message" when signal.Id is not null => Observation.Foreign,
        "agent.thinking" when Processed => Observation.Thinking,
        "agent.message" when Processed => Observation.Answer,
        // An idle seen before our message was processed belongs to an earlier turn.
        "session.status_idle" when !Processed => Observation.StaleIdle,
        // requires_action is a transient idle (waiting on the client), never a finished turn.
        "session.status_idle" when signal.StopReason == "requires_action" => Observation.TransientIdle,
        "session.status_idle" when signal.StopReason == "end_turn" => Observation.Settled,
        "session.status_idle" => Observation.Stopped,
        "session.status_terminated" => Observation.Stopped,
        _ => Observation.Ignored,
    };
}
