using FormsSystemStatsWidget.Core;
using FormsSystemStatsWidget.Forms.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        // Audio recording
        private readonly AudioHandling _audioHandler = new();
        private DateTime? _hotkeyDownTime = null;
        private bool _isAudioRecording = false;
        private bool _waitForToggleStop = false;

        // Hotkey combo keys for audio recording (Ctrl + RShift)
        private GlobalKeyboardHook? _audioHotkeyListener;
        internal Keys[] AudioRecordingHotkeyModifiers { get; private set; } = [Keys.Control];
        internal Keys AudioRecordingHotkeyKey { get; private set; } = Keys.R;

        private Timer _recordingBlinkerTimer = new() { Interval = 250 };



        private void InitializeAudioHotkeys()
        {
            this._audioHotkeyListener = new GlobalKeyboardHook()
            {
                TargetKey = this.AudioRecordingHotkeyKey,
                TargetModifiers = this.AudioRecordingHotkeyModifiers
            };
            this._audioHotkeyListener.HotkeyTriggered += this.OnAudioRecordingHotkeyPressed;
            this._audioHotkeyListener.Start();

            this._recordingBlinkerTimer.Tick += async (s, e) => await this.RecordingBlinkerTimer_Tick(s, e);
        }

        private void OnAudioRecordingHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            if (e.Action == KeyAction.Pressed)
            {
                if (this._isAudioRecording)
                {
                    // Aufnahme läuft bereits. Warten wir auf den zweiten Klick zum Stoppen? (Toggle-Mode)
                    if (this._waitForToggleStop)
                    {
                        this.StopAudioRecording();
                    }
                    // Falls _waitForToggleStop FALSE ist, hält der User die Taste gerade gedrückt
                    // und Windows feuert wiederholt 'Pressed'. Diese ignorieren wir einfach.
                }
                else
                {
                    // 1. Klick -> Neue Aufnahme starten
                    this._hotkeyDownTime = DateTime.UtcNow;
                    this._isAudioRecording = true;
                    this._waitForToggleStop = false;

                    // Als Fire & Forget Task starten, um den globalen Keyboard-Hook nicht zu blockieren
                    _ = Task.Run(this.StartAudioRecordingAsync);
                }
            }
            else if (e.Action == KeyAction.Released)
            {
                if (this._isAudioRecording && !this._waitForToggleStop && this._hotkeyDownTime.HasValue)
                {
                    // Wie lange wurde die Kombi gedrückt?
                    double pressDurationMs = (DateTime.UtcNow - this._hotkeyDownTime.Value).TotalMilliseconds;

                    if (pressDurationMs > 1000)
                    {
                        // HOLD-MODE: Wurde länger als 1000ms gehalten -> Beim Loslassen direkt stoppen
                        this.StopAudioRecording();
                    }
                    else
                    {
                        // TOGGLE-MODE: War ein kurzer Hit (< 1000ms) -> Aufnahme läuft weiter, 
                        // wir setzen das Flag, damit der nächste Klick sie stoppt.
                        this._waitForToggleStop = true;
                    }
                }
            }
        }

        private async Task StartAudioRecordingAsync()
        {
            Logger.Log("Audio recording triggered via hotkey...");

            this.RunOnUiThread(() => this._recordingBlinkerTimer.Start());

            // Silence-based auto-stop is disabled here because control is handled
            // exclusively by the hotkey (toggle/hold).
            AudioObj? audioObj = await this._audioHandler.RecordAudioAutoAsync(autoStopSilenceSeconds: null);

            // Reset flags if recording ended through another path (for example an error)
            this._isAudioRecording = false;
            this._waitForToggleStop = false;
            this.RunOnUiThread(() =>
            {
                this._recordingBlinkerTimer.Stop();
                this.SetFormBorderColor(null);
            });

            if (audioObj == null)
            {
                Logger.Log("Audio recording failed or was cancelled.");
                return;
            }

            Logger.Log($"Audio captured: {audioObj.Name} (Duration: {audioObj.Duration.TotalSeconds:F1}s)");

            string? audioBase64 = await audioObj.SerializeAsBase64Async(16000, 1, 16);
            if (string.IsNullOrEmpty(audioBase64))
            {
                Logger.Log("Failed to serialize audio to Base64.");
                return;
            }

            int approxTokens = (int) (audioObj.Duration.TotalSeconds * 50);
            string durationStr = $"{(int) audioObj.Duration.TotalMinutes:D2}:{audioObj.Duration.Seconds:D2}";
            string uiNotification = $" + 🎤 ({durationStr} | ~{approxTokens} tok.)";

            if (!LlamaOllamaBridge.IsRunning)
            {
                Logger.Log("[AudioInput] Llama-Ollama bridge is not active. Audio will not be sent.");
                return;
            }

            bool sent = await LlamaOllamaBridge.SendAudioInputAsync(uiNotification, audioBase64);
            if (!sent)
            {
                Logger.Log("[AudioInput] Sending the audio input to the bridge failed.");
                return;
            }

            Logger.Log($"[AudioInput] Audio was successfully sent to the bridge.{uiNotification}");
        }

        private void StopAudioRecording()
        {
            this._isAudioRecording = false;
            this._waitForToggleStop = false;
            this._audioHandler.StopRecording(); // Triggers the cancellation token from the recording method
            this.RunOnUiThread(() =>
            {
                this._recordingBlinkerTimer.Stop();
                this.SetFormBorderColor(null);
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(action);
                return;
            }

            action();
        }

        private void SetFormBorderColor(Color? color = null)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => { this.SetFormBorderColor(color); }));
            }

            if (!color.HasValue)
            {
                this.FormBorderColor = SystemColors.ControlDark;
                this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.FormBorderColor = color.Value;
            }
        }


        private async Task RecordingBlinkerTimer_Tick(object? sender, EventArgs e)
        {
            if (this._isAudioRecording)
            {
                if (this.FormBorderStyle == FormBorderStyle.FixedToolWindow)
                {
                    this.SetFormBorderColor(Color.Red);
                }
                else
                {
                    this.SetFormBorderColor(null);
                }
            }
            else
            {
                // Recording is not active, ensure the border is reset to the default state
                this.SetFormBorderColor(null);
            }
        }
    }
}
