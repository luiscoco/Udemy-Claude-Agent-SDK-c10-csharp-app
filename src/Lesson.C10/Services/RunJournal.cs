using System.Text.Json;
using Lesson.C10.Lessons;

namespace Lesson.C10.Services;

// Appends results (not production workflow state) to .lab-data/runs.jsonl, as the Node app did.
public sealed class RunJournal(string directory)
{
    private static readonly JsonSerializerOptions Compact = new(Core.Json) { WriteIndented = false };
    private readonly SemaphoreSlim _write = new(1, 1);

    public async Task<string> Append(int lesson, Result result)
    {
        var id = Guid.NewGuid().ToString();
        var line = JsonSerializer.Serialize(new { id, at = DateTimeOffset.UtcNow, lesson, result }, Compact);
        await _write.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            await File.AppendAllTextAsync(Path.Combine(directory, "runs.jsonl"), line + "\n");
        }
        finally { _write.Release(); }
        return id;
    }
}

// One experiment at a time across every browser circuit, like the Node server's `busy` flag.
public sealed class ExperimentGate
{
    private readonly SemaphoreSlim _busy = new(1, 1);

    public async Task<T> Run<T>(Func<Task<T>> experiment)
    {
        if (!_busy.Wait(0)) throw new InvalidOperationException("An experiment is already running. Wait for it to finish.");
        try { return await experiment(); }
        finally { _busy.Release(); }
    }
}
