# Copilot instructions

## Azure DevOps skill (MCP)

This repository ships a local MCP server (`azure-devops`) that lets you manage Azure DevOps
objects for the repo you are working in. Its tools are prefixed `azdo_`.

**Context is automatic.** The organization, project, and default repository are detected from this
repo's git remote, and credentials are reused from the `azdo` CLI sign-in (or `AZURE_DEVOPS_*`
environment variables). You usually do **not** need to ask the user for the org/project/repo.

- When unsure what the tools will act on, call **`azdo_get_context`** first and report the resolved
  organization / project / repository.
- For repository tools (`azdo_list_branches`, `azdo_list_pull_requests`, `azdo_get_pull_request`,
  `azdo_create_pull_request`), omit the `repository` argument to use the current repo; only pass it
  to target a different one.

### Choosing tools
- Browsing: `azdo_list_projects`, `azdo_list_repositories`, `azdo_list_branches`,
  `azdo_list_pull_requests`, `azdo_list_builds`, `azdo_list_pipelines`.
- Work items: `azdo_query_work_items` (WIQL) to find ids, then `azdo_get_work_item` for details.
  Create/update with `azdo_create_work_item` / `azdo_update_work_item`; discuss with
  `azdo_add_work_item_comment`.
- Pull requests: `azdo_create_pull_request` to open a PR from the branch you've been working on into
  the default branch.
- Pipelines: `azdo_list_pipelines` to find a definition id, then `azdo_run_pipeline`.

### Safety
Tools that **change state** — `azdo_create_pull_request`, `azdo_create_work_item`,
`azdo_update_work_item`, `azdo_add_work_item_comment`, `azdo_run_pipeline` — should be used only
after confirming intent with the user. Summarize what you are about to create/queue and proceed once
the user agrees. Read-only tools can be used freely to answer questions.
