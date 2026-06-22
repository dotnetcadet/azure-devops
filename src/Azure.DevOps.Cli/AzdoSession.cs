using Azure.DevOps.Cli.Configuration;
using Azure.DevOps.Sdk;
using Azure.DevOps.Sdk.Authentication;

namespace Azure.DevOps.Cli;

/// <summary>
/// Holds the loaded profiles and the active connection, and builds an <see cref="AzureDevOpsClient"/>
/// from the current profile on demand. The owning client (which owns the HTTP connection) is cached and
/// disposed when the profile changes; a transient project selection is layered on as a lightweight view.
/// </summary>
public sealed class AzdoSession : IDisposable
{
    private readonly ProfileStore _store;
    private readonly ProfileConfig _config;
    private AzureDevOpsClient? _owner;
    private string? _projectOverride;

    public AzdoSession()
    {
        _store = new ProfileStore();
        _config = _store.Load();
        Current = _config.Profiles.FirstOrDefault(p => p.Name == _config.CurrentProfile)
                  ?? _config.Profiles.FirstOrDefault();
    }

    public string StoreLocation => _store.Location;

    public IReadOnlyList<Profile> Profiles => _config.Profiles;

    public Profile? Current { get; private set; }

    /// <summary>The project actually in effect — a transient override if set, otherwise the profile default.</summary>
    public string? ActiveProject => _projectOverride ?? Current?.Project;

    /// <summary>The owning SDK client for the current profile (owns the connection), created lazily.</summary>
    private AzureDevOpsClient Owner => _owner ??= CreateClient(Current
        ?? throw new InvalidOperationException("No profile is selected. Add one first."));

    /// <summary>The SDK client to use — the owner, scoped to a transient project override when one is set.</summary>
    public AzureDevOpsClient Client => _projectOverride is null ? Owner : Owner.WithProject(_projectOverride);

    public static AzureDevOpsClient CreateClient(Profile profile)
    {
        var token = SecretProtector.Unprotect(profile.ProtectedToken);
        return new AzureDevOpsClient(new AzureDevOpsClientOptions
        {
            Organization = profile.Organization,
            Project = profile.Project,
            Credential = new PatCredential(token),
        });
    }

    public void Upsert(Profile profile)
    {
        var existing = _config.Profiles.FindIndex(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _config.Profiles[existing] = profile;
        }
        else
        {
            _config.Profiles.Add(profile);
        }

        SetCurrent(profile);
    }

    public void Remove(Profile profile)
    {
        _config.Profiles.RemoveAll(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        if (Current?.Name == profile.Name)
        {
            ResetConnection();
            Current = _config.Profiles.FirstOrDefault();
            _config.CurrentProfile = Current?.Name;
        }

        Persist();
    }

    public void SetCurrent(Profile profile)
    {
        ResetConnection();
        Current = profile;
        _config.CurrentProfile = profile.Name;
        Persist();
    }

    /// <summary>
    /// Switches the active project for the rest of this session only — a transient view over the same
    /// connection. The profile's saved default project is left untouched.
    /// </summary>
    public void UseProject(string project)
    {
        _projectOverride = project;
    }

    private void ResetConnection()
    {
        _owner?.Dispose();
        _owner = null;
        _projectOverride = null;
    }

    private void Persist() => _store.Save(_config);

    public void Dispose()
    {
        _owner?.Dispose();
        _owner = null;
    }
}
