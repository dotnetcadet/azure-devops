using System.Diagnostics;

namespace Azure.DevOps.Mcp;

/// <summary>The organization / project / repository inferred from a git remote URL.</summary>
public sealed record GitRemoteInfo(string Organization, string Project, string Repository);

/// <summary>
/// Parses Azure DevOps git remote URLs so the server can scope itself to the repository the
/// developer is currently working in, without any explicit configuration.
/// </summary>
public static class GitRemote
{
    /// <summary>Reads <c>remote.origin.url</c> for the given working directory and parses it.</summary>
    public static GitRemoteInfo? Detect(string workingDirectory)
    {
        var url = ReadOriginUrl(workingDirectory);
        return url is null ? null : Parse(url);
    }

    /// <summary>Parses an Azure DevOps remote URL into its org/project/repo, or null if it is not one.</summary>
    public static GitRemoteInfo? Parse(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        remoteUrl = remoteUrl.Trim();

        // SSH: git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
        if (remoteUrl.Contains("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var idx = remoteUrl.IndexOf("v3/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var parts = remoteUrl[(idx + 3)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return new GitRemoteInfo(parts[0], parts[1], parts[2]);
                }
            }

            return null;
        }

        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // https://dev.azure.com/{org}/{project}/_git/{repo}
        if (host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return FromGitPath(segments, orgFromPath: true);
        }

        // https://{org}.visualstudio.com[/DefaultCollection]/{project}/_git/{repo}
        if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var org = host[..host.IndexOf('.')];
            return FromGitPath(segments, orgFromPath: false, org: org);
        }

        return null;
    }

    private static GitRemoteInfo? FromGitPath(string[] segments, bool orgFromPath, string? org = null)
    {
        var gitIndex = Array.FindIndex(segments, s => s.Equals("_git", StringComparison.OrdinalIgnoreCase));
        if (gitIndex < 0 || gitIndex + 1 >= segments.Length)
        {
            return null;
        }

        var repo = segments[gitIndex + 1];
        var project = segments[gitIndex - 1];

        if (orgFromPath)
        {
            // segment 0 is the org for dev.azure.com
            org = segments.Length > 0 ? segments[0] : null;
        }

        return org is null ? null : new GitRemoteInfo(org, project, repo);
    }

    private static string? ReadOriginUrl(string workingDirectory)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "config --get remote.origin.url")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
