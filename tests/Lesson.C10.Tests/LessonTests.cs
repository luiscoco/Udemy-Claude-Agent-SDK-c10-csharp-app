using System.Text.Json.Nodes;
using Lesson.C10.Lessons;

namespace Lesson.C10.Tests;

public class LessonTests
{
    static readonly C10Lesson Lesson = new();

    public static TheoryData<string> Cases => new(Lesson.Cases.Select(scenario => scenario.Id));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Scenario_matches_expected_outcome_and_evidence(string id)
    {
        var scenario = Lesson.Cases.Single(c => c.Id == id);
        var result = await Lesson.Run(scenario.Input.DeepClone().AsObject());
        Assert.Equal(scenario.Expected, result.Outcome);
        foreach (var (key, value) in scenario.Checks ?? [])
            Assert.True(JsonNode.DeepEquals(value, result.Evidence[key]), key);
    }

    [Fact]
    public async Task Blank_message_is_rejected()
    {
        var input = new JsonObject { ["messages"] = new JsonArray("Remember Aurora", " "), ["concurrentInput"] = false };
        await Assert.ThrowsAsync<ArgumentException>(() => Lesson.Run(input));
    }
}
