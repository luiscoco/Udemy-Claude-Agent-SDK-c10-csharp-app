using Anthropic.Exceptions;
using Lesson.C10.Services;

namespace Lesson.C10.Tests;

public class TeachingTests
{
    static readonly TurnTracker Turn = new("evt-user-1", new HashSet<string> { "evt-user-1" });

    [Theory]
    [InlineData(Observation.Ignored)]
    [InlineData(Observation.EarlierOwnEvent)]
    public void Uninteresting_events_are_not_narrated(Observation observation) =>
        Assert.Null(Teaching.Describe(observation, Turn, 1, "cmd-1", hasNext: true, foreignId: null));

    [Fact]
    public void Settled_turn_says_whether_the_next_message_may_be_sent()
    {
        Assert.Contains("safe to send message 2", Teaching.Describe(Observation.Settled, Turn, 1, "cmd-1", hasNext: true, null)!.Detail);
        Assert.Contains("last queued message", Teaching.Describe(Observation.Settled, Turn, 2, "cmd-2", hasNext: false, null)!.Detail);
    }

    [Fact]
    public void Foreign_event_is_a_stop_tagged_with_one_writer()
    {
        var step = Teaching.Describe(Observation.Foreign, Turn, 1, "cmd-1", hasNext: true, foreignId: "evt-foreign")!;
        Assert.Equal(Concept.OneWriter, step.Concept);
        Assert.Equal(Tone.Stop, step.Tone);
        Assert.Contains("evt-foreign", step.Title);
    }

    [Fact]
    public void Every_concept_has_a_label_and_explanation()
    {
        foreach (var concept in Enum.GetValues<Concept>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Teaching.Label(concept)));
            Assert.False(string.IsNullOrWhiteSpace(Teaching.Explain(concept)));
        }
    }

    [Fact]
    public void Wrapped_cancellation_is_explained_as_a_timeout() =>
        Assert.Equal("the session did not finish before the time limit.",
            ManagedSessions.Describe(new InvalidOperationException("outer", new TaskCanceledException())));

    [Fact]
    public void Unknown_errors_keep_their_message() =>
        Assert.Equal("boom", ManagedSessions.Describe(new InvalidOperationException("boom")));
}
