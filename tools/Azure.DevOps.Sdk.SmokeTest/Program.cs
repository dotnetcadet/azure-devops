using System.Net;
using System.Text;
using Azure.DevOps.Sdk;
using Azure.DevOps.Sdk.Authentication;
using Azure.DevOps.Sdk.Models;

// Offline smoke test: a stub HTTP handler captures outgoing requests and returns canned
// responses, letting us assert URL composition, auth, the collection envelope, pagination,
// and JSON Patch serialization without touching the network.

var failures = 0;
void Check(bool condition, string message)
{
    Console.WriteLine((condition ? "  PASS " : "  FAIL ") + message);
    if (!condition)
    {
        failures++;
    }
}

var handler = new StubHandler();
var httpClient = new HttpClient(handler);
using var client = new AzureDevOpsClient(new AzureDevOpsClientOptions
{
    Organization = "myorg",
    Project = "MyProject",
    Credential = new PatCredential("dummy-pat"),
    HttpClient = httpClient,
});

// 1) Collection endpoint: list projects -> URL + auth + {count,value} unwrapping.
Console.WriteLine("Test: Core.Projects.ListAsync()");
handler.Next = (req) => Json("""{ "count": 2, "value": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Alpha" }, { "name": "Beta" } ] }""");
var projects = await client.Core.Projects.ListAsync();
Check(handler.LastUri!.ToString() == "https://dev.azure.com/myorg/_apis/projects?api-version=7.2-preview.4",
    $"URL = {handler.LastUri}");
Check(handler.LastRequest!.Headers.Authorization?.Scheme == "Basic", "Authorization is Basic PAT");
Check(projects.Count == 2 && projects[0].Name == "Alpha", "Unwrapped 2 projects, first is Alpha");

// 2) Deep chain with project + repo + PR id indexers.
Console.WriteLine("Test: Git.Repositories[\"MyRepo\"].PullRequests[42].GetPullRequestAsync()");
handler.Next = (req) => Json("""{ "pullRequestId": 42, "title": "Fix bug" }""");
var pr = await client.Git.Repositories["MyRepo"].PullRequests[42].GetPullRequestAsync();
// Note: ADO's endpoint template uses lowercase 'pullrequests' even though the fluent property is 'PullRequests'.
Check(handler.LastUri!.AbsolutePath == "/myorg/MyProject/_apis/git/repositories/MyRepo/pullrequests/42",
    $"Path = {handler.LastUri!.AbsolutePath}");
Check(handler.LastUri!.Query.Contains("api-version=7.2-preview"), $"Query = {handler.LastUri!.Query}");

// 3) Typed query parameters.
Console.WriteLine("Test: typed query parameters on Git.Repositories.ListAsync()");
handler.Next = (req) => Json("""{ "count": 0, "value": [] }""");
await client.Git.Repositories.ListAsync(config =>
{
    config.QueryParameters.IncludeLinks = true;
    config.QueryParameters.IncludeAllUrls = true;
});
Check(handler.LastUri!.Query.Contains("includeLinks=true"), $"includeLinks present: {handler.LastUri!.Query}");

// 4) JSON Patch body for work item creation -> content type + array body.
Console.WriteLine("Test: WorkItemTracking work item create with JsonPatchDocument");
handler.Next = (req) => Json("""{ "id": 7, "rev": 1 }""");
var patch = new JsonPatchDocument()
    .Add("/fields/System.Title", "New work item")
    .Add("/fields/System.State", "New");
// Work item creation uses the $type-indexed builder: /wit/workitems/${type}
var created = await client.WorkItemTracking.WorkItems["Bug"].CreateAsync(patch);
Check(handler.LastContentType == "application/json-patch+json", $"Content-Type = {handler.LastContentType}");
Check(handler.LastBody!.TrimStart().StartsWith('['), "Body serialized as a JSON array");
Check(handler.LastBody!.Contains("System.Title"), "Body contains the patched field");

// 5) WithProject produces an independent project-scoped view.
Console.WriteLine("Test: WithProject override");
handler.Next = (req) => Json("""{ "count": 0, "value": [] }""");
await client.WithProject("OtherProject").Build.Builds.ListAsync();
Check(handler.LastUri!.AbsolutePath.StartsWith("/myorg/OtherProject/"), $"Path uses OtherProject: {handler.LastUri!.AbsolutePath}");

