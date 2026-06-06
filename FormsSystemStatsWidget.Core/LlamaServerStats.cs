using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static class LlamaServerStats
    {
        // Timeout deutlich erhöht! 250ms war viel zu kurz für einen ausgelasteten KI-Server.
        private static readonly HttpClient _statsClient = new() { Timeout = TimeSpan.FromMilliseconds(2500) };

        private static int _lastTaskId = -1;
        private static int _lastNDecoded = 0;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static DateTime _lastPollTime = DateTime.MinValue;
        private static float _currentTps = 0f;
        private static float _liveTpsFromStdOut = 0f;
        private static DateTime _liveTpsFromStdOutUtc = DateTime.MinValue;
        private static readonly TimeSpan IdlePollingInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan StdOutTpsTtl = TimeSpan.FromSeconds(3);

        // Zähler für kurzzeitige Aussetzer bei hoher Systemlast
        private static int _errorCount = 0;

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

                _errorCount = 0; // Fehler-Zähler zurücksetzen bei erfolgreichem Pull

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

                int deltaTokens = currentNDecoded - _lastNDecoded;
                double deltaSeconds = (now - _lastCheckTime).TotalSeconds;

                if (directTokensPerSecond > 0f)
                {
                    _currentTps = directTokensPerSecond;
                    _lastNDecoded = currentNDecoded;
                    _lastCheckTime = now;
                }
                else if (deltaSeconds > 0 && deltaTokens >= 0)
                {
                    if (deltaTokens > 0)
                    {
                        _currentTps = (float) (deltaTokens / deltaSeconds);
                        _lastNDecoded = currentNDecoded;
                        _lastCheckTime = now;
                    }
                    else
                    {
                        if (currentNDecoded == 0)
                        {
                            _currentTps = 0f;
                        }
                        else
                        {
                            _currentTps *= 0.90f;
                            if (_currentTps < 0.05f)
                            {
                                _currentTps = 0f;
                            }
                        }
                        // Update Zeit für nächste Berechnung
                        _lastCheckTime = now;
                    }
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

                // Wenn der Server gerade hart rechnet, blockiert der HTTP-Thread desllama-server 
                // manchmal massiv. Wir buffern nun bis zu 15 Timeouts (~30-37s) ab!
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
    }
}