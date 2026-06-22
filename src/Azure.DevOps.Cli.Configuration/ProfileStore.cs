using System.Text.Json;

namespace Azure.DevOps.Cli.Configuration;

/// <summary>Loads and persists the profile configuration under the user's application data folder.</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public ProfileStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "azdo");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "profiles.json");
    }

    public string Location => _path;

    public ProfileConfig Load()
    {
        if (!File.Exists(_path))
        {
            return new ProfileConfig();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ProfileConfig>(json, Options) ?? new ProfileConfig();
        }
        catch (JsonException)
        {
            return new ProfileConfig();
        }
    }

    public void Save(ProfileConfig config)
    {
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(_path, json);
    }
}
