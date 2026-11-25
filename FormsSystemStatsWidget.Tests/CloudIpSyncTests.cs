using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FormsSystemStatsWidget.Tests
{
    [TestClass]
    public class CloudIpSyncTests
    {
        [TestMethod]
        public void IpDataFormatter_ContainsIpv4AndIpv6()
        {
            DateTime timestamp = new(2026, 11, 25, 12, 34, 56, DateTimeKind.Utc);

            string content = CloudIpDataFormatter.Build(
                "DESKTOP-TEST",
                "203.0.113.10",
                "2001:db8::1",
                timestamp);

            StringAssert.Contains(content, "Host: DESKTOP-TEST");
            StringAssert.Contains(content, "Updated (UTC): 2026-11-25T12:34:56Z");
            StringAssert.Contains(content, "WAN IPv4: 203.0.113.10");
            StringAssert.Contains(content, "WAN IPv6: 2001:db8::1");
            Assert.IsTrue(
                content.IndexOf("WAN IPv4", StringComparison.Ordinal)
                    < content.IndexOf("WAN IPv6", StringComparison.Ordinal),
                "IPv6 must be below IPv4.");
        }

        [TestMethod]
        public void IpDataFormatter_UsesUnavailableForMissingIpv6()
        {
            string content = CloudIpDataFormatter.Build(
                "DESKTOP-TEST",
                "203.0.113.10",
                null,
                DateTime.UtcNow);

            StringAssert.Contains(content, "WAN IPv4: 203.0.113.10");
            StringAssert.Contains(content, "WAN IPv6: unavailable");
        }

        [TestMethod]
        public void SanitizeFileName_ReplacesInvalidCharacters()
        {
            string name = CloudIpSyncHelpers.SanitizeFileName("PC:NAME/with*invalid?chars");

            Assert.IsFalse(name.Contains(':'));
            Assert.IsFalse(name.Contains('/'));
            Assert.IsFalse(name.Contains('*'));
            Assert.IsFalse(name.Contains('?'));
            Assert.IsTrue(name.Length > 0);
        }

        [TestMethod]
        public void GetIpDataFileName_EndsWithIpdata()
        {
            string fileName = CloudIpSyncHelpers.GetIpDataFileName("DESKTOP-TEST");

            Assert.AreEqual("DESKTOP-TEST.ipdata", fileName);
        }

        [TestMethod]
        public void ProviderInfo_ParsesStorageValues()
        {
            Assert.AreEqual(
                CloudIpSyncProvider.OneDrive,
                CloudIpSyncProviderInfo.Parse("OneDrive"));
            Assert.AreEqual(
                CloudIpSyncProvider.GoogleDrive,
                CloudIpSyncProviderInfo.Parse("something-else"));
            Assert.AreEqual(
                "GoogleDrive",
                CloudIpSyncProvider.GoogleDrive.ToStorageValue());
        }

        [TestMethod]
        public void SettingsMapper_RoundTripsSecretsAndProtectsThem()
        {
            var settings = new WidgetPersistentSettings();
            var options = new CloudIpSyncOptions
            {
                Provider = CloudIpSyncProvider.GoogleDrive,
                TenantId = "common",
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RefreshToken = "refresh-token",
                AccessToken = "access-token",
                AccessTokenExpiresUtc = new DateTime(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc),
                AccountDisplayName = "user@example.com"
            };

            CloudIpSyncSettingsMapper.ApplyToPersistent(settings, options);
            CloudIpSyncOptions restored = CloudIpSyncSettingsMapper.FromPersistent(settings);

            Assert.AreEqual(options.Provider, restored.Provider);
            Assert.AreEqual(options.TenantId, restored.TenantId);
            Assert.AreEqual(options.ClientId, restored.ClientId);
            Assert.AreEqual(options.ClientSecret, restored.ClientSecret);
            Assert.AreEqual(options.RefreshToken, restored.RefreshToken);
            Assert.AreEqual(options.AccessToken, restored.AccessToken);
            Assert.AreEqual(options.AccountDisplayName, restored.AccountDisplayName);

            Assert.AreNotEqual("client-secret", settings.CloudIpSyncClientSecretProtected);
            Assert.AreNotEqual("refresh-token", settings.CloudIpSyncRefreshTokenProtected);
            Assert.IsTrue(CloudIpSyncSettingsMapper.IsConfigured(settings));
        }

        [TestMethod]
        public void SettingsMapper_GoogleRequiresClientSecret()
        {
            var options = new CloudIpSyncOptions
            {
                Provider = CloudIpSyncProvider.GoogleDrive,
                ClientId = "client-id",
                RefreshToken = "refresh-token",
                ClientSecret = string.Empty
            };

            Assert.IsFalse(CloudIpSyncSettingsMapper.IsConfigured(options));

            options.Provider = CloudIpSyncProvider.OneDrive;
            Assert.IsTrue(CloudIpSyncSettingsMapper.IsConfigured(options));
        }

        [TestMethod]
        public void AuthorizationUrl_ContainsPkceStateAndScopes()
        {
            string url = CloudOAuthClient.BuildAuthorizationUrl(
                CloudIpSyncProvider.GoogleDrive,
                "client-id",
                "http://localhost:12345/",
                "state-value",
                "challenge-value");

            StringAssert.Contains(url, "client_id=client-id");
            StringAssert.Contains(url, "code_challenge=challenge-value");
            StringAssert.Contains(url, "code_challenge_method=S256");
            StringAssert.Contains(url, "state=state-value");
            StringAssert.Contains(url, "access_type=offline");
            StringAssert.Contains(url, Uri.EscapeDataString("https://www.googleapis.com/auth/drive.file"));
        }

        [TestMethod]
        public void OneDriveEndpoints_UseConfiguredTenant()
        {
            string authorizationUrl = CloudOAuthClient.BuildAuthorizationUrl(
                CloudIpSyncProvider.OneDrive,
                "client-id",
                "http://localhost:12345/",
                "state-value",
                "challenge-value",
                "11111111-2222-3333-4444-555555555555");

            StringAssert.Contains(
                authorizationUrl,
                "https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555/oauth2/v2.0/authorize");
            Assert.AreEqual(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                CloudIpSyncProvider.OneDrive.GetTokenEndpoint("common"));
        }

        [TestMethod]
        public void CodeChallenge_IsUrlSafe()
        {
            string challenge = CloudOAuthClient.CreateCodeChallenge("verifier");

            Assert.IsFalse(challenge.Contains('+'));
            Assert.IsFalse(challenge.Contains('/'));
            Assert.IsFalse(challenge.Contains('='));
            Assert.IsTrue(challenge.Length > 20);
        }

        [TestMethod]
        public void ProtectedSecretStore_RoundTrips()
        {
            string protectedValue = ProtectedSecretStore.Protect("super-secret-value");

            Assert.AreNotEqual("super-secret-value", protectedValue);
            Assert.AreEqual("super-secret-value", ProtectedSecretStore.Unprotect(protectedValue));
        }

        [TestMethod]
        public void MenuHitTest_BodyTogglesArrowDoesNot()
        {
            var bounds = new System.Drawing.Rectangle(100, 100, 300, 24);

            Assert.IsTrue(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(110, 112), 96));
            Assert.IsTrue(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(370, 112), 96));
            Assert.IsFalse(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(390, 112), 96));
            Assert.IsFalse(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(90, 112), 96));
        }

        [TestMethod]
        public void MenuHitTest_ScalesArrowWithDpi()
        {
            var bounds = new System.Drawing.Rectangle(0, 0, 300, 24);

            // At 200% the reserved arrow area is twice as wide.
            Assert.IsFalse(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(280, 12), 192));
            Assert.IsTrue(CloudIpSyncMenuHitTest.IsToggleClick(bounds, new System.Drawing.Point(250, 12), 192));
        }

        [TestMethod]
        public void SettingsMapper_PreservesOneDriveTenant()
        {
            var settings = new WidgetPersistentSettings();
            var options = new CloudIpSyncOptions
            {
                Provider = CloudIpSyncProvider.OneDrive,
                TenantId = "11111111-2222-3333-4444-555555555555",
                ClientId = "client-id",
                RefreshToken = "refresh-token"
            };

            CloudIpSyncSettingsMapper.ApplyToPersistent(settings, options);
            CloudIpSyncOptions restored = CloudIpSyncSettingsMapper.FromPersistent(settings);

            Assert.AreEqual(options.TenantId, restored.TenantId);
            Assert.AreEqual(options.Provider, restored.Provider);
        }

        [TestMethod]
        public async Task LoopbackCallbackListener_AcceptsValidCallback()
        {
            using var listener = LoopbackCallbackListener.Start();
            Task<OAuthCallbackResult> callbackTask = listener.WaitForCallbackAsync(
                "expected-state",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            using var client = new HttpClient();
            string response = await client.GetStringAsync(
                listener.RedirectUri + "?code=test-code&state=expected-state");

            OAuthCallbackResult callback = await callbackTask;
            Assert.AreEqual("test-code", callback.Code);
            Assert.IsNull(callback.Error);
            StringAssert.Contains(response, "Sign-in complete");
        }

        [TestMethod]
        public async Task LoopbackCallbackListener_RejectsWrongState()
        {
            using var listener = LoopbackCallbackListener.Start();
            Task<OAuthCallbackResult> callbackTask = listener.WaitForCallbackAsync(
                "expected-state",
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            using var client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(
                listener.RedirectUri + "?code=test-code&state=wrong-state");
            Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

            OAuthCallbackResult callback = await callbackTask;
            Assert.AreEqual("invalid_state", callback.Error);
            Assert.IsNull(callback.Code);
        }
    }
}
