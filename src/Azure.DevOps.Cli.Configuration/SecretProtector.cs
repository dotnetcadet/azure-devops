using System.Security.Cryptography;
using System.Text;

namespace Azure.DevOps.Cli.Configuration;

/// <summary>
/// Protects personal access tokens at rest. On Windows it uses DPAPI (per-user encryption);
/// on other platforms it falls back to a reversible encoding and warns the user, so the tool
/// still works cross-platform while never writing a bare token by default on Windows.
/// </summary>
public static class SecretProtector
{
    private const string DpapiPrefix = "dpapi:";
    private const string PlainPrefix = "plain:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Azure.DevOps.Cli/v1");

    /// <summary><c>true</c> when strong (DPAPI) protection is available on this platform.</summary>
    public static bool StrongProtectionAvailable => OperatingSystem.IsWindows();

    public static string Protect(string token)
    {
        if (OperatingSystem.IsWindows())
        {
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            return DpapiPrefix + Convert.ToBase64String(encrypted);
        }

        return PlainPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
    }

    public static string Unprotect(string protectedToken)
    {
        if (protectedToken.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("This token was protected with Windows DPAPI and cannot be read on this platform.");
            }

            var data = Convert.FromBase64String(protectedToken[DpapiPrefix.Length..]);
            var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        if (protectedToken.StartsWith(PlainPrefix, StringComparison.Ordinal))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedToken[PlainPrefix.Length..]));
        }

        // Backwards/forgiving: treat as already-plain.
        return protectedToken;
    }
}
