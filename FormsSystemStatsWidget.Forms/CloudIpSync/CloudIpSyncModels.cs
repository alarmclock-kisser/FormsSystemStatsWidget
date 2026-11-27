using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    internal enum CloudIpSyncProvider
    {
        GoogleDrive,
        OneDrive,
        LocalDirectory
    }

    internal static class CloudIpSyncProviderInfo
    {
        public const string GoogleDriveStorageValue = "GoogleDrive";
        public const string OneDriveStorageValue = "OneDrive";
        public const string LocalDirectoryStorageValue = "LocalDirectory";

        public static CloudIpSyncProvider Parse(string? value)
        {
            if (string.Equals(value, OneDriveStorageValue, StringComparison.OrdinalIgnoreCase))
            {
                return CloudIpSyncProvider.OneDrive;
            }

            return string.Equals(value, LocalDirectoryStorageValue, StringComparison.OrdinalIgnoreCase)
                ? CloudIpSyncProvider.LocalDirectory
                : CloudIpSyncProvider.GoogleDrive;
        }

        public static string ToStorageValue(this CloudIpSyncProvider provider)
        {
            return provider switch
            {
                CloudIpSyncProvider.OneDrive => OneDriveStorageValue,
                CloudIpSyncProvider.LocalDirectory => LocalDirectoryStorageValue,
                _ => GoogleDriveStorageValue
            };
        }

        public static string GetDisplayName(this CloudIpSyncProvider provider)
        {
            return provider switch
            {
                CloudIpSyncProvider.OneDrive => "Microsoft OneDrive",
                CloudIpSyncProvider.LocalDirectory => "Local directory (sync)",
                _ => "Google Drive"
            };
        }

        public static bool IsLocalDirectory(this CloudIpSyncProvider provider)
        {
            return provider == CloudIpSyncProvider.LocalDirectory;
        }

        public static string GetAuthorizationEndpoint(this CloudIpSyncProvider provider, string? tenantId = null)
        {
            return provider == CloudIpSyncProvider.OneDrive
                ? "https://login.microsoftonline.com/" + NormalizeTenant(tenantId) + "/oauth2/v2.0/authorize"
                : "https://accounts.google.com/o/oauth2/v2/auth";
        }

        public static string GetTokenEndpoint(this CloudIpSyncProvider provider, string? tenantId = null)
        {
            return provider == CloudIpSyncProvider.OneDrive
                ? "https://login.microsoftonline.com/" + NormalizeTenant(tenantId) + "/oauth2/v2.0/token"
                : "https://oauth2.googleapis.com/token";
        }

        public static string NormalizeTenant(string? tenantId)
        {
            string tenant = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId.Trim();
            return Uri.EscapeDataString(tenant);
        }

        public static string GetScope(this CloudIpSyncProvider provider)
        {
            // drive.file only grants access to files created by this application.
            // openid/email/profile are used solely to show which account is connected.
            return provider == CloudIpSyncProvider.OneDrive
                ? "openid profile email offline_access Files.ReadWrite User.Read"
                : "openid email profile https://www.googleapis.com/auth/drive.file";
        }

        public static bool RequiresClientSecret(this CloudIpSyncProvider provider)
        {
            // Google "Desktop app" OAuth clients always have a client secret.
            // Microsoft desktop/public clients can be configured without one.
            return provider == CloudIpSyncProvider.GoogleDrive;
        }

        public static string GetConsoleUrl(this CloudIpSyncProvider provider)
        {
            return provider == CloudIpSyncProvider.OneDrive
                ? "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade"
                : "https://console.cloud.google.com/apis/credentials";
        }

        public static string GetSetupHint(this CloudIpSyncProvider provider)
        {
            if (provider.IsLocalDirectory())
            {
                return "No OAuth sign-in is needed for a local directory. FSSWidget writes the .ipdata "
                    + "file directly into the folder below. For other PCs to receive the updates, the folder "
                    + "should be synchronized by a cloud client already installed on this machine "
                    + "(e.g. the OneDrive or Google Drive sync folder), or mirrored by another tool. "
                    + "The folder is created automatically if it does not exist yet.";
            }

            return provider == CloudIpSyncProvider.OneDrive
                ? "Create an app registration in Microsoft Entra ID. Choose an account type that "
                  + "includes personal Microsoft accounts when using OneDrive personal. Under "
                  + "Authentication add the \"Mobile and desktop applications\" platform with "
                  + "http://localhost as redirect URI. Paste the application (client) ID. A client "
                  + "secret is optional for public clients. For a single-tenant app, enter its "
                  + "Directory (tenant) ID below instead of common. Microsoft's Files.ReadWrite scope "
                  + "covers your OneDrive; FSSWidget only writes its own <HostPCName>.ipdata file."
                : "Create an OAuth client of type \"Desktop app\" in Google Cloud Console and enable the "
                  + "Google Drive API. Paste its client ID and client secret. FSSWidget only accesses "
                  + "files it creates itself (drive.file scope). Publish the OAuth consent screen to "
                  + "\"In production\": refresh tokens for apps left in \"Testing\" expire after 7 days.";
        }
    }

    internal sealed class CloudIpSyncOptions
    {
        public CloudIpSyncProvider Provider { get; set; } = CloudIpSyncProvider.GoogleDrive;

        public string TenantId { get; set; } = "common";

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public DateTime AccessTokenExpiresUtc { get; set; } = DateTime.MinValue;

        public string AccountDisplayName { get; set; } = string.Empty;

        // Local (sync) directory target, used only when Provider == LocalDirectory.
        public string LocalDirectory { get; set; } = string.Empty;

        public string LocalFileName { get; set; } = string.Empty;

        public CloudIpSyncOptions Clone()
        {
            return (CloudIpSyncOptions) this.MemberwiseClone();
        }
    }

    internal sealed record CloudOAuthTokenResult(
        string AccessToken,
        string? RefreshToken,
        DateTime ExpiresUtc,
        string? Scope);

    internal enum CloudIpSyncState
    {
        Disabled,
        Starting,
        Syncing,
        Synced,
        Error,
        NeedsConfiguration,
        NeedsSignIn
    }

    internal sealed record CloudIpSyncStatus(
        CloudIpSyncState State,
        string Message,
        DateTime? LastUploadUtc = null)
    {
        public static CloudIpSyncStatus Disabled { get; } = new(CloudIpSyncState.Disabled, "Off");
    }

    internal sealed class CloudOAuthException : Exception
    {
        public CloudOAuthException(string message, string? errorCode = null, Exception? innerException = null)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        public string? ErrorCode { get; }

        public bool IsInvalidGrant
        {
            get
            {
                return string.Equals(this.ErrorCode, "invalid_grant", StringComparison.OrdinalIgnoreCase)
                    || (this.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
                    || (this.Message.Contains("AADSTS70008", StringComparison.OrdinalIgnoreCase))
                    || (this.Message.Contains("expired", StringComparison.OrdinalIgnoreCase)
                        && this.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>
    /// Verschlüsselt Zugangsdaten mit Windows DPAPI für den aktuellen Benutzer.
    /// Die JSON-Datei enthält dadurch nur Base64-Blobs, niemals Klartext-Passwörter.
    /// </summary>
    internal static class ProtectedSecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FormsSystemStatsWidget.CloudIpSync.v1");

        public static string Protect(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
                try
                {
                    byte[] encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                    return Convert.ToBase64String(encrypted);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plainBytes);
                }
            }
            catch (Exception ex)
            {
                // Never log the value itself.
                Logger.Log($"[CloudIpSync] Failed to protect a credential: {ex.GetType().Name}");
                return string.Empty;
            }
        }

        public static string Unprotect(string? protectedBase64)
        {
            if (string.IsNullOrWhiteSpace(protectedBase64))
            {
                return string.Empty;
            }

            try
            {
                byte[] encrypted = Convert.FromBase64String(protectedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                try
                {
                    return Encoding.UTF8.GetString(plainBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plainBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[CloudIpSync] Failed to unprotect a credential: {ex.GetType().Name}");
                return string.Empty;
            }
        }
    }

    internal static class CloudIpSyncSettingsMapper
    {
        public static CloudIpSyncOptions FromPersistent(WidgetPersistentSettings settings)
        {
            return new CloudIpSyncOptions
            {
                Provider = CloudIpSyncProviderInfo.Parse(settings.CloudIpSyncProvider),
                TenantId = string.IsNullOrWhiteSpace(settings.CloudIpSyncTenant) ? "common" : settings.CloudIpSyncTenant,
                ClientId = settings.CloudIpSyncClientId ?? string.Empty,
                ClientSecret = ProtectedSecretStore.Unprotect(settings.CloudIpSyncClientSecretProtected),
                RefreshToken = ProtectedSecretStore.Unprotect(settings.CloudIpSyncRefreshTokenProtected),
                AccessToken = ProtectedSecretStore.Unprotect(settings.CloudIpSyncAccessTokenProtected),
                AccessTokenExpiresUtc = settings.CloudIpSyncAccessTokenExpiresUtc,
                AccountDisplayName = settings.CloudIpSyncAccount ?? string.Empty,
                LocalDirectory = settings.CloudIpSyncLocalDirectory ?? string.Empty,
                LocalFileName = settings.CloudIpSyncLocalFileName ?? string.Empty
            };
        }

        public static void ApplyToPersistent(WidgetPersistentSettings settings, CloudIpSyncOptions options)
        {
            settings.CloudIpSyncProvider = options.Provider.ToStorageValue();
            settings.CloudIpSyncTenant = string.IsNullOrWhiteSpace(options.TenantId) ? "common" : options.TenantId.Trim();
            settings.CloudIpSyncClientId = options.ClientId?.Trim() ?? string.Empty;
            settings.CloudIpSyncClientSecretProtected = ProtectedSecretStore.Protect(options.ClientSecret);
            settings.CloudIpSyncRefreshTokenProtected = ProtectedSecretStore.Protect(options.RefreshToken);
            settings.CloudIpSyncAccessTokenProtected = ProtectedSecretStore.Protect(options.AccessToken);
            settings.CloudIpSyncAccessTokenExpiresUtc = options.AccessTokenExpiresUtc;
            settings.CloudIpSyncAccount = options.AccountDisplayName ?? string.Empty;
            settings.CloudIpSyncLocalDirectory = options.LocalDirectory?.Trim() ?? string.Empty;
            settings.CloudIpSyncLocalFileName = options.LocalFileName?.Trim() ?? string.Empty;
        }

        public static bool IsConfigured(WidgetPersistentSettings settings)
        {
            return IsConfigured(FromPersistent(settings));
        }

        public static bool IsConfigured(CloudIpSyncOptions options)
        {
            if (options.Provider.IsLocalDirectory())
            {
                return !string.IsNullOrWhiteSpace(options.LocalDirectory);
            }

            return !string.IsNullOrWhiteSpace(options.ClientId)
                && !string.IsNullOrWhiteSpace(options.RefreshToken)
                && (!options.Provider.RequiresClientSecret() || !string.IsNullOrWhiteSpace(options.ClientSecret));
        }
    }

    internal static class CloudIpDataFormatter
    {
        public static string Build(string hostName, string? ipv4, string? ipv6, DateTime utcNow)
        {
            string timestamp = utcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            return "Host: " + hostName + "\n"
                + "Updated (UTC): " + timestamp + "\n"
                + "WAN IPv4: " + NormalizeAddress(ipv4) + "\n"
                + "WAN IPv6: " + NormalizeAddress(ipv6) + "\n";
        }

        private static string NormalizeAddress(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Trim();
        }
    }

    /// <summary>
    /// Hit-testing for the context-menu entry that both toggles and opens a submenu.
    /// The item body toggles the check state; the arrow at the right edge opens the submenu.
    /// </summary>
    internal static class CloudIpSyncMenuHitTest
    {
        private const int ArrowWidthDip = 20;

        public static bool IsToggleClick(Rectangle itemScreenBounds, Point cursor, int deviceDpi)
        {
            if (!itemScreenBounds.Contains(cursor))
            {
                return false;
            }

            int dpi = deviceDpi <= 0 ? 96 : deviceDpi;
            int arrowWidth = Math.Max(18, (int) Math.Round(ArrowWidthDip * dpi / 96.0));
            return cursor.X < itemScreenBounds.Right - arrowWidth;
        }
    }

    internal static class CloudIpSyncHelpers
    {
        public static string GetHostName()
        {
            try
            {
                string dnsName = Dns.GetHostName();
                if (!string.IsNullOrWhiteSpace(dnsName))
                {
                    return dnsName.Trim();
                }
            }
            catch
            {
                // Fall back to the environment name.
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.MachineName))
                {
                    return Environment.MachineName.Trim();
                }
            }
            catch
            {
                // Ignore and use a fixed fallback.
            }

            return "FSSWidget";
        }

        public static string GetIpDataFileName(string? hostName = null)
        {
            return SanitizeFileName(hostName ?? GetHostName()) + ".ipdata";
        }

        public static string SanitizeFileName(string? name)
        {
            string candidate = (name ?? string.Empty).Trim();
            if (candidate.Length == 0)
            {
                return "FSSWidget";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(candidate.Length);
            foreach (char ch in candidate)
            {
                bool isInvalid = char.IsControl(ch) || Array.IndexOf(invalid, ch) >= 0;
                builder.Append(isInvalid ? '_' : ch);
            }

            string result = builder.ToString().Trim().Trim(' ', '.');
            if (result.Length == 0)
            {
                result = "FSSWidget";
            }

            return result.Length > 100 ? result[..100] : result;
        }
    }
}
