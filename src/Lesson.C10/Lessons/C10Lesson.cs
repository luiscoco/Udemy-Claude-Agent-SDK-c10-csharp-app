using System.Text.Json.Nodes;
using static Lesson.C10.Lessons.Core;

namespace Lesson.C10.Lessons;

// A focused local experiment derived from this lesson's reviewed presentation.
public sealed class C10Lesson : ILesson
{
    public int Number => 10;
    public string Title => "Multi-turn Sessions and Message Queues";
    public string Description => "Real conversations need follow-up questions. A Managed Agents session retains its history on the server, so another user message sent to the same session continues that conversation.";
    public IReadOnlyList<string> Concepts { get; } = ["One writer per conversation", "Correlate each accepted user event", "Advance only after settled completion"];
    public string Scope => "Runs an in-process serialized queue with authored turn events. It demonstrates admission and attribution, not a durable distributed queue. Each run starts from a fresh local seed. The run journal stores results, not production workflow state.";

    public IReadOnlyList<Scenario> Cases { get; } =
    [
        // Turn 2 can only be answered from turn 1, which makes shared session history visible in the live queue.
        new("0", "Two correlated turns",
            new JsonObject { ["messages"] = new JsonArray("My project is called Aurora. Remember that.", "What is my project called?"), ["concurrentInput"] = false },
            Outcome.Passed, new JsonObject { ["turns"] = 2 }),
        new("1", "Detect a competing writer",
            new JsonObject { ["messages"] = new JsonArray("My project is called Aurora. Remember that.", "What is my project called?"), ["concurrentInput"] = true },
            Outcome.Conflict, new JsonObject { ["turns"] = 0 }),
    ];

    public Task<Result> Run(JsonObject input)
    {
        var messages = List(input, "messages");
        if (messages.Any(m => m is not JsonValue v || !v.TryGetValue(out string? s) || string.IsNullOrWhiteSpace(s)))
            throw new ArgumentException("Each queued message must contain text");

        if (Flag(input, "concurrentInput"))
            return Task.FromResult(Report(Outcome.Conflict, "Another writer invalidated exclusive turn attribution",
                new JsonObject { ["turns"] = 0 },
                "Admit one writer", "Observe a foreign user event", "Block queue advancement"));

        var receipts = new JsonArray(messages.Select((message, index) => (JsonNode)new JsonObject
        {
            ["commandId"] = $"cmd-{index + 1}",
            ["userEventId"] = $"user-{index + 1}",
            ["message"] = message!.GetValue<string>(),
            ["status"] = "idle/end_turn",
        }).ToArray());

        return Task.FromResult(Report(Outcome.Passed, "Serialized each admitted turn in one conversation",
            new JsonObject { ["turns"] = receipts.Count, ["receipts"] = receipts },
            "Open one shared stream reader", "Send exactly one queued message", "Match the accepted user event ID", "Confirm reusable idle before advancing"));
    }
}
