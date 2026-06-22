using Azure.DevOps.Sdk.Authentication;
using Azure.DevOps.Sdk.Http;

namespace Azure.DevOps.Sdk;

/// <summary>
/// The entry point to the Azure DevOps SDK. Construct it with an organization and a credential,
/// then chain into the API by area:
/// <code>
/// var client = new AzureDevOpsClient("contoso", new PatCredential(pat)) { };
/// var projects = await client.Core.Projects.GetAsync();
/// var repo     = await client.Git.Repositories["MyRepo"].GetAsync();
/// var pr       = await client.Git.Repositories["MyRepo"].PullRequests[42].GetAsync();
/// </code>
/// The area accessor properties (<c>Core</c>, <c>Git</c>, <c>Build</c>, …) are supplied by a
/// generated partial of this class — one per Azure DevOps REST area.
/// </summary>
public sealed partial class AzureDevOpsClient : IDisposable
{
    private readonly IRequestAdapter _adapter;
    private readonly IReadOnlyDictionary<string, object?> _pathParameters;
    private readonly bool _ownsAdapter;

    /// <summary>Creates a client for an organization using the supplied credential.</summary>
    /// <param name="organization">The Azure DevOps organization name.</param>
    /// <param name="credential">The credential (e.g. <see cref="PatCredential"/>).</param>
    /// <param name="project">An optional default project (name or id) for project-scoped endpoints.</param>
    public AzureDevOpsClient(string organization, IAzureDevOpsCredential credential, string? project = null)
        : this(new AzureDevOpsClientOptions { Organization = organization, Credential = credential, Project = project })
    {
    }

    /// <summary>Creates a client from a full options object.</summary>
    public AzureDevOpsClient(AzureDevOpsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Determine host routing and the organization/collection segment. A full URL may be supplied
        // via OrganizationUrl, or directly in Organization, to target on-prem / non-dev.azure.com hosts.
        var organizationUrl = options.OrganizationUrl
            ?? (AzureDevOpsUrl.IsUrl(options.Organization) ? options.Organization : null);

        ServiceHostResolver hostResolver;
        string? organization;
        if (organizationUrl is not null)
        {
            (hostResolver, organization) = AzureDevOpsUrl.Parse(organizationUrl);
        }
        else
        {
            hostResolver = options.HostOverrides is null
                ? ServiceHostResolver.Default
                : new ServiceHostResolver(options.HostOverrides);
            organization = options.Organization;
        }

        if (options.HttpClient is not null)
        {
            _adapter = new AzureDevOpsRequestAdapter(options.HttpClient, hostResolver, options.SerializerOptions, options.Credential);
            _ownsAdapter = true; // we own the adapter wrapper, not the HttpClient
        }
        else
        {
            if (options.Credential is null)
            {
                throw new ArgumentException("A credential is required when no pre-configured HttpClient is supplied.", nameof(options));
            }

            _adapter = new AzureDevOpsRequestAdapter(options.Credential, hostResolver, options.SerializerOptions);
            _ownsAdapter = true;
        }

        _pathParameters = BuildPathParameters(organization, options.Project);
    }

    private AzureDevOpsClient(IRequestAdapter adapter, IReadOnlyDictionary<string, object?> pathParameters)
    {
        _adapter = adapter;
        _pathParameters = pathParameters;
        _ownsAdapter = false; // derived views share the parent's adapter
    }

    /// <summary>The request adapter used by all builders created from this client.</summary>
    public IRequestAdapter RequestAdapter => _adapter;

    /// <summary>The organization this client targets, if set.</summary>
    public string? Organization => _pathParameters.TryGetValue("organization", out var v) ? v as string : null;

    /// <summary>The default project this client targets, if set.</summary>
    public string? Project => _pathParameters.TryGetValue("project", out var v) ? v as string : null;

    /// <summary>
    /// Returns a lightweight view of this client scoped to a different project. The returned client
    /// shares the same underlying connection; project-scoped endpoints will use the supplied project.
    /// </summary>
    public AzureDevOpsClient WithProject(string project)
    {
        var clone = new Dictionary<string, object?>(_pathParameters, StringComparer.Ordinal)
        {
            ["project"] = project,
        };
        return new AzureDevOpsClient(_adapter, clone);
    }

    /// <summary>
    /// Returns a lightweight view of this client scoped to a different organization, sharing the
    /// same underlying connection and credential.
    /// </summary>
    public AzureDevOpsClient WithOrganization(string organization)
    {
        var clone = new Dictionary<string, object?>(_pathParameters, StringComparer.Ordinal)
        {
            ["organization"] = organization,
        };
        return new AzureDevOpsClient(_adapter, clone);
    }

    private static Dictionary<string, object?> BuildPathParameters(string? organization, string? project)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(organization))
        {
            parameters["organization"] = organization;
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            parameters["project"] = project;
        }

        return parameters;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsAdapter && _adapter is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
