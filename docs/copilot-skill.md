# Azure DevOps skill for GitHub Copilot

`src/Azure.DevOps.Mcp` is a local [Model Context Protocol](https://modelcontextprotocol.io) (MCP)
server that turns the Azure DevOps SDK into a **GitHub Copilot skill**. Once installed, Copilot can
list and manage Azure DevOps objects (repositories, branches, pull requests, work items, builds and
pipelines) for the repository you're working in.

It's a normal local process — there is no cloud service to register and nothing leaves your machine
except the Azure DevOps REST calls the SDK already makes.

## What it can do

`azdo_get_context`, `azdo_list_projects`, `azdo_list_repositories`, `azdo_list_branches`,
`azdo_list_pull_requests`, `azdo_get_pull_request`, `azdo_create_pull_request`,
`azdo_query_work_items` (WIQL), `azdo_get_work_item`, `azdo_create_work_item`,
`azdo_update_work_item`, `azdo_add_work_item_comment`, `azdo_list_pipelines`, `azdo_run_pipeline`,
`azdo_list_builds`, `azdo_get_build`.

## How it finds your org / project / repo / credentials

Resolved automatically, most-specific first, so the skill is **repo-aware** with zero config:

1. **Environment variables** — `AZURE_DEVOPS_ORG`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PAT`
   (also accepts `AZDO_ORG` / `AZDO_PROJECT` / `AZDO_PAT`). For Azure DevOps Server (on-prem) or any
   non-`dev.azure.com` host, set `AZURE_DEVOPS_ORG_URL` to the full collection URL
   (e.g. `https://tfs.contoso.com/DefaultCollection`).
2. **The repo's git remote** — org/project/repository are parsed from `origin` (dev.azure.com or
   *.visualstudio.com, HTTPS or SSH).
3. **The `azdo` CLI sign-in** — the organization/project and the (DPAPI-protected) PAT saved by the
   `azdo` CLI are reused.

So the easiest setup is: **sign in once with the `azdo` CLI**, then the skill just works in any
Azure DevOps repo you open.

## Install (one time)

```bash
# 1) Build the solution
dotnet build azure-devops.slnx -c Release

# 2) (Recommended) sign in once so the skill can reuse your credentials
dotnet run --project src/Azure.DevOps.Cli   # choose "Profile — add / sign in"

# 3) Pack and install the MCP server as a global .NET tool
dotnet pack src/Azure.DevOps.Mcp -c Release
dotnet tool install --global --add-source ./src/Azure.DevOps.Mcp/nupkg Azure.DevOps.Mcp
```

This puts an `azure-devops-mcp` command on your PATH (`~/.dotnet/tools`). To update later:
`dotnet tool update --global --add-source ./src/Azure.DevOps.Mcp/nupkg Azure.DevOps.Mcp`.

## Use it in VS Code (Copilot agent mode)

A workspace config is already committed at [`.vscode/mcp.json`](../.vscode/mcp.json):

```json
{
  "servers": {
    "azure-devops": { "type": "stdio", "command": "azure-devops-mcp" }
  }
}
```

1. Open the repo in VS Code, open Copilot Chat, and switch to **Agent** mode.
2. Start the `azure-devops` server when prompted (or via the MCP servers list).
3. Ask things like *"list my active pull requests"* or *"create a bug titled 'Login fails on Safari'"*.

To use it in **another** repository, drop the same `.vscode/mcp.json` there (the global
`azure-devops-mcp` command works anywhere). To pass an explicit PAT instead of reusing the `azdo`
login, use VS Code's secret input:

```json
{
  "inputs": [
    { "type": "promptString", "id": "azdo-pat", "description": "Azure DevOps PAT", "password": true }
  ],
  "servers": {
    "azure-devops": {
      "type": "stdio",
      "command": "azure-devops-mcp",
      "env": { "AZURE_DEVOPS_ORG": "your-org", "AZURE_DEVOPS_PAT": "${input:azdo-pat}" }
    }
  }
}
```

## Use it in the GitHub Copilot CLI

A project config is committed at [`.mcp.json`](../.mcp.json) (the CLI also reads
`~/.copilot/mcp-config.json`). Note the CLI's schema differs from VS Code — it uses `mcpServers`
and `type: "local"`:

```json
{
  "mcpServers": {
    "azure-devops": { "type": "local", "command": "azure-devops-mcp", "tools": ["*"], "env": {} }
  }
}
```

## Example prompts

- "What's the default branch of this repo and what PRs are open against it?"
- "Open a pull request from my current branch into main titled 'Add retry handler'."
- "Find active bugs assigned to me." → runs a WIQL query, then fetches details.
- "Create a task 'Write integration tests' and assign it to me."
- "Queue the CI pipeline on this branch."

## Troubleshooting

- *Tools don't appear*: ensure `azure-devops-mcp` is on your PATH (`~/.dotnet/tools`) and reload the
  MCP server. Don't point the config at `dotnet run` — its build output would corrupt the protocol;
  always use the installed command (or the built DLL).
- *"No Azure DevOps connection is configured"*: run `azdo` to sign in, or set `AZURE_DEVOPS_ORG` and
  `AZURE_DEVOPS_PAT`. Use the `azdo_get_context` tool to see what was resolved and from where.
