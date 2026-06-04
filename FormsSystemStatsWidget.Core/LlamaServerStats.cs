using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static class LlamaServerStats
    {
        // Eigener HttpClient mit extrem kurzem Timeout, damit der UI-Timer bei Server-Offline niemals blockiert
        private static readonly HttpClient _statsClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };

        private static int _lastTaskId = -1;
        private static int _lastNDecoded = 0;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static float _currentTps = 0f;

        /// <summary>
        /// Fragt den llama-server Slots-Endpunkt ab und errechnet die aktuellen Tokens pro Sekunde (t/s).
        /// Gibt 'null' zurück, wenn der Server offline ist, und '0', wenn er idle ist.
        /// </summary>
        public static async Task<float?> GetCurrentLlamaServerGenerationStatsAsync(int llamacppPort = 8080)
        {
            try
            {
                // 1. Slots-Status vom llama-server abrufen
                var response = await _statsClient.GetAsync($"http://localhost:{llamacppPort}/slots");
                if (!response.IsSuccessStatusCode)
                {
                    return null; // Server antwortet fehlerhaft -> Als Offline werten
                }

                var content = await response.Content.ReadAsStringAsync();
                var slotsArray = JsonNode.Parse(content)?.AsArray();
                if (slotsArray == null || slotsArray.Count == 0)
                {
                    return 0f; // Keine Slots aktiv -> 0 t/s
                }

                bool anySlotActive = false;
                int activeTaskId = -1;
                int currentNDecoded = 0;

                // 2. Slots durchscannen und aktive Generation-Tasks aggregieren
                foreach (var slotNode in slotsArray)
                {
                    if (slotNode == null)
                    {
                        continue;
                    }

                    // id_task ist -1 wenn der Slot schläft (idle), ansonsten steht dort die Task-ID vom Copilot
                    var idTaskNode = slotNode["id_task"];
                    if (idTaskNode != null && int.TryParse(idTaskNode.ToString(), out int idTask) && idTask != -1)
                    {
                        anySlotActive = true;
                        activeTaskId = idTask;

                        // n_decoded enthält die Anzahl der für diese laufende Task generierten Tokens
                        var nDecodedNode = slotNode["n_decoded"];
                        if (nDecodedNode != null && int.TryParse(nDecodedNode.ToString(), out int nDecoded))
                        {
                            currentNDecoded += nDecoded;
                        }
                    }
                }

                // 3. Wenn alle Slots idle sind -> Sofort 0 t/s zurückgeben
                if (!anySlotActive)
                {
                    _lastTaskId = -1;
                    _lastNDecoded = 0;
                    _currentTps = 0f;
                    return 0f;
                }

                DateTime now = DateTime.UtcNow;

                // 4. Wenn eine brandneue Task gestartet wurde, initialisieren wir die Tracker für den nächsten Tick
                if (activeTaskId != _lastTaskId)
                {
                    _lastTaskId = activeTaskId;
                    _lastNDecoded = currentNDecoded;
                    _lastCheckTime = now;

                    // Liefert beim allerersten Token-Anlauf entweder den letzten Wert oder fängt bei 0 an
                    return _currentTps > 0 ? _currentTps : 0f;
                }

                // 5. Zeit- und Token-Delta berechnen seit dem letzten Timer-Tick (~420ms)
                int deltaTokens = currentNDecoded - _lastNDecoded;
                double deltaSeconds = (now - _lastCheckTime).TotalSeconds;

                if (deltaSeconds > 0 && deltaTokens >= 0)
                {
                    if (deltaTokens > 0)
                    {
                        // Errechnung der echten Tokens/Sec über die Delta-Zeit
                        _currentTps = (float) (deltaTokens / deltaSeconds);
                        _lastNDecoded = currentNDecoded;
                        _lastCheckTime = now;
                    }
                    else
                    {
                        // Falls der Timer schneller tickt als das Modell ein Token auswirft (Prefill-Phase oder Generation-Verzögerung),
                        // nutzen wir ein leichtes Decay (Dämpfung), damit die UI-Anzeige geschmeidig bleibt und nicht flackert.
                        _currentTps *= 0.85f;
                        if (_currentTps < 0.1f)
                        {
                            _currentTps = 0f;
                        }
                    }
                }

                return _currentTps;
            }
            catch
            {
                // Connection Refused oder Timeout -> Llama-Server läuft zurzeit gar nicht
                return null;
            }
        }
    }
}