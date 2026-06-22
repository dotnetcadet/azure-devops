using Azure.DevOps.Cli.Configuration;
using Azure.DevOps.Cli.Ui;
using Azure.DevOps.Sdk.Models;
using Sharprompt;

namespace Azure.DevOps.Cli.Commands;

/// <summary>Interactive profile management: add/login, switch, and remove connections.</summary>
public static class ProfileCommands
{
    public static async Task LoginAsync(AzdoSession session)
    {
        ConsoleUx.Heading("Add or update a connection");

        var organization = Prompt.Input<string>(
            "Organization name, or full URL for on-prem/other (e.g. contoso, or https://tfs.contoso.com/DefaultCollection)",
            validators: new[] { Validators.Required() });

        var projectInput = Prompt.Input<string>("Default project (optional — Enter to skip)", defaultValue: string.Empty);
        var project = string.IsNullOrWhiteSpace(projectInput) ? null : projectInput.Trim();

        var token = Prompt.Password("Personal access token", validators: new[] { Validators.Required() });

        var name = Prompt.Input<string>("Profile name", defaultValue: organization,
            validators: new[] { Validators.Required() });

        ConsoleUx.Info("Verifying credentials against Azure DevOps...");
        try
        {
            var profile = new Profile
            {
                Name = name.Trim(),
                Organization = organization.Trim(),
                Project = project,
                ProtectedToken = SecretProtector.Protect(token),
            };

            using var client = AzdoSession.CreateClient(profile);
            var projects = await client.Core.Projects.ListAsync();
            session.Upsert(profile);
            ConsoleUx.Success($"Connected to '{organization}' — {projects.Count} project(s) visible. Saved profile '{profile.Name}'.");
            if (!SecretProtector.StrongProtectionAvailable)
            {
                ConsoleUx.Warn("DPAPI is unavailable on this OS; the token was stored with reversible encoding.");
            }
        }
        catch (VssServiceException ex)
        {
            ConsoleUx.Error($"Sign-in failed (HTTP {(int)ex.StatusCode}): {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            ConsoleUx.Error($"Network error reaching Azure DevOps: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            ConsoleUx.Error("Verification timed out or was cancelled.");
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            ConsoleUx.Error($"Could not protect the token: {ex.Message}");
        }
    }

    public static void Switch(AzdoSession session)
    {
        if (session.Profiles.Count == 0)
        {
            ConsoleUx.Warn("No profiles yet — add one first.");
            return;
        }

        var profile = Prompt.Select("Select the active profile", session.Profiles, textSelector: p => p.Display);
        session.SetCurrent(profile);
        ConsoleUx.Success($"Now using '{profile.Name}' ({profile.Organization}).");
    }

    public static void Remove(AzdoSession session)
    {
        if (session.Profiles.Count == 0)
        {
            ConsoleUx.Warn("No profiles to remove.");
            return;
        }

        var profile = Prompt.Select("Select a profile to remove", session.Profiles, textSelector: p => p.Display);
        if (Prompt.Confirm($"Remove profile '{profile.Name}'?", defaultValue: false))
        {
            session.Remove(profile);
            ConsoleUx.Success($"Removed '{profile.Name}'.");
        }
    }

    public static void List(AzdoSession session)
    {
        ConsoleUx.Heading("Profiles");
        var rows = session.Profiles
            .Select(p => (IReadOnlyList<string?>)new[]
            {
                p.Name == session.Current?.Name ? "● " + p.Name : "  " + p.Name,
                p.Organization,
                p.Project ?? "-",
            })
            .ToList();
        ConsoleUx.Table(new[] { "Profile", "Organization", "Default project" }, rows);
        ConsoleUx.Info($"Config: {session.StoreLocation}");
    }
}
