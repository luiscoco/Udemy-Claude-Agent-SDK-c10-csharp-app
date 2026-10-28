namespace Lesson.C10.Services;

/// The lesson's three concepts. Every timeline step is tagged with the one it demonstrates.
public enum Concept { OneWriter, Correlate, Settled }

public enum Tone { Info, Good, Wait, Stop }

public sealed record TeachingStep(Concept Concept, Tone Tone, string Title, string Detail, int Number = 0);

public static class Teaching
{
    public static string Label(Concept concept) => concept switch
    {
        Concept.OneWriter => "1 · One writer per conversation",
        Concept.Correlate => "2 · Correlate each accepted user event",
        _ => "3 · Advance only after settled completion",
    };

    public static string Explain(Concept concept) => concept switch
    {
        Concept.OneWriter => "Only one sender may post into a session. If anyone else does, you can no longer tell whose turn an answer or idle belongs to.",
        Concept.Correlate => "Every send returns the event ID the server accepted. That ID is the receipt that ties stream events back to your command.",
        _ => "An answer is not the end of a turn. Send the next message only after the session goes idle with end_turn.",
    };

    public static TeachingStep SessionCreated(string sessionId) => new(Concept.OneWriter, Tone.Info,
        "Created one session for the whole conversation",
        $"Session {sessionId} will hold every turn. The queue sends all messages to this one session, and it expects to be the only writer.");

    public static TeachingStep StreamOpened() => new(Concept.Correlate, Tone.Info,
        "Opened the event stream before sending anything",
        "The stream has no replay: an event emitted before you connect is lost. So the reader starts first, and every later event reaches it.");

    public static TeachingStep Sent(int turn, string commandId, string message, string userEventId) => new(Concept.Correlate, Tone.Info,
        $"Turn {turn}: sent “{message}”",
        $"The server accepted it as event {userEventId}. That ID is the receipt for {commandId}: only stream events carrying it count as this command's turn.");

    public static TeachingStep SecondWriter(string message) => new(Concept.OneWriter, Tone.Stop,
        "A second writer posted into the same session",
        $"This simulates another browser tab or service sending “{message}”. The queue was not told this event's ID.");

    public static TeachingStep Final(int turns) => new(Concept.Settled, Tone.Good,
        $"All {turns} turns settled, one at a time, in order",
        "Compare the answers below: a later answer can use earlier turns only because every message went to the same session and no turns overlapped.");

    /// Explains one observation, or returns null when the event teaches nothing new (spans, usage, repeats).
    public static TeachingStep? Describe(Observation observation, TurnTracker turn, int turnNumber, string commandId, bool hasNext, string? foreignId) => observation switch
    {
        Observation.OwnQueued => new(Concept.Correlate, Tone.Wait, $"Event {turn.UserEventId} is queued",
            "The session has received our message but is still busy with earlier work (processed_at is empty)."),
        Observation.OwnProcessed => new(Concept.Correlate, Tone.Good, $"The session picked up {commandId}'s event",
            $"Event {turn.UserEventId} matches the receipt for {commandId}, so what Claude says next belongs to this command."),
        Observation.Foreign => new(Concept.OneWriter, Tone.Stop, $"Unknown user event {foreignId}: another writer!",
            "This queue never sent that event, so the conversation no longer has a single writer. Any later answer or idle could belong to the other writer's turn. The queue stops sending."),
        Observation.Thinking => new(Concept.Settled, Tone.Wait, "Claude is thinking",
            "The turn is running. Sending the next message now could interleave turns, so the queue waits."),
        Observation.Answer => new(Concept.Settled, Tone.Info, "Claude answered",
            $"“{turn.Answer}” The turn is not over yet: an answer is not the same as a settled session."),
        Observation.StaleIdle => new(Concept.Settled, Tone.Info, "An idle from an earlier turn: ignored",
            $"It arrived before our message was processed, so it says nothing about turn {turnNumber}."),
        Observation.TransientIdle => new(Concept.Settled, Tone.Wait, "Idle with requires_action: keep waiting",
            "The session is paused waiting for the client. That is not a finished turn."),
        Observation.Settled => new(Concept.Settled, Tone.Good, $"Idle with end_turn: turn {turnNumber} is settled",
            hasNext ? $"Only now is it safe to send message {turnNumber + 1}." : "That was the last queued message."),
        Observation.Stopped => new(Concept.Settled, Tone.Stop, $"The turn stopped: {turn.StopReason}",
            "The session stopped for a reason other than end_turn, so the queue does not advance."),
        _ => null,
    };
}
