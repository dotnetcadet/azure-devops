# Azure DevOps Utilities

A fluent **.NET 10 SDK** for the Azure DevOps REST API (version **7.2**) and a **slick interactive CLI**
built on top of it.

The SDK mirrors the **Microsoft Graph SDK** ergonomics: you navigate the API by chaining strongly typed
request builders and indexers, then call an operation method.

```csharp
var client = new AzureDevOpsClient("contoso", new PatCredential(pat), project: "MyProject");

// Collections, indexers, and deep chains — just like the Graph SDK
var projects = await client.Core.Projects.ListAsync();
var repo     = await client.Git.Repositories["MyRepo"].GetAsync();
var thread   = await client.Git.Repositories["MyRepo"].PullRequests[42].Threads[7].GetAsync();

// Typed query parameters
var prs = await client.Git.Repositories["MyRepo"].PullRequests.GetPullRequestsAsync(
    cfg => cfg.QueryParameters.SearchCriteriaStatus = PullRequestStatus.Active);

// JSON Patch helpers for work items
var patch = new JsonPatchDocument()
    .Add("/fields/System.Title", "New bug")
    .Add("/fields/System.State", "Active");
var workItem = await client.WorkItemTracking.WorkItems["Bug"].CreateAsync(patch);
```

---

## Repository layout

| Path | What it is |
|------|------------|
| `src/Azure.DevOps.Sdk` | The SDK. Hand-written runtime + **generated** request builders and models. |
| `src/Azure.DevOps.Cli` | The `azdo` CLI (interactive, Sharprompt-based). |
| `src/Azure.DevOps.Cli.Configuration` | Shared profile store + PAT protection (used by the CLI and the MCP server). |
| `src/Azure.DevOps.Mcp` | A local **MCP server** that exposes the SDK as a GitHub Copilot skill. |
| `tools/Azure.DevOps.Sdk.Generator` | Spec-driven C# code generator that emits the SDK surface. |
| `tools/Azure.DevOps.Sdk.SmokeTest` | Offline end-to-end test of the runtime pipeline. |
| `specs/7.2` | Vendored Azure DevOps REST 7.2 OpenAPI specs (the generator input). |

---

## The SDK

### Coverage

The SDK is generated from the **official Azure DevOps OpenAPI 7.2 specs**, so it covers the
**entire** documented REST surface:

- **44 API areas** — Core, Git, Build, Pipelines, Release, Work Item Tracking, Work, Wiki, Graph,
  Distributed Task, Policy, Service Endpoints, Test / Test Plan / Test Results, Artifacts &
  package types, Dashboards, Notifications, Member Entitlement Management, Audit, Security,
  Advanced Security, Extensions, and more.
- **~1,086 operations**, **2,654 model classes**, **500 enums**.

Because the surface is generated from specs, regenerating against a newer API version is a single command.

### Design

- **Request-builder chaining** — every URL segment is a builder; collections expose an indexer
  (`["id"]`) that returns the item builder. This is the Microsoft Graph SDK pattern.
- **Organization / project context** — `{organization}` and `{project}` are supplied from the client
  (or `WithOrganization` / `WithProject` views) rather than cluttering the chain. The optional
  `{team}` scope is handled the same way.
- **Authentication is pluggable** — `IAzureDevOpsCredential` is the seam. `PatCredential` ships today;
  `BearerTokenCredential` covers any OAuth 2.0 / Microsoft Entra ID token flow (static or refreshed via a
  delegate), and new schemes can be added without touching the pipeline.
- **Resilient runtime** — automatic retry/backoff that honors `Retry-After` (HTTP 429/503), transparent
  unwrapping of the `{ count, value }` collection envelope, continuation-token pagination via
  `PagedList<T>`, tolerant enum deserialization (forward-compatible with new service enum values), and
  typed `VssServiceException` errors.
- **On-prem ready** — a `ServiceHostResolver` maps each operation's logical host (e.g. `vsrm.dev.azure.com`)
  to a base URI, so Azure DevOps Server / sovereign clouds can be targeted by supplying host overrides.

