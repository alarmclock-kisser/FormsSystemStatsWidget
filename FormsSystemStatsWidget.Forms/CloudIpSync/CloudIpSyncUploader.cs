using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed record WanIpResult(string? Ipv4, string? Ipv6)
    {
        public bool HasIpv4 => !string.IsNullOrWhiteSpace(this.Ipv4);
    }

    internal static class WanIpLookup
    {
        private static readonly HttpClient Client = CreateHttpClient();

        private static readonly string[] Ipv4Endpoints =
        {
            "https://checkip.amazonaws.com",
            "https://api.ipify.org",
            "https://ipv4.icanhazip.com"
        };

        private static readonly string[] Ipv6Endpoints =
        {
            "https://api6.ipify.org",
            "https://ipv6.icanhazip.com",
            "https://v6.ident.me"
        };

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FSSWidget/1.4");
            return client;
        }

        public static async Task<WanIpResult> GetAsync(CancellationToken cancellationToken)
        {
            // IPv4 is the minimum requirement; do not let an unavailable IPv6 connection delay it.
            using var ipv6Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ipv6Cts.CancelAfter(TimeSpan.FromSeconds(6));

            Task<string?> ipv4Task = GetFirstValidAsync(
                Ipv4Endpoints,
                AddressFamily.InterNetwork,
                cancellationToken);
            Task<string?> ipv6Task = GetFirstValidAsync(
                Ipv6Endpoints,
                AddressFamily.InterNetworkV6,
                ipv6Cts.Token);

            string? ipv4 = await ipv4Task.ConfigureAwait(false);
            string? ipv6 = null;
            try
            {
                ipv6 = await ipv6Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // No IPv6 connectivity within the short grace period.
            }

            if (string.IsNullOrWhiteSpace(ipv4))
            {
                Logger.Log("[CloudIpSync] WAN IPv4 lookup failed on all endpoints.");
            }

            return new WanIpResult(ipv4, ipv6);
        }

        private static async Task<string?> GetFirstValidAsync(
            IEnumerable<string> endpoints,
            AddressFamily family,
            CancellationToken cancellationToken)
        {
            foreach (string endpoint in endpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string text = (await Client.GetStringAsync(endpoint, cancellationToken).ConfigureAwait(false)).Trim();
                    if (TryParsePublicAddress(text, family, out IPAddress? address))
                    {
                        return address!.ToString();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Log($"[CloudIpSync] IP lookup via {endpoint} failed: {ex.GetType().Name}");
                }
            }

            return null;
        }

        private static bool TryParsePublicAddress(string text, AddressFamily family, out IPAddress? address)
        {
            address = null;
            if (!IPAddress.TryParse(text, out IPAddress? parsed) || parsed.AddressFamily != family)
            {
                return false;
            }

            if (IPAddress.IsLoopback(parsed))
            {
                return false;
            }

            if (family == AddressFamily.InterNetworkV6
                && (parsed.IsIPv6LinkLocal || parsed.IsIPv6SiteLocal || parsed.IsIPv6Multicast))
            {
                return false;
            }

            address = parsed;
            return true;
        }
    }

    internal sealed record CloudUploadOutcome(
        bool Success,
        string Message,
        bool NeedsSignIn = false,
        bool Unauthorized = false,
        CloudOAuthTokenResult? UpdatedToken = null)
    {
        public static CloudUploadOutcome Ok(string message, CloudOAuthTokenResult? updatedToken = null)
        {
            return new CloudUploadOutcome(true, message, UpdatedToken: updatedToken);
        }

        public static CloudUploadOutcome Fail(
            string message,
            bool needsSignIn = false,
            CloudOAuthTokenResult? updatedToken = null)
        {
            return new CloudUploadOutcome(false, message, needsSignIn, UpdatedToken: updatedToken);
        }

        public static CloudUploadOutcome UnauthorizedResult()
        {
            return new CloudUploadOutcome(false, "Unauthorized", Unauthorized: true);
        }
    }

    internal static class CloudIpSyncUploader
    {
        private static readonly HttpClient Client = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FSSWidget/1.4");
            return client;
        }

        public static async Task<CloudUploadOutcome> UploadAsync(
            CloudIpSyncOptions options,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            CloudOAuthTokenResult? updatedToken = null;

            try
            {
                string accessToken = options.AccessToken;
                if (string.IsNullOrWhiteSpace(accessToken)
                    || options.AccessTokenExpiresUtc <= DateTime.UtcNow.AddMinutes(2))
                {
                    updatedToken = await CloudOAuthClient.RefreshAsync(options, cancellationToken).ConfigureAwait(false);
                    accessToken = updatedToken.AccessToken;
                }

                CloudUploadOutcome outcome = await UploadWithTokenAsync(
                    options.Provider,
                    accessToken,
                    content,
                    fileName,
                    cancellationToken).ConfigureAwait(false);

                if (outcome.Unauthorized)
                {
                    updatedToken = await CloudOAuthClient.RefreshAsync(options, cancellationToken).ConfigureAwait(false);
                    outcome = await UploadWithTokenAsync(
                        options.Provider,
                        updatedToken.AccessToken,
                        content,
                        fileName,
                        cancellationToken).ConfigureAwait(false);

                    if (outcome.Unauthorized)
                    {
                        return CloudUploadOutcome.Fail(
                            "The cloud provider rejected the sign-in. Please sign in again.",
                            needsSignIn: true,
                            updatedToken);
                    }
                }

                return outcome with { UpdatedToken = updatedToken ?? outcome.UpdatedToken };
            }
            catch (CloudOAuthException ex)
            {
                return CloudUploadOutcome.Fail(
                    ex.Message,
                    ex.IsInvalidGrant || string.Equals(ex.ErrorCode, "missing_refresh_token", StringComparison.OrdinalIgnoreCase),
                    updatedToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CloudUploadOutcome.Fail("The cloud upload timed out.", updatedToken: updatedToken);
            }
            catch (HttpRequestException ex)
            {
                return CloudUploadOutcome.Fail("Network error while uploading: " + ex.Message, updatedToken: updatedToken);
            }
            catch (Exception ex)
            {
                return CloudUploadOutcome.Fail("Upload failed: " + ex.Message, updatedToken: updatedToken);
            }
        }

        private static Task<CloudUploadOutcome> UploadWithTokenAsync(
            CloudIpSyncProvider provider,
            string accessToken,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            return provider == CloudIpSyncProvider.OneDrive
                ? UploadToOneDriveAsync(accessToken, content, fileName, cancellationToken)
                : UploadToGoogleDriveAsync(accessToken, content, fileName, cancellationToken);
        }

        private static async Task<CloudUploadOutcome> UploadToOneDriveAsync(
            string accessToken,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            string url = "https://graph.microsoft.com/v1.0/me/drive/root:/"
                + Uri.EscapeDataString(fileName)
                + ":/content";

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(content, Encoding.UTF8, "text/plain");

            using HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return CloudUploadOutcome.UnauthorizedResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                return CloudUploadOutcome.Fail(
                    await BuildHttpErrorAsync("OneDrive upload", response, cancellationToken).ConfigureAwait(false));
            }

            return CloudUploadOutcome.Ok($"Uploaded {fileName} to OneDrive.");
        }

        private static async Task<CloudUploadOutcome> UploadToGoogleDriveAsync(
            string accessToken,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            string query = "name = '" + EscapeDriveQueryValue(fileName) + "' and trashed = false";
            string listUrl = "https://www.googleapis.com/drive/v3/files"
                + "?spaces=drive&pageSize=10&fields=files(id,name)&q="
                + Uri.EscapeDataString(query);

            using (var listRequest = new HttpRequestMessage(HttpMethod.Get, listUrl))
            {
                listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using HttpResponseMessage listResponse = await Client
                    .SendAsync(listRequest, HttpCompletionOption.ResponseContentRead, cancellationToken)
                    .ConfigureAwait(false);

                if (listResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return CloudUploadOutcome.UnauthorizedResult();
                }

                if (!listResponse.IsSuccessStatusCode)
                {
                    return CloudUploadOutcome.Fail(
                        await BuildHttpErrorAsync("Google Drive file lookup", listResponse, cancellationToken).ConfigureAwait(false));
                }

                string listBody = await listResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string? existingFileId = ParseFirstGoogleFileId(listBody);
                if (!string.IsNullOrWhiteSpace(existingFileId))
                {
                    return await UpdateGoogleDriveFileAsync(
                        accessToken,
                        existingFileId,
                        content,
                        fileName,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return await CreateGoogleDriveFileAsync(
                accessToken,
                content,
                fileName,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<CloudUploadOutcome> UpdateGoogleDriveFileAsync(
            string accessToken,
            string fileId,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            string updateUrl = "https://www.googleapis.com/upload/drive/v3/files/"
                + Uri.EscapeDataString(fileId)
                + "?uploadType=media&fields=id,name";

            using var request = new HttpRequestMessage(HttpMethod.Patch, updateUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(content, Encoding.UTF8, "text/plain");

            using HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return CloudUploadOutcome.UnauthorizedResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                return CloudUploadOutcome.Fail(
                    await BuildHttpErrorAsync("Google Drive update", response, cancellationToken).ConfigureAwait(false));
            }

            return CloudUploadOutcome.Ok($"Uploaded {fileName} to Google Drive.");
        }

        private static async Task<CloudUploadOutcome> CreateGoogleDriveFileAsync(
            string accessToken,
            string content,
            string fileName,
            CancellationToken cancellationToken)
        {
            string boundary = "fsswidget_" + Guid.NewGuid().ToString("N");
            string metadata = JsonSerializer.Serialize(new
            {
                name = fileName,
                mimeType = "text/plain"
            });

            var body = new StringBuilder();
            body.Append("--").Append(boundary).Append("\r\n");
            body.Append("Content-Type: application/json; charset=UTF-8\r\n\r\n");
            body.Append(metadata).Append("\r\n");
            body.Append("--").Append(boundary).Append("\r\n");
            body.Append("Content-Type: text/plain; charset=UTF-8\r\n\r\n");
            body.Append(content).Append("\r\n");
            body.Append("--").Append(boundary).Append("--\r\n");

            string createUrl = "https://www.googleapis.com/upload/drive/v3/files"
                + "?uploadType=multipart&fields=id,name";

            using var request = new HttpRequestMessage(HttpMethod.Post, createUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var multipart = new StringContent(body.ToString(), Encoding.UTF8);
            multipart.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/related; boundary=" + boundary);
            request.Content = multipart;

            using HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return CloudUploadOutcome.UnauthorizedResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                return CloudUploadOutcome.Fail(
                    await BuildHttpErrorAsync("Google Drive upload", response, cancellationToken).ConfigureAwait(false));
            }

            return CloudUploadOutcome.Ok($"Uploaded {fileName} to Google Drive.");
        }

        private static string? ParseFirstGoogleFileId(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("files", out JsonElement files)
                    || files.ValueKind != JsonValueKind.Array
                    || files.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement first = files[0];
                return first.TryGetProperty("id", out JsonElement idElement)
                    ? idElement.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string EscapeDriveQueryValue(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private static async Task<string> BuildHttpErrorAsync(
            string operation,
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string detail = ExtractErrorMessage(body);
            detail = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (detail.Length > 300)
            {
                detail = detail[..300];
            }

            return string.IsNullOrWhiteSpace(detail)
                ? $"{operation} failed (HTTP {(int) response.StatusCode})."
                : $"{operation} failed (HTTP {(int) response.StatusCode}: {detail}).";
        }

        private static string ExtractErrorMessage(string body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("error", out JsonElement error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? string.Empty;
                    }

                    if (error.ValueKind == JsonValueKind.Object
                        && error.TryGetProperty("message", out JsonElement message))
                    {
                        return message.GetString() ?? string.Empty;
                    }
                }

                if (root.TryGetProperty("message", out JsonElement rootMessage))
                {
                    return rootMessage.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                // Fall back to the raw body below.
            }

            return body;
        }
    }
}
