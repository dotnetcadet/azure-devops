using System.Text.Json.Serialization;

namespace Azure.DevOps.Cli.Configuration;

/// <summary>
/// A saved connection: a named organization (with an optional default project) plus the
/// protected personal access token used to authenticate against it.
/// </summary>
public sealed class Profile
{
    /// <summary>A friendly, unique name for this profile (e.g. <c>contoso-prod</c>).</summary>
    public required string Name { get; set; }

    /// <summary>The Azure DevOps organization name.</summary>
    public required string Organization { get; set; }

    /// <summary>An optional default project for project-scoped operations.</summary>
    public string? Project { get; set; }

    /// <summary>The protected (encrypted) personal access token. Never stored in plain text.</summary>
    public required string ProtectedToken { get; set; }

    [JsonIgnore]
    public string Display => string.IsNullOrEmpty(Project)
        ? $"{Name}  ({Organization})"
        : $"{Name}  ({Organization}/{Project})";
}

/// <summary>The on-disk shape of the profile store.</summary>
public sealed class ProfileConfig
{
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>The name of the most recently used profile.</summary>
    public string? CurrentProfile { get; set; }
}
