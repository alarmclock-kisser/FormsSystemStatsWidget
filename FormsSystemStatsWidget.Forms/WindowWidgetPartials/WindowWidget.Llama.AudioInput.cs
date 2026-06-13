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

            // Start timer for blinking effect in UI
            this._recordingBlinkerTimer.Start();

            // Auto-Stop durch Stille setzen wir hier auf null, da die Steuerung 
            // exklusiv durch den Hotkey (Toggle/Hold) erledigt wird.
            AudioObj? audioObj = await this._audioHandler.RecordAudioAutoAsync(autoStopSilenceSeconds: null);

            // Flags zurücksetzen, falls die Aufnahme anderweitig (z.B. Fehler) abbrach
            this._isAudioRecording = false;
            this._waitForToggleStop = false;

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

            // DEBUG OPEN MSGBOX
            MessageBox.Show(this, $"Audio recording completed!{Environment.NewLine}Duration: {durationStr}{Environment.NewLine}Approx. Tokens: {approxTokens}{Environment.NewLine}Base64 Size: {audioBase64.Length / 1024} KB{Environment.NewLine}{Environment.NewLine}This info would be sent to the API for processing.", "Audio Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void StopAudioRecording()
        {
            this._isAudioRecording = false;
            this._waitForToggleStop = false;
            this._audioHandler.StopRecording(); // Triggert das CancellationToken aus der neuen Methode
            this._recordingBlinkerTimer.Stop();
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
                // Aufnahme ist nicht aktiv, stelle sicher, dass der Rahmen auf Standard zurückgesetzt ist
                this.SetFormBorderColor(null);
            }
        }
    }
}
