using System.Net.Http.Headers;
using System.Text;

namespace Azure.DevOps.Sdk.Authentication;

/// <summary>
/// Authenticates requests using an Azure DevOps Personal Access Token (PAT).
/// </summary>
/// <remarks>
/// Azure DevOps expects the PAT to be sent using HTTP Basic authentication with an
/// empty username and the token as the password, i.e.
/// <c>Authorization: Basic base64(":" + pat)</c>.
/// </remarks>
public sealed class PatCredential : IAzureDevOpsCredential
{
    private readonly string _headerParameter;

    /// <summary>
    /// Initializes a new <see cref="PatCredential"/> from a personal access token.
    /// </summary>
    /// <param name="personalAccessToken">The personal access token.</param>
    /// <exception cref="ArgumentException">Thrown when the token is null or whitespace.</exception>
    public PatCredential(string personalAccessToken)
    {
        if (string.IsNullOrWhiteSpace(personalAccessToken))
        {
            throw new ArgumentException("A personal access token is required.", nameof(personalAccessToken));
        }

        _headerParameter = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + personalAccessToken));
    }

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _headerParameter);
        return ValueTask.CompletedTask;
    }
}
