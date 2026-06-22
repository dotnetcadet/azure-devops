using Azure.DevOps.Cli.Configuration;
using Azure.DevOps.Sdk;
using Azure.DevOps.Sdk.Authentication;

namespace Azure.DevOps.Mcp;

/// <summary>
/// Resolves the Azure DevOps connection for the current repository and builds the SDK client.
/// Resolution order, most specific first: environment variables, the local git remote, and the
/// credentials saved by the <c>azdo</c> CLI (so a developer who has run <c>azdo</c> login gets a
/// working server with zero extra configuration).
/// </summary>
public sealed class AzureDevOpsContext
{
    private readonly Lazy<Resolution> _resolution;
    private AzureDevOpsClient? _client;

    public AzureDevOpsContext()
    {
        _resolution = new Lazy<Resolution>(Resolve);
    }

    public string? Organization => _resolution.Value.Organization;

    public string? Project => _resolution.Value.Project;

    public string? DefaultRepository => _resolution.Value.Repository;

    /// <summary>The SDK client for the resolved connection. Throws a guidance error when unresolved.</summary>
    public AzureDevOpsClient Client
    {
        get
        {
            var r = _resolution.Value;
            if (string.IsNullOrWhiteSpace(r.Organization) || string.IsNullOrWhiteSpace(r.Token))
            {
                throw new InvalidOperationException(
                    "No Azure DevOps connection is configured. Provide AZURE_DEVOPS_ORG and AZURE_DEVOPS_PAT " +
                    "environment variables (org/project are also auto-detected from the repo's git remote), " +
                    "or run the 'azdo' CLI once to sign in and save a profile.");
            }

            return _client ??= new AzureDevOpsClient(new AzureDevOpsClientOptions
            {
                Organization = r.Organization,
                Project = r.Project,
                Credential = new PatCredential(r.Token),
            });
        }
    }

    /// <summary>Resolves a repository argument, falling back to the repo detected from the git remote.</summary>
    public string ResolveRepository(string? repository)
    {
        if (!string.IsNullOrWhiteSpace(repository))
        {
            return repository;
        }

        if (!string.IsNullOrWhiteSpace(DefaultRepository))
        {
            return DefaultRepository!;
        }

        throw new InvalidOperationException(
            "No repository specified and none could be detected from the git remote. Pass the 'repository' argument.");
    }

    /// <summary>A human-readable summary of the resolved connection and where each value came from.</summary>
    public object Describe()
    {
        var r = _resolution.Value;
        return new
        {
            organization = r.Organization,
            project = r.Project,
            repository = r.Repository,
            authenticated = !string.IsNullOrWhiteSpace(r.Token),
            sources = r.Sources,
        };
    }

    private Resolution Resolve()
    {
        var sources = new Dictionary<string, string>();
        var repoPath = Env("AZURE_DEVOPS_REPO_PATH", "AZDO_REPO_PATH") ?? Directory.GetCurrentDirectory();

        GitRemoteInfo? git = null;
        try
        {
            git = GitRemote.Detect(repoPath);
        }
        catch
        {
            // git not available / not a repo — ignore.
        }

        Profile? profile = TryLoadCliProfile();

        var organization = Pick(sources, "organization",
            ("env", Env("AZURE_DEVOPS_ORG", "AZDO_ORG")),
            ("env-url", OrgFromUrl(Env("AZURE_DEVOPS_ORG_URL", "AZDO_ORG_URL"))),
            ("git-remote", git?.Organization),
            ("azdo-profile", profile?.Organization));

        var project = Pick(sources, "project",
            ("env", Env("AZURE_DEVOPS_PROJECT", "AZDO_PROJECT")),
            ("git-remote", git?.Project),
            ("azdo-profile", profile?.Project));

        var repository = Pick(sources, "repository",
            ("env", Env("AZURE_DEVOPS_REPO", "AZDO_REPO")),
            ("git-remote", git?.Repository));

        string? token = null;
        var envToken = Env("AZURE_DEVOPS_PAT", "AZDO_PAT", "AZURE_DEVOPS_EXT_PAT");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            token = envToken;
            sources["token"] = "env";
        }
        else if (profile is not null)
        {
            try
            {
                token = SecretProtector.Unprotect(profile.ProtectedToken);
                sources["token"] = "azdo-profile";
            }
            catch
            {
                // Could not decrypt (e.g. profile from another machine/OS) — leave unauthenticated.
            }
        }

        return new Resolution(organization, project, repository, token, sources);
    }

    private static Profile? TryLoadCliProfile()
    {
        try
        {
            var config = new ProfileStore().Load();
            var name = Environment.GetEnvironmentVariable("AZDO_PROFILE");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return config.Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            return config.Profiles.FirstOrDefault(p => p.Name == config.CurrentProfile)
                   ?? config.Profiles.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? Pick(Dictionary<string, string> sources, string key, params (string Source, string? Value)[] candidates)
    {
        foreach (var (source, value) in candidates)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sources[key] = source;
                return value.Trim();
            }
        }

        return null;
    }

    private static string? Env(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? OrgFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : null;
        }

        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host[..uri.Host.IndexOf('.')];
        }

        return null;
    }

    private sealed record Resolution(
        string? Organization,
        string? Project,
        string? Repository,
        string? Token,
        IReadOnlyDictionary<string, string> Sources);
}
