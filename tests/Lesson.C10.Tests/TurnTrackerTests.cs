using Lesson.C10.Services;

namespace Lesson.C10.Tests;

public class TurnTrackerTests
{
    static TurnTracker Track(string id, params string[] ownIds) => new(id, new HashSet<string>(ownIds.Append(id)));

    static TurnState Replay(TurnTracker turn, params SessionSignal[] signals)
    {
        foreach (var signal in signals)
            if (turn.Observe(signal) != TurnState.Waiting) break;
        return turn.State;
    }

    [Fact]
    public void Recorded_single_turn_settles_on_end_turn()
    {
        // Event order observed from a real session (sesn_01GodGRNyPmSMViDe69LYanP).
        var turn = Track("evt-user-1");
        var state = Replay(turn,
            new("session.status_running"),
            new("session.thread_status_running"),
            new("user.message", "evt-user-1", Processed: true),
            new("span.model_request_start"),
            new("agent.thinking"),
            new("agent.message", "evt-agent-1", Text: "Aurora noted."),
            new("span.model_request_end"),
            new("session.thread_status_idle"),
            new("session.usage"),
            new("session.status_idle", "evt-idle-1", StopReason: "end_turn"));

        Assert.Equal(TurnState.Settled, state);
        Assert.True(turn.Processed);
        Assert.Equal("Aurora noted.", turn.Answer);
    }

    [Fact]
    public void Queued_then_processed_echo_is_tracked()
    {
        var turn = Track("evt-user-1");
        turn.Observe(new("user.message", "evt-user-1", Processed: false));
        Assert.True(turn.QueuedSeen);
        Assert.False(turn.Processed);
        turn.Observe(new("user.message", "evt-user-1", Processed: true));
        Assert.True(turn.Processed);
    }

    [Fact]
    public void Idle_before_our_message_is_processed_does_not_settle()
    {
        // The idle that ended turn 1 can still be buffered when turn 2 starts.
        var turn = Track("evt-user-2", "evt-user-1");
        Assert.Equal(TurnState.Waiting, turn.Observe(new("session.status_idle", StopReason: "end_turn")));
        turn.Observe(new("user.message", "evt-user-2", Processed: true));
        Assert.Equal(TurnState.Settled, turn.Observe(new("session.status_idle", StopReason: "end_turn")));
    }

    [Fact]
    public void Earlier_own_user_event_is_not_a_conflict()
    {
        var turn = Track("evt-user-2", "evt-user-1");
        Assert.Equal(TurnState.Waiting, turn.Observe(new("user.message", "evt-user-1", Processed: true)));
    }

    [Fact]
    public void Foreign_user_event_is_a_conflict()
    {
        var turn = Track("evt-user-1");
        var state = Replay(turn,
            new("user.message", "evt-user-1", Processed: false),
            new("user.message", "evt-foreign", Processed: false),
            new("user.message", "evt-user-1", Processed: true),
            new("session.status_idle", StopReason: "end_turn"));

        Assert.Equal(TurnState.Conflict, state);
        Assert.Equal("evt-foreign", turn.ForeignUserEventId);
    }

    [Fact]
    public void Requires_action_idle_keeps_waiting()
    {
        var turn = Track("evt-user-1");
        turn.Observe(new("user.message", "evt-user-1", Processed: true));
        Assert.Equal(TurnState.Waiting, turn.Observe(new("session.status_idle", StopReason: "requires_action")));
        Assert.Equal(TurnState.Settled, turn.Observe(new("session.status_idle", StopReason: "end_turn")));
    }

    [Theory]
    [InlineData("retries_exhausted")]
    [InlineData("budget_reached")]
    [InlineData("refusal")]
    public void Other_stop_reasons_stop_the_queue(string stopReason)
    {
        var turn = Track("evt-user-1");
        turn.Observe(new("user.message", "evt-user-1", Processed: true));
        Assert.Equal(TurnState.Stopped, turn.Observe(new("session.status_idle", StopReason: stopReason)));
        Assert.Equal(stopReason, turn.StopReason);
    }

    [Fact]
    public void Termination_stops_the_queue()
    {
        var turn = Track("evt-user-1");
        Assert.Equal(TurnState.Stopped, turn.Observe(new("session.status_terminated")));
    }

    [Fact]
    public void Recorded_competing_writer_run_is_a_conflict_without_a_settled_idle()
    {
        // Event order observed from a real session (sesn_01FfpUPHiqdmmJ5B86NG9cGx): the second writer's
        // message was picked up right after our answer, with no end_turn idle in between.
        var turn = Track("sevt_01GGr6CRirsovswm1A4X6fWk");
        var observations = new List<Observation>();
        foreach (var signal in new SessionSignal[]
        {
            new("session.status_running"),
            new("session.thread_status_running"),
            new("user.message", "sevt_01GGr6CRirsovswm1A4X6fWk", Processed: true),
            new("span.model_request_start"),
            new("agent.thinking"),
            new("agent.message", Text: "Got it, I'll remember \"Aurora\"."),
            new("span.model_request_end"),
            new("user.message", "sevt_01BonHeeJAiZ7HYPh9HWrzGQ", Processed: true),
        })
        {
            turn.Observe(signal);
            observations.Add(turn.Last);
        }

        Assert.Equal(TurnState.Conflict, turn.State);
        Assert.True(turn.Answered);
        Assert.Equal(
            [Observation.Ignored, Observation.Ignored, Observation.OwnProcessed, Observation.Ignored, Observation.Thinking, Observation.Answer, Observation.Ignored, Observation.Foreign],
            observations);
    }

    [Fact]
    public void Events_after_the_turn_ends_are_ignored()
    {
        var turn = Track("evt-user-1");
        turn.Observe(new("user.message", "evt-user-1", Processed: true));
        turn.Observe(new("session.status_idle", StopReason: "end_turn"));
        turn.Observe(new("user.message", "evt-foreign", Processed: true));
        Assert.Equal(TurnState.Settled, turn.State);
        Assert.Equal(Observation.Ignored, turn.Last);
    }

    [Fact]
    public void Agent_text_before_our_message_is_processed_is_ignored()
    {
        var turn = Track("evt-user-2", "evt-user-1");
        turn.Observe(new("agent.message", Text: "Answer to turn 1"));
        turn.Observe(new("user.message", "evt-user-2", Processed: true));
        turn.Observe(new("agent.message", Text: "Your project is Aurora."));
        Assert.Equal("Your project is Aurora.", turn.Answer);
    }
}
