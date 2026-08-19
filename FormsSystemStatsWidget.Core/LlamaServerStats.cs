using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Provides real-time statistics and monitoring for a running llama.cpp server process.
    /// </summary>
    /// <remarks>
    /// This class connects directly to a running llama.cpp process to capture console output
    /// in real-time and provides generation speed (tokens per second) and context token counts.
    /// It supports both direct HTTP API access to the llama.cpp stats endpoint and StdOut polling.
    /// </remarks>
    public static partial class LlamaServerStats
    {
        /// <summary>
        /// HTTP client used to communicate with the llama.cpp server's statistics endpoint.
        /// </summary>
        /// <remarks>
        /// Timeout is set to 2500ms to accommodate loaded AI servers that may respond slowly.
        /// </remarks>
        private static readonly HttpClient _statsClient = new() { Timeout = TimeSpan.FromMilliseconds(2500) };

        private static int _lastTaskId = -1;
        private static int _lastNDecoded = 0;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static DateTime _lastPollTime = DateTime.MinValue;
        private static float _currentTps = 0f;
        private static float _liveTpsFromStdOut = 0f;
        private static DateTime _liveTpsFromStdOutUtc = DateTime.MinValue;
        private static readonly TimeSpan IdlePollingInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The time-to-live (TTL) for a StdOut tokens-per-second value.
        /// </summary>
        /// <remarks>
        /// A StdOut TPS value is only valid for this duration (e.g., 4 seconds) before it becomes stale.
        /// </remarks>
        private static readonly TimeSpan StdOutTpsTtl = TimeSpan.FromSeconds(4);

        /// <summary>
                /// Regex pattern used to parse tokens-per-second values from llama.cpp console output.
                /// </summary>
                /// <remarks>
                /// Matches log entries in the format "tg = 19.14 t/s" to extract the generation speed.
                /// </remarks>
                private static readonly Regex TimingRegex = TokensPerSecondRegex();
        /// <summary>
                /// Count of errors encountered while monitoring the llama.cpp server.
                /// </summary>
                /// <remarks>
                /// Incremented when statistics requests fail or StdOut parsing encounters issues.
                /// If the error count exceeds 15, the monitoring state is reset.
                /// </remarks>
                private static int _errorCount = 0;

        /// <summary>
                /// The last recorded count of context tokens from the llama.cpp server.
                /// </summary>
                /// <remarks>
                /// This value is updated when new context token data is available from the /slots endpoint.
                /// It tracks the highest context token count seen during active generation.
                /// </remarks>
                public static int _lastHighContextTokens { get; private set; } = 0;

        /// <summary>
        /// Attaches the class directly to a running llama.cpp process to capture console output in real-time.
        /// </summary>
        /// <remarks>
        /// llama.cpp sends almost all logs (including info) to StandardError, so both error and output
        /// data events are hooked to capture all relevant information.
        /// </summary>
        /// <param name="llamaServerProcess">The running llama.cpp process to attach to.</param>
        public static void AttachToProcess(Process llamaServerProcess)
        {
            if (llamaServerProcess == null)
            {
                return;
            }

            // WICHTIG: llama.cpp sendet fast alle Logs (auch Info) an StandardError!
            llamaServerProcess.ErrorDataReceived += (sender, e) =>
            {
                ParseStdOutLine(e.Data);
            };

            // Zur Sicherheit auch StandardOutput anbinden
            llamaServerProcess.OutputDataReceived += (sender, e) =>
            {
                ParseStdOutLine(e.Data);
            };
        }

        /// <summary>
                /// Parses a standard output line from the llama.cpp process to extract tokens-per-second values.
                /// </summary>
                /// <remarks>
                /// If the line contains a matching tokens-per-second pattern, the generation speed is updated.
                /// Lines that are null, empty, or whitespace are ignored.
                /// </remarks>
                /// <param name="line">The console output line to parse. Can be null or whitespace.</param>
                public static void ParseStdOutLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var match = TimingRegex.Match(line);
            if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float tokensPerSecond))
            {
                UpdateGenerationSpeed(tokensPerSecond);
            }
        }

        /// <summary>
                /// Asynchronously retrieves the current generation statistics from the llama.cpp server.
                /// </summary>
                /// <remarks>
                /// First attempts to get tokens-per-second from standard output (if recently captured).
                /// Falls back to HTTP API call to the /slots endpoint if StdOut data is stale or unavailable.
                /// Returns null if the server is unreachable or no active generation is detected.
                /// </remarks>
                /// <param name="llamacppPort">The port number where the llama.cpp server is listening (default: 8080).</param>
                /// <returns>Current tokens-per-second value, or null if the server is unreachable.</returns>
                public static async Task<float?> GetCurrentLlamaServerGenerationStatsAsync(int llamacppPort = 8080)
        {
            DateTime now = DateTime.UtcNow;

            if (TryGetFreshStdOutTps(now, out float stdOutTps))
            {
                _currentTps = stdOutTps;
                return stdOutTps;
            }

            if (_lastTaskId == -1 && (now - _lastPollTime) < IdlePollingInterval)
            {
                return 0f;
            }

            try
            {
                _lastPollTime = now;

                var response = await _statsClient.GetAsync($"http://localhost:{llamacppPort}/slots");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var slotsArray = JsonNode.Parse(content)?.AsArray();
                if (slotsArray == null || slotsArray.Count == 0)
                {
                    return 0f;
                }

                bool anySlotActive = false;
                int activeTaskId = -1;
                int currentNDecoded = 0;
                float directTokensPerSecond = 0f;

                foreach (var slotNode in slotsArray)
                {
                    if (slotNode == null)
                    {
                        continue;
                    }

                    bool isProcessing = false;

                    var stateNode = slotNode["state"];
                    if (stateNode != null)
                    {
                        string stateStr = stateNode.ToString().ToLower();
                        if (stateStr == "1" || stateStr == "processing")
                        {
                            isProcessing = true;
                        }
                    }

                    var isProcNode = slotNode["is_processing"];
                    if (isProcNode != null)
                    {
                        string procStr = isProcNode.ToString().ToLower();
                        if (procStr == "true" || procStr == "1")
                        {
                            isProcessing = true;
                        }
                    }

                    if (isProcessing)
                    {
                        anySlotActive = true;
                        if (slotNode["id_task"] != null && int.TryParse(slotNode["id_task"]?.ToString(), out int idt))
                        {
                            activeTaskId = idt;
                        }
                        else
                        {
                            activeTaskId = 1;
                        }

                        if (TryReadInt(slotNode, out int nDecoded, "n_decoded_tokens", "n_decoded", "n_decode", "n_tokens_predicted", "n_predict", "tokens_predicted"))
                        {
                            currentNDecoded += nDecoded;
                        }

                        if (TryReadFloat(slotNode, out float slotTokensPerSecond, "tokens_per_second", "tokens/s", "predicted_per_second", "generation_tokens_per_second"))
                        {
                            directTokensPerSecond += slotTokensPerSecond;
                        }

                        JsonNode? timingsNode = slotNode["timings"];
                        if (timingsNode != null && TryReadFloat(timingsNode, out slotTokensPerSecond, "predicted_per_second", "generation_tokens_per_second"))
                        {
                            directTokensPerSecond += slotTokensPerSecond;
                        }
                    }
                }

                _errorCount = 0;

                if (!anySlotActive)
                {
                    _lastTaskId = -1;
                    _lastNDecoded = 0;
                    _currentTps = 0f;
                    return 0f;
                }

                if (activeTaskId != _lastTaskId)
                {
                    _lastTaskId = activeTaskId;
                    _lastNDecoded = currentNDecoded;
                    _lastCheckTime = now;
                    if (directTokensPerSecond > 0f)
                    {
                        _currentTps = directTokensPerSecond;
                    }

                    return _currentTps > 0 ? _currentTps : 0f;
                }

                return _currentTps;
            }
            catch
            {
                _errorCount++;

                if (TryGetFreshStdOutTps(DateTime.UtcNow, out float stdOutTpsFallback))
                {
                    _currentTps = stdOutTpsFallback;
                    return stdOutTpsFallback;
                }

                if (_errorCount > 15)
                {
                    _lastTaskId = -1;
                    _lastNDecoded = 0;
                    _currentTps = 0f;
                    return null;
                }

                return _currentTps > 0 ? _currentTps : 0f;
            }
        }

        /// <summary>
        /// Asynchronously retrieves the current context token count from the llama.cpp server.
        /// </summary>
        /// <remarks>
        /// Reads the total number of prompt and decoded tokens from the /slots endpoint.
        /// Returns 0 if the endpoint is unreachable. The value is compared against the last high
        /// context token count to track maximum usage.
        /// </remarks>
        /// <param name="llamacppPort">The port number where the llama.cpp server is listening (default: 8080).</param>
        /// <param name="useLastHigh">If true, returns the maximum of the current and last high context token count.</param>
        /// <returns>The current context token count, or the last high count if useLastHigh is true.</returns>
        public static async Task<int> GetCurrentContextTokensAsync(int llamacppPort = 8080, bool useLastHigh = true)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                var response = await _statsClient.GetAsync($"http://localhost:{llamacppPort}/slots", cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }

                var content = await response.Content.ReadAsStringAsync();
                var slotsArray = JsonNode.Parse(content)?.AsArray();
                if (slotsArray == null)
                {
                    return 0;
                }

                int total = 0;
                foreach (var slotNode in slotsArray)
                {
                    if (slotNode == null)
                    {
                        continue;
                    }

                    if (TryReadInt(slotNode, out int promptTokens, "n_prompt_tokens", "n_prompt_tokens_processed"))
                    {
                        total += promptTokens;
                    }

                    // n_decoded liegt je nach llama.cpp-Version unter next_token[0].n_decoded oder direkt im Slot
                    JsonNode? nextTokenArray = slotNode["next_token"];
                    if (nextTokenArray != null && nextTokenArray.AsArray().Count > 0 && TryReadInt(nextTokenArray[0]!, out int nDecoded, "n_decoded"))
                    {
                        total += nDecoded;
                    }
                    else if (TryReadInt(slotNode, out int nDecodedDirect, "n_decoded_tokens", "n_decoded"))
                    {
                        total += nDecodedDirect;
                    }
                }

                _lastHighContextTokens = total > _lastHighContextTokens ? total : _lastHighContextTokens;
                return useLastHigh ? Math.Max(total, _lastHighContextTokens) : total;
            }
            catch
            {
                _lastHighContextTokens = 0;
                return 0;
            }
        }

        /// <summary>
                /// Updates the generation speed based on a new tokens-per-second value from the StdOut.
                /// </summary>
                /// <remarks>
                /// Stores the new TPS value and the current UTC timestamp. Also updates the current TPS
                /// if the new value is greater than zero.
                /// </remarks>
                /// <param name="tokensPerSecond">The new tokens-per-second value to record.</param>
                public static void UpdateGenerationSpeed(float tokensPerSecond)
        {
            if (tokensPerSecond <= 0f)
            {
                return;
            }

            _liveTpsFromStdOut = tokensPerSecond;
            _liveTpsFromStdOutUtc = DateTime.UtcNow;
            _currentTps = tokensPerSecond;
        }

        /// <summary>
                /// Checks if the StdOut tokens-per-second value is still fresh (within the TTL).
                /// </summary>
                /// <remarks>
                /// A StdOut TPS value is only considered fresh if it was captured within the StdOutTpsTtl
                /// duration (e.g., 4 seconds). Returns false if no value has been captured yet or if it has expired.
                /// </remarks>
                /// <param name="now">The current UTC time.</param>
                /// <param out tokensPerSecond>The tokens-per-second value if fresh, otherwise 0.</param>
                /// <returns>true if the TPS value is fresh and valid; otherwise false.</returns>
                private static bool TryGetFreshStdOutTps(DateTime now, out float tokensPerSecond)
        {
            tokensPerSecond = 0f;
            if (_liveTpsFromStdOut <= 0f)
            {
                return false;
            }

            if ((now - _liveTpsFromStdOutUtc) > StdOutTpsTtl)
            {
                return false;
            }

            tokensPerSecond = _liveTpsFromStdOut;
            return true;
        }

        /// <summary>
                /// Tries to read an integer value from a JSON node by checking multiple property names.
                /// </summary>
                /// <remarks>
                /// Iterates through the provided property name parameters and attempts to parse the first
                /// found value as an integer. If no matching property is found or parsing fails, the output
                /// value is set to 0 and the method returns false.
                /// </remarks>
                /// <param name="node">The JSON node to search for the integer value.</param>
                /// <param out value>The output integer value. Set to 0 if no valid value is found.</param>
                /// <param name="propertyNames">Variable-length list of property names to check for the value.</param>
                /// <returns>true if an integer value was successfully parsed from one of the properties; otherwise false.</returns>
                private static bool TryReadInt(JsonNode node, out int value, params string[] propertyNames)
        {
            value = 0;
            foreach (string propertyName in propertyNames)
            {
                JsonNode? valueNode = node[propertyName];
                if (valueNode != null && int.TryParse(valueNode.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadFloat(JsonNode node, out float value, params string[] propertyNames)
        {
            value = 0f;
            foreach (string propertyName in propertyNames)
            {
                JsonNode? valueNode = node[propertyName];
                if (valueNode != null && float.TryParse(valueNode.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0f)
                {
                    return true;
                }
            }
            return false;
        }

        // Die Regex passt perfekt auf dein Log: "tg =  19.14 t/s"
        [GeneratedRegex(@"tg\s*=\s*([\d.]+)\s*t\/s", RegexOptions.Compiled)]
        private static partial Regex TokensPerSecondRegex();
    }
}