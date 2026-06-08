using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace FormsSystemStatsWidget.Core
{
    public partial class LlamaToolExecutor
    {
        private readonly HttpClient _httpClient = new();
        private readonly StringBuilder _streamBuffer = new();



        [GeneratedRegex(@"<api>(?<content>.*?)</\s*//?api>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex RegexApiTag();
        private static readonly Regex ApiTagRegex = RegexApiTag();



        /// <summary>
        /// Verarbeitet einen Chunk des Ausgabestreams und sucht nach <api> Tags.
        /// </summary>
        /// <param name="chunk">Der eingehende Text-Chunk vom LLM-Server.</param>
        /// <returns>Das Ergebnis der ausgeführten API-Aufrufe.</returns>
        public async Task<string> ProcessStreamChunkAsync(string chunk)
        {
            this._streamBuffer.Append(chunk);
            string result = string.Empty;

            var matches = ApiTagRegex.Matches(this._streamBuffer.ToString());
            foreach (Match match in matches)
            {
                string content = match.Groups["content"].Value.Trim();
                string fullUrl = this.ExpandUrl(content);

                try
                {
                    string apiResult = await this.ExecuteApiCallAsync(fullUrl);
                    result += $"\n[API Result ({fullUrl})]: {apiResult}";
                }
                catch (Exception ex)
                {
                    result += $"\n[API Error ({fullUrl})]: {ex.Message}";
                }

                // Entferne den verarbeiteten Teil aus dem Buffer
                this._streamBuffer.Remove(0, match.Index + match.Length);
            }

            return result;
        }

        /// <summary>
        /// Erweitert eine relative URL (z.B. "1290/route") zu einer vollständigen URL (z.B. "https://localhost:1290/route").
        /// </summary>
        private string ExpandUrl(string content)
        {
            string url = content;
            // Wenn kein Doppelpunkt vorhanden ist, nehmen wir an, dass der erste Teil der Port ist.
            if (!url.Contains(":"))
            {
                var parts = url.Split('/', 2);
                if (parts.Length == 2)
                {
                    url = $":{parts[0]}/{parts[1]}";
                }
                else if (parts.Length == 1)
                {
                    url = $":{parts[0]}";
                }
            }

            return $"https://localhost{url}";
        }

        /// <summary>
        /// Führt einen API-Aufruf aus.
        /// </summary>
        private async Task<string> ExecuteApiCallAsync(string url)
        {
            var response = await this._httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Führt einen CMD-Befehl aus.
        /// </summary>
        public async Task<string> ExecuteCommandAsync(string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return "Error: Could not start process.";

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrEmpty(error) ? output : $"Error: {error}";
        }

        /// <summary>
        /// Liest den Inhalt einer Datei.
        /// </summary>
        public async Task<string> ReadFileAsync(string path)
        {
            if (!File.Exists(path)) return $"Error: File not found at {path}";
            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        /// Schreibt Inhalt in eine Datei.
        /// </summary>
        public async Task<string> WriteFileAsync(string path, string content)
        {
            try
            {
                await File.WriteAllTextAsync(path, content);
                return "Success: File written.";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Gibt das aktuelle Datum und die Uhrzeit zurück.
        /// </summary>
        public string GetDateTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        
    }
}