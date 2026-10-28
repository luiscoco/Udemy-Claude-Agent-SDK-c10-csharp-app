using Lesson.C10.Services;

namespace Lesson.C10.Tests;

public class DotEnvTests : IDisposable
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"c10-{Guid.NewGuid():N}.env");
    readonly string _key = $"C10_TEST_{Guid.NewGuid():N}".ToUpperInvariant();

    public void Dispose()
    {
        File.Delete(_path);
        Environment.SetEnvironmentVariable(_key, null);
    }

    [Fact]
    public void Load_skips_comments_and_keeps_existing_values()
    {
        File.WriteAllLines(_path, ["# comment", $"{_key}=\"from-file\"", "", "NOT A PAIR"]);
        DotEnv.Load(_path);
        Assert.Equal("from-file", Environment.GetEnvironmentVariable(_key));

        File.WriteAllLines(_path, [$"{_key}=changed"]);
        DotEnv.Load(_path);
        Assert.Equal("from-file", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void Save_replaces_an_existing_key_and_appends_a_new_one()
    {
        File.WriteAllLines(_path, ["# keep me", $"{_key}=old"]);
        DotEnv.Save(_path, _key, "new");
        DotEnv.Save(_path, _key + "_2", "added");
        Environment.SetEnvironmentVariable(_key + "_2", null);

        Assert.Equal(["# keep me", $"{_key}=new", $"{_key}_2=added"], File.ReadAllLines(_path));
    }
}
