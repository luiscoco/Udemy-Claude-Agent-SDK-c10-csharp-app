using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lesson.C10.Lessons;

public enum Outcome { Passed, Rejected, Paused, Conflict, Incomplete, Unconfirmed }

public sealed record Result(string Source, Outcome Outcome, string Summary, JsonObject Evidence, IReadOnlyList<string> Steps);

public sealed record Scenario(string Id, string Label, JsonObject Input, Outcome Expected, JsonObject? Checks = null);

public interface ILesson
{
    int Number { get; }
    string Title { get; }
    string Description { get; }
    IReadOnlyList<string> Concepts { get; }
    string Scope { get; }
    IReadOnlyList<Scenario> Cases { get; }
    Task<Result> Run(JsonObject input);
}

public static class Core
{
    // camelCase properties, lowercase outcomes and unescaped quotes keep the journal and evidence identical to the Node app.
    // Relaxed escaping is safe: Blazor HTML-encodes everything it renders.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Format(Outcome outcome) => outcome.ToString().ToLowerInvariant();

    public static string Text(JsonObject input, string key, string fallback = "")
    {
        var node = input[key];
        if (node is null) return fallback;
        if (node is not JsonValue value || !value.TryGetValue(out string? text) || text.Length > 20000)
            throw new ArgumentException($"{key} must be bounded text");
        return text;
    }

    public static double Number(JsonObject input, string key, double fallback = 0)
    {
        var node = input[key];
        if (node is null) return fallback;
        if (node is not JsonValue value || !value.TryGetValue(out double number) || !double.IsFinite(number))
            throw new ArgumentException($"{key} must be a finite number");
        return number;
    }

    public static bool Flag(JsonObject input, string key, bool fallback = false)
    {
        var node = input[key];
        if (node is null) return fallback;
        if (node is not JsonValue value || !value.TryGetValue(out bool flag))
            throw new ArgumentException($"{key} must be true or false");
        return flag;
    }

    public static JsonArray List(JsonObject input, string key)
    {
        if (input[key] is not JsonArray array || array.Count > 200)
            throw new ArgumentException($"{key} must be a list of at most 200 values");
        return array;
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static Result Report(Outcome outcome, string summary, JsonObject evidence, params string[] steps) =>
        new("local-experiment", outcome, summary, evidence, steps);

    public static bool SafePath(string value) =>
        !string.IsNullOrEmpty(value) && !value.StartsWith('/') && !value.Contains('\\') && !value.Contains(':')
        && !value.Split('/').Any(part => part is ".." or "." or "" or ".git" or ".env" or ".agents" or ".codex");
}
