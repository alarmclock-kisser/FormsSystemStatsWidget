using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    /// <summary>
    /// OAuth 2.0 browser flow with a temporary loopback listener.
    /// No account password ever reaches FSSWidget; only OAuth tokens are stored (DPAPI protected).
    /// </summary>
    internal static class CloudOAuthClient
    {
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FSSWidget/1.4");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public static async Task<CloudOAuthTokenResult> SignInAsync(
            CloudIpSyncProvider provider,
            string tenantId,
            string clientId,
            string? clientSecret,
            IProgress<string>? status,
            IProgress<string>? authorizationUrl,
            CancellationToken cancellationToken)
        {
            clientId = (clientId ?? string.Empty).Trim();
            clientSecret = (clientSecret ?? string.Empty).Trim();

            if (clientId.Length == 0)
            {
                throw new CloudOAuthException("An OAuth client ID is required.");
            }

            if (provider.RequiresClientSecret() && clientSecret.Length == 0)
            {
                throw new CloudOAuthException("Google Drive requires the OAuth client secret.");
            }

            using var listener = LoopbackCallbackListener.Start();
            string redirectUri = listener.RedirectUri;
            string state = CreateRandomUrlToken(24);
            string codeVerifier = CreateRandomUrlToken(48);
            string codeChallenge = CreateCodeChallenge(codeVerifier);

            string url = BuildAuthorizationUrl(provider, clientId, redirectUri, state, codeChallenge, tenantId);
            authorizationUrl?.Report(url);

            status?.Report("Opening the default browser...");
            bool browserOpened = TryOpenBrowser(url);
            status?.Report(browserOpened
                ? "Waiting for sign-in in the browser..."
                : "Could not open the browser automatically. Copy the sign-in link and open it manually.");

            OAuthCallbackResult callback = await listener
                .WaitForCallbackAsync(state, TimeSpan.FromMinutes(10), cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(callback.Error))
            {
                string message = string.IsNullOrWhiteSpace(callback.ErrorDescription)
                    ? callback.Error
                    : callback.Error + ": " + callback.ErrorDescription;
                throw new CloudOAuthException("Sign-in failed: " + message, callback.Error);
            }

            if (string.IsNullOrWhiteSpace(callback.Code))
            {
                throw new CloudOAuthException("Sign-in failed: the provider did not return an authorization code.");
            }

            status?.Report("Exchanging the authorization code...");
            CloudOAuthTokenResult result = await ExchangeCodeAsync(
                provider,
                tenantId,
                clientId,
                clientSecret,
                redirectUri,
                callback.Code,
                codeVerifier,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                throw new CloudOAuthException(
                    "The provider did not return a refresh token. Remove FSSWidget from the provider's "
                    + "authorized apps and sign in again, or paste an existing refresh token manually.");
            }

            return result;
        }

        public static async Task<CloudOAuthTokenResult> RefreshAsync(
            CloudIpSyncOptions options,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.RefreshToken))
            {
                throw new CloudOAuthException("No refresh token is stored. Please sign in again.", "missing_refresh_token");
            }

            var form = new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = options.RefreshToken
            };

            if (!string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                form["client_secret"] = options.ClientSecret;
            }

            if (options.Provider == CloudIpSyncProvider.OneDrive)
            {
                form["scope"] = options.Provider.GetScope();
            }

            CloudOAuthTokenResult result = await RequestTokenAsync(
                options.Provider,
                options.TenantId,
                form,
                cancellationToken).ConfigureAwait(false);

            // Some providers rotate the refresh token; keep the previous one if none is returned.
            return result with
            {
                RefreshToken = string.IsNullOrWhiteSpace(result.RefreshToken) ? options.RefreshToken : result.RefreshToken
            };
        }

        public static async Task<string> GetAccountDisplayNameAsync(
            CloudIpSyncProvider provider,
            string accessToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return string.Empty;
            }

            try
            {
                string url = provider == CloudIpSyncProvider.OneDrive
                    ? "https://graph.microsoft.com/v1.0/me?$select=displayName,userPrincipalName,mail"
                    : "https://www.googleapis.com/oauth2/v3/userinfo";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using HttpResponseMessage response = await SharedHttpClient
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;

                if (provider == CloudIpSyncProvider.OneDrive)
                {
                    return GetString(root, "mail")
                        ?? GetString(root, "userPrincipalName")
                        ?? GetString(root, "displayName")
                        ?? string.Empty;
                }

                return GetString(root, "email")
                    ?? GetString(root, "name")
                    ?? string.Empty;
            }
            catch
            {
                // The account name is cosmetic; never fail sign-in because of it.
                return string.Empty;
            }
        }

        internal static string BuildAuthorizationUrl(
            CloudIpSyncProvider provider,
            string clientId,
            string redirectUri,
            string state,
            string codeChallenge,
            string? tenantId = null)
        {
            var query = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = provider.GetScope(),
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256"
            };

            if (provider == CloudIpSyncProvider.GoogleDrive)
            {
                query["access_type"] = "offline";
                // "select_account" shows the already-logged-in Google accounts (chooser)
                // instead of silently reusing the last consent flow.
                query["prompt"] = "select_account";
                query["include_granted_scopes"] = "true";
            }
            else
            {
                query["response_mode"] = "query";
                query["prompt"] = "select_account";
            }

            string queryString = string.Join(
                "&",
                query.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
            return provider.GetAuthorizationEndpoint(tenantId) + "?" + queryString;
        }

        internal static string CreateRandomUrlToken(int byteCount)
        {
            return Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));
        }

        internal static string CreateCodeChallenge(string codeVerifier)
        {
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static async Task<CloudOAuthTokenResult> ExchangeCodeAsync(
            CloudIpSyncProvider provider,
            string tenantId,
            string clientId,
            string clientSecret,
            string redirectUri,
            string code,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            };

            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                form["client_secret"] = clientSecret;
            }

            if (provider == CloudIpSyncProvider.OneDrive)
            {
                form["scope"] = provider.GetScope();
            }

            return await RequestTokenAsync(provider, tenantId, form, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<CloudOAuthTokenResult> RequestTokenAsync(
            CloudIpSyncProvider provider,
            string tenantId,
            Dictionary<string, string> form,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, provider.GetTokenEndpoint(tenantId))
            {
                Content = new FormUrlEncodedContent(form)
            };

            using HttpResponseMessage response = await SharedHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                throw new CloudOAuthException(
                    $"The token endpoint returned HTTP {(int) response.StatusCode} with an unreadable response.");
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (!response.IsSuccessStatusCode)
                {
                    string error = GetString(root, "error") ?? "HTTP " + (int) response.StatusCode;
                    string description = GetString(root, "error_description")
                        ?? GetString(root, "error_codes")
                        ?? "The token request was rejected.";
                    throw new CloudOAuthException(BuildSafeOAuthMessage(error, description), error);
                }

                string? accessToken = GetString(root, "access_token");
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    throw new CloudOAuthException("The token response did not contain an access token.");
                }

                int expiresIn = 3600;
                if (root.TryGetProperty("expires_in", out JsonElement expiresElement))
                {
                    if (expiresElement.ValueKind == JsonValueKind.Number
                        && expiresElement.TryGetInt32(out int numericSeconds))
                    {
                        expiresIn = numericSeconds;
                    }
                    else if (expiresElement.ValueKind == JsonValueKind.String
                        && int.TryParse(expiresElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSeconds))
                    {
                        expiresIn = parsedSeconds;
                    }
                }

                expiresIn = Math.Clamp(expiresIn, 60, 24 * 3600);
                string? refreshToken = GetString(root, "refresh_token");
                string? scope = GetString(root, "scope");
                return new CloudOAuthTokenResult(
                    accessToken,
                    refreshToken,
                    DateTime.UtcNow.AddSeconds(expiresIn),
                    scope);
            }
        }

        private static string BuildSafeOAuthMessage(string error, string description)
        {
            string message = string.IsNullOrWhiteSpace(description)
                ? error
                : error + ": " + description;
            message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return message.Length > 400 ? message[..400] : message;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(item => item.ToString())),
                _ => null
            };
        }

        private static bool TryOpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[CloudIpSync] Could not open the default browser: {ex.Message}");
                return false;
            }
        }
    }

    internal sealed record OAuthCallbackResult(string? Code, string? Error, string? ErrorDescription);

    /// <summary>
    /// Minimal loopback HTTP listener for the OAuth redirect. It binds IPv4 and IPv6 loopback
    /// so http://localhost:port works regardless of how the browser resolves localhost.
    /// </summary>
    internal sealed class LoopbackCallbackListener : IDisposable
    {
        private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);

        private readonly TcpListener _listener4;
        private readonly TcpListener? _listener6;
        private readonly Channel<TcpClient> _clients = Channel.CreateUnbounded<TcpClient>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly CancellationTokenSource _acceptCts = new();
        private readonly Task _accept4Task;
        private readonly Task? _accept6Task;
        private bool _disposed;

        private LoopbackCallbackListener(TcpListener listener4, TcpListener? listener6, int port)
        {
            this._listener4 = listener4;
            this._listener6 = listener6;
            this.Port = port;
            // No trailing slash: this is the form both Google and Microsoft document
            // for desktop loopback redirect URIs. The browser still requests "/".
            this.RedirectUri = $"http://localhost:{port}";
            this._accept4Task = this.AcceptLoopAsync(listener4);
            if (listener6 != null)
            {
                this._accept6Task = this.AcceptLoopAsync(listener6);
            }
        }

        public int Port { get; }

        public string RedirectUri { get; }

        public static LoopbackCallbackListener Start()
        {
            var listener4 = new TcpListener(IPAddress.Loopback, 0);
            listener4.Start();

            int port = ((IPEndPoint) listener4.LocalEndpoint).Port;
            TcpListener? listener6 = null;
            try
            {
                listener6 = new TcpListener(IPAddress.IPv6Loopback, port);
                listener6.Start();
            }
            catch
            {
                try { listener6?.Stop(); } catch { }
                listener6 = null;
            }

            return new LoopbackCallbackListener(listener4, listener6, port);
        }

        public async Task<OAuthCallbackResult> WaitForCallbackAsync(
            string expectedState,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                while (true)
                {
                    TcpClient client = await this._clients.Reader
                        .ReadAsync(timeoutCts.Token)
                        .ConfigureAwait(false);
                    using (client)
                    {
                        try
                        {
                            OAuthCallbackResult? result = await HandleClientAsync(
                                client,
                                expectedState,
                                timeoutCts.Token).ConfigureAwait(false);
                            if (result != null)
                            {
                                return result;
                            }
                        }
                        catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
                        {
                            // Per-request read timeout (for example a stalled speculative
                            // connection). Keep waiting for the real callback.
                        }
                        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                        {
                            // Browsers open speculative connections that may disappear before
                            // sending a request. Keep waiting for the real callback.
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The browser sign-in timed out. Please try again.");
            }
        }

        private async Task AcceptLoopAsync(TcpListener listener)
        {
            while (!this._acceptCts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(this._acceptCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }

                if (!this._clients.Writer.TryWrite(client))
                {
                    client.Dispose();
                    break;
                }
            }
        }

        private static async Task<OAuthCallbackResult?> HandleClientAsync(
            TcpClient client,
            string expectedState,
            CancellationToken cancellationToken)
        {
            NetworkStream stream = client.GetStream();
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(RequestReadTimeout);

            string? requestLine;
            using (var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true))
            {
                requestLine = await reader.ReadLineAsync(readCts.Token).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return null;
            }

            string[] requestParts = requestLine.Split(' ');
            if (requestParts.Length < 2
                || !string.Equals(requestParts[0], "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad request",
                    BuildHtml("Sign-in failed", "The local callback received an invalid request."),
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            string target = requestParts[1];
            int queryStart = target.IndexOf('?');
            string path = queryStart >= 0 ? target[..queryStart] : target;
            if (!string.Equals(path, "/", StringComparison.Ordinal)
                && !string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(
                    stream,
                    404,
                    "Not found",
                    BuildHtml("Not found", "This local callback URL is not valid."),
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            Dictionary<string, string> query = ParseQuery(
                queryStart >= 0 ? target[(queryStart + 1)..] : string.Empty);

            if (!query.TryGetValue("state", out string? state)
                || !FixedTimeEquals(state, expectedState))
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Invalid state",
                    BuildHtml("Sign-in failed", "The sign-in response did not belong to this request."),
                    cancellationToken).ConfigureAwait(false);
                return new OAuthCallbackResult(
                    null,
                    "invalid_state",
                    "The sign-in response did not match this request. Please try again.");
            }

            if (query.TryGetValue("error", out string? error) && !string.IsNullOrWhiteSpace(error))
            {
                query.TryGetValue("error_description", out string? errorDescription);
                await WriteResponseAsync(
                    stream,
                    400,
                    "Sign-in failed",
                    BuildHtml("Sign-in failed", errorDescription ?? error),
                    cancellationToken).ConfigureAwait(false);
                return new OAuthCallbackResult(null, error, errorDescription);
            }

            if (!query.TryGetValue("code", out string? code) || string.IsNullOrWhiteSpace(code))
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Missing code",
                    BuildHtml("Sign-in failed", "The provider did not return an authorization code."),
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            await WriteResponseAsync(
                stream,
                200,
                "OK",
                BuildHtml("Sign-in complete", "FSSWidget is now connected. You can close this tab."),
                cancellationToken).ConfigureAwait(false);
            return new OAuthCallbackResult(code, null, null);
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string html,
            CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(html);
            string headers = "HTTP/1.1 " + statusCode + " " + statusText + "\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + "Content-Length: " + body.Length + "\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

            try
            {
                await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // The browser tab may already be gone; the OAuth result is still valid.
            }
        }

        private static string BuildHtml(string title, string message)
        {
            string safeTitle = WebUtility.HtmlEncode(title);
            string safeMessage = WebUtility.HtmlEncode(message);
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + safeTitle + "</title></head>"
                + "<body style=\"margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;"
                + "background:#111;color:#eee;font-family:Segoe UI,Arial,sans-serif\">"
                + "<div style=\"max-width:560px;padding:28px 32px;border:1px solid #333;border-radius:12px;background:#1b1b1b\">"
                + "<h2 style=\"margin:0 0 12px\">" + safeTitle + "</h2>"
                + "<p style=\"margin:0 0 18px;line-height:1.5\">" + safeMessage + "</p>"
                + "<p style=\"margin:0;color:#999\">You can close this browser tab and return to FSSWidget.</p>"
                + "</div></body></html>";
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf('=');
                string key = separator >= 0 ? pair[..separator] : pair;
                string value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
                try
                {
                    key = Uri.UnescapeDataString(key.Replace("+", " "));
                    value = Uri.UnescapeDataString(value.Replace("+", " "));
                }
                catch
                {
                    // Keep the raw values if they are not valid percent-encoded strings.
                }

                result[key] = value;
            }

            return result;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] leftBytes = Encoding.UTF8.GetBytes(left);
            byte[] rightBytes = Encoding.UTF8.GetBytes(right);
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            try { this._acceptCts.Cancel(); } catch { }
            try { this._listener4.Stop(); } catch { }
            try { this._listener6?.Stop(); } catch { }

            while (this._clients.Reader.TryRead(out TcpClient? client))
            {
                client.Dispose();
            }

            try { this._accept4Task.Wait(TimeSpan.FromSeconds(1)); } catch { }
            try { this._accept6Task?.Wait(TimeSpan.FromSeconds(1)); } catch { }
            this._acceptCts.Dispose();
        }
    }
}
