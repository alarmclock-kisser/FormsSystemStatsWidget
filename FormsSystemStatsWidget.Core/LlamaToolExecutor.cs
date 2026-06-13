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
        /// Processes a chunk of the output stream and looks for <api> tags.
        /// </summary>
        /// <param name="chunk">The incoming text chunk from the LLM server.</param>
        /// <returns>The result of the executed API calls.</returns>
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

                // Remove the processed section from the buffer
                this._streamBuffer.Remove(0, match.Index + match.Length);
            }

            return result;
        }

        /// <summary>
        /// Expands a relative URL (for example "1290/route") into a full URL (for example "https://localhost:1290/route").
        /// </summary>
        private string ExpandUrl(string content)
        {
            string url = content;
            // If no colon is present, assume the first segment is the port.
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
        /// Executes an API call.
        /// </summary>
        private async Task<string> ExecuteApiCallAsync(string url)
        {
            var response = await this._httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Executes a CMD command.
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
            if (process == null)
            {
                return "Error: Could not start process.";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrEmpty(error) ? output : $"Error: {error}";
        }

        /// <summary>
        /// Reads the contents of a file.
        /// </summary>
        public async Task<string> ReadFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                return $"Error: File not found at {path}";
            }

            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        /// Writes content to a file.
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
        /// Returns the current date and time.
        /// </summary>
        public string GetDateTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        
    }
}