// 6) Unknown enum value deserializes to null (not a wrong member) and is omitted on write-back.
Console.WriteLine("Test: unknown enum value is preserved-by-omission on round-trip");
var json = """{ "id": "00000000-0000-0000-0000-000000000000", "name": "Demo", "visibility": "brandNewVisibilityValue" }""";
var project = System.Text.Json.JsonSerializer.Deserialize<Azure.DevOps.Sdk.Core.Models.TeamProject>(
    json, Azure.DevOps.Sdk.Serialization.AzureDevOpsJson.Default);
Check(project is not null && project.Visibility is null, "Unknown enum 'visibility' deserialized to null");
var reserialized = System.Text.Json.JsonSerializer.Serialize(project, Azure.DevOps.Sdk.Serialization.AzureDevOpsJson.Default);
Check(!reserialized.Contains("visibility"), "Unknown enum omitted on write-back (no corruption)");

// Known enum value still round-trips.
var known = System.Text.Json.JsonSerializer.Deserialize<Azure.DevOps.Sdk.Core.Models.TeamProject>(
    """{ "name": "Demo", "visibility": "private" }""", Azure.DevOps.Sdk.Serialization.AzureDevOpsJson.Default);
Check(known?.Visibility == Azure.DevOps.Sdk.Core.Models.ProjectVisibility.Private, "Known enum 'private' round-trips");

// 7) Organization URL parsing: on-prem collapses all services; cloud keeps per-service subdomains.
Console.WriteLine("Test: organization URL routing");
var onprem = Azure.DevOps.Sdk.Http.AzureDevOpsUrl.Parse("https://tfs.contoso.com/DefaultCollection");
Check(onprem.Organization == "DefaultCollection", "on-prem collection parsed as organization");
Check(onprem.Resolver.Resolve("dev.azure.com") == "https://tfs.contoso.com"
   && onprem.Resolver.Resolve("vsrm.dev.azure.com") == "https://tfs.contoso.com",
    "on-prem collapses every service to the collection base");

var onpremVdir = Azure.DevOps.Sdk.Http.AzureDevOpsUrl.Parse("https://tfs.contoso.com/tfs/DefaultCollection");
Check(onpremVdir.Organization == "DefaultCollection"
   && onpremVdir.Resolver.Resolve("vssps.dev.azure.com") == "https://tfs.contoso.com/tfs",
    "on-prem with virtual directory keeps the prefix in the base");

var cloudUrl = Azure.DevOps.Sdk.Http.AzureDevOpsUrl.Parse("https://dev.azure.com/contoso");
Check(cloudUrl.Organization == "contoso"
   && cloudUrl.Resolver.Resolve("vsrm.dev.azure.com") == "https://vsrm.dev.azure.com",
    "cloud full URL extracts org and keeps per-service subdomains");

var vsts = Azure.DevOps.Sdk.Http.AzureDevOpsUrl.Parse("https://contoso.visualstudio.com");
Check(vsts.Organization == "contoso" && vsts.Resolver.Resolve("dev.azure.com") == "https://dev.azure.com",
    "visualstudio.com routes via modern dev.azure.com endpoints");

// 8) End-to-end on-prem request goes to the collection base, not dev.azure.com.
Console.WriteLine("Test: on-prem client builds collection-based URLs");
var onpremHandler = new StubHandler { Next = _ => Json("""{ "count": 0, "value": [] }""") };
using var onpremClient = new AzureDevOpsClient(new AzureDevOpsClientOptions
{
    OrganizationUrl = "https://tfs.contoso.com/DefaultCollection",
    Credential = new PatCredential("dummy"),
    HttpClient = new HttpClient(onpremHandler),
});
await onpremClient.Core.Projects.ListAsync();
Check(onpremHandler.LastUri!.ToString().StartsWith("https://tfs.contoso.com/DefaultCollection/_apis/projects"),
    $"on-prem URL = {onpremHandler.LastUri}");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL SMOKE TESTS PASSED" : $"{failures} SMOKE TEST(S) FAILED");
return failures == 0 ? 0 : 1;

static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json"),
};

sealed class StubHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Next { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
    public Uri? LastUri { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }
    public string? LastContentType { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastUri = request.RequestUri;
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            LastContentType = request.Content.Headers.ContentType?.MediaType;
        }
        else
        {
            LastBody = null;
            LastContentType = null;
        }

        return Next(request);
    }
}
