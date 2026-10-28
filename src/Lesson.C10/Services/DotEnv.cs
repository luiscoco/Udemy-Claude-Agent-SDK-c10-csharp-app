namespace Lesson.C10.Services;

// Mirrors Node's process.loadEnvFile: values already set in the real environment win.
public static class DotEnv
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
        }
    }

    // Updates or appends one KEY=value line, saving immediately so later failures cannot lose it.
    public static void Save(string path, string key, string value)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(key + "="));
        if (index >= 0) lines[index] = $"{key}={value}"; else lines.Add($"{key}={value}");
        File.WriteAllLines(path, lines);
        Environment.SetEnvironmentVariable(key, value);
    }
}