### Authentication

```csharp
// Personal Access Token (today)
var client = new AzureDevOpsClient("contoso", new PatCredential(pat));

// Any bearer-token flow (OAuth / Entra ID), refreshed per request
var client = new AzureDevOpsClient(new AzureDevOpsClientOptions
{
    Organization = "contoso",
    Credential   = new BearerTokenCredential(ct => GetEntraTokenAsync(ct)),
});
```

### Multiple organizations and projects

```csharp
var other = client.WithOrganization("fabrikam").WithProject("Web");
await other.Build.Builds.ListAsync();   // shares the same connection
```

### Pagination, errors, streams

```csharp
PagedList<GitRepository> page = await client.Git.Repositories.ListAsync();
if (page.HasMore) { /* page.ContinuationToken */ }

try { await client.Core.Projects["missing"].GetAsync(); }
catch (VssServiceException ex) { Console.WriteLine($"{(int)ex.StatusCode}: {ex.Message}"); }

Stream zip = await client.Git.Repositories["MyRepo"].Items.GetAsync(/* download */);
```

---

## The CLI (`azdo`)

An interactive, multi-organization / multi-project console powered by
[Sharprompt](https://github.com/shibayan/Sharprompt).

```
dotnet run --project src/Azure.DevOps.Cli      # or run the built `azdo` executable
```

Features:

- **Named profiles** — each profile is an organization + optional default project + a PAT. Switch between
  them at any time; the active project can be changed on the fly.
- **Secure token storage** — PATs are encrypted at rest with **Windows DPAPI** (per-user). On other
  platforms the tool falls back to a reversible encoding and warns you.
- **Browse your org** — list projects, repositories, pull requests, and recent builds; get and create work
  items — all through the SDK.
- **Credential verification** — adding a profile validates it against Azure DevOps before saving.

```
azdo --help        # usage
azdo --version     # version
azdo               # launch the interactive console
```

Profiles are stored at `%APPDATA%/azdo/profiles.json`.

---

## GitHub Copilot skill (MCP)

`src/Azure.DevOps.Mcp` is a local [MCP](https://modelcontextprotocol.io) server that lets **GitHub
Copilot** manage Azure DevOps objects for the repository you're working in — list/create pull
requests, query and edit work items, run pipelines, and more. It reuses the SDK and the `azdo` CLI
login, and **auto-detects** the organization/project/repository from the repo's git remote, so it
works with zero configuration once you've signed in.

```bash
dotnet pack src/Azure.DevOps.Mcp -c Release
dotnet tool install --global --add-source ./src/Azure.DevOps.Mcp/nupkg Azure.DevOps.Mcp
```

That installs an `azure-devops-mcp` command; VS Code Copilot picks it up via the committed
[`.vscode/mcp.json`](.vscode/mcp.json) (and the Copilot CLI via [`.mcp.json`](.mcp.json)). Full
instructions, auth options, and example prompts are in [docs/copilot-skill.md](docs/copilot-skill.md).

## Building & running

```bash
dotnet build azure-devops.slnx          # build everything
dotnet run  --project tools/Azure.DevOps.Sdk.SmokeTest   # offline pipeline test
dotnet run  --project src/Azure.DevOps.Cli               # the CLI
```

### Regenerating the SDK

The generated files under `src/Azure.DevOps.Sdk/Generated/` are produced from `specs/7.2/`:

```bash
dotnet run --project tools/Azure.DevOps.Sdk.Generator
```

To target a different API version, vendor that version's specs and point the generator at them:

```bash
dotnet run --project tools/Azure.DevOps.Sdk.Generator -- path/to/specs path/to/output
```

---

## Notes

- Generated code is committed so consumers don't need to run the generator.
- The SDK targets `net10.0` and has **no runtime dependencies** beyond the BCL.
- The CLI depends only on Sharprompt and `System.Security.Cryptography.ProtectedData`.
