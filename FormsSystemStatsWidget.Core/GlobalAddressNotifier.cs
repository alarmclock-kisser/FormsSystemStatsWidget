using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static class GlobalAddressNotifier
    {
        private static readonly HttpClient _httpClient = new();

        // Der feste Pfad zu deiner OneDrive-Datei
        private static readonly string _lastIpFilePath = @"C:\Users\op\OneDrive\IPCONFIG\DERGERAET2_IP.txt";

        public static async Task<string?> GetGlobalAddressIpv4Async()
        {
            try
            {
                // Holt die aktuelle öffentliche IPv4
                return await _httpClient.GetStringAsync("https://api.ipify.org");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching IPv4: {ex.Message}");
                return null;
            }
        }

        public static async Task CheckAndUpdateIpAsync()
        {
            string? currentIpv4 = await GetGlobalAddressIpv4Async();

            if (string.IsNullOrEmpty(currentIpv4))
            {
                return;
            }

            // Sicherstellen, dass der IPCONFIG-Ordner auf dem OneDrive existiert
            string? directory = Path.GetDirectoryName(_lastIpFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string lastKnownIp = string.Empty;
            if (File.Exists(_lastIpFilePath))
            {
                // Liest die alte IP aus der Datei
                lastKnownIp = await File.ReadAllTextAsync(_lastIpFilePath);
            }

            // Wenn die IP sich geändert hat oder die Datei noch gar nicht existierte
            if (currentIpv4 != lastKnownIp)
            {
                Console.WriteLine($"IP-Change detected! Old: {lastKnownIp} | New: {currentIpv4}");

                // Datei wird überschrieben. Das flusht die Daten auf die Festplatte, 
                // woraufhin der OneDrive-Client sofort den Cloud-Upload anwirft.
                await File.WriteAllTextAsync(_lastIpFilePath, currentIpv4);

                Console.WriteLine("File updated. OneDrive is now synchronizing in the background.");
            }
            else
            {
                Console.WriteLine("IP is unchanged. No action required.");
            }
        }
    }
}