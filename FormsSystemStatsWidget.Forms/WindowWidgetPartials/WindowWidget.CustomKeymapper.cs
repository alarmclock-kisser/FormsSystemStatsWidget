
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        // --- 1. Global Keyboard Hook P/Invoke Definitions ---
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // --- 2. Keymapper State & Fields ---
        private enum KeymapperState { Idle, WaitingForTarget, RecordingMapping }

        private KeymapperState _currentState = KeymapperState.Idle;
        private KeyMappingEntry? _tempEntry;
        private DateTime _recordingStartTime;

        // Temporary collections for the recording phase
        private readonly List<Keys> _recordedKeys = [];
        private readonly List<bool> _recordedKeyDown = [];
        private readonly List<TimeSpan> _recordedTimings = [];

        // Call this in your Form_Load or Constructor
        private void InitializeKeymapper()
        {
            this._proc = this.HookCallback;
            this._hookID = this.SetHook(this._proc);

            // Ensure hook is removed when application closes
            this.FormClosing += (s, e) => UnhookWindowsHookEx(this._hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule!;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        // --- 3. The Interactive Recording Logic ---
        private void toolStripMenuItem_remapAnyKey_Click(object sender, EventArgs e)
        {
            if (this._currentState == KeymapperState.Idle)
            {
                // Step 1: Init recording mode
                this._currentState = KeymapperState.WaitingForTarget;
                this.toolStripMenuItem_remapAnyKey.Text = "Press any key (target) ...";
                this._tempEntry = new KeyMappingEntry();
            }
            else if (this._currentState == KeymapperState.RecordingMapping)
            {
                // Step 2.1: User clicked again -> Finish recording and save
                this._currentState = KeymapperState.Idle;
                this.toolStripMenuItem_remapAnyKey.Text = "Remap another key";

                if (this._tempEntry != null && this._recordedKeys.Count > 0)
                {
                    this._tempEntry.MappedKeyValues = [.. this._recordedKeys];
                    this._tempEntry.MappedKeyDownValues = [.. this._recordedKeyDown];
                    this._tempEntry.MappedKeyTimings = this._recordedTimings.Select(t => (TimeSpan?) t).ToArray();
                    this._tempEntry.Enabled = true;

                    Keymapper.MappedKeys.Add(this._tempEntry);
                }

                // Cleanup session
                this._recordedKeys.Clear();
                this._recordedKeyDown.Clear();
                this._recordedTimings.Clear();
                this._tempEntry = null;
            }
        }

        // --- 4. The Global Listener & Interceptor ---
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys) vkCode;
                bool isKeyDown = (wParam == (IntPtr) WM_KEYDOWN || wParam == (IntPtr) WM_SYSKEYDOWN);

                switch (this._currentState)
                {
                    case KeymapperState.WaitingForTarget:
                        if (isKeyDown)
                        {
                            // 1.1: Grab the target key
                            this._tempEntry!.TargetKey = key;
                            this._currentState = KeymapperState.RecordingMapping;

                            // Using Invoke to safely update UI from the hook thread
                            this.Invoke(() =>
                            {
                                this.toolStripMenuItem_remapAnyKey.Text = $"[{key}] captured. Type combo, then click here to save.";
                            });

                            this._recordingStartTime = DateTime.Now;
                            return 1; // Block the target key from passing to the OS
                        }
                        break;

                    case KeymapperState.RecordingMapping:
                        // 2.0: Record the sequence (both Down and Up events)
                        this._recordedKeys.Add(key);
                        this._recordedKeyDown.Add(isKeyDown);
                        this._recordedTimings.Add(DateTime.Now - this._recordingStartTime);
                        return 1; // Block keys from OS while recording

                    case KeymapperState.Idle:
                        // Normal operation: intercept and execute mapped keys
                        if (isKeyDown) // Only trigger on KeyDown to prevent double execution
                        {
                            var activeMapping = Keymapper.MappedKeys.FirstOrDefault(m => m.TargetKey == key && m.Enabled);
                            if (activeMapping != null)
                            {
                                // Fire and forget the playback
                                _ = this.ExecuteMappingAsync(activeMapping);
                                return 1; // Block the original keypress!
                            }
                        }
                        break;
                }
            }

            // Let the keypress pass through if we didn't block it
            return CallNextHookEx(this._hookID, nCode, wParam, lParam);
        }

        // --- 5. Playback Execution ---
        private async Task ExecuteMappingAsync(KeyMappingEntry mapping)
        {
            // Execute String Input if defined
            if (!string.IsNullOrEmpty(mapping.MappedInputStringValue))
            {
                if (mapping.EnterInputStringSerially)
                {
                    foreach (char c in mapping.MappedInputStringValue)
                    {
                        SendKeys.SendWait(c.ToString());
                        await Task.Delay(100); // 100ms serial delay
                    }
                }
                else
                {
                    SendKeys.SendWait(mapping.MappedInputStringValue);
                }
                return;
            }

            // Execute Key Sequences if defined
            if (mapping.MappedKeyValues != null && mapping.MappedKeyTimings != null)
            {
                TimeSpan lastTiming = TimeSpan.Zero;

                for (int i = 0; i < mapping.MappedKeyValues.Length; i++)
                {
                    Keys keyToPress = mapping.MappedKeyValues[i];
                    bool isDown = mapping.MappedKeyDownValues?[i] ?? true;
                    TimeSpan currentTiming = mapping.MappedKeyTimings[i] ?? TimeSpan.FromMilliseconds(i * 50);

                    // Wait for the delta between the last key event and this one
                    TimeSpan delay = currentTiming - lastTiming;
                    if (delay.TotalMilliseconds > 0)
                    {
                        await Task.Delay(delay);
                    }

                    // For broad compatibility in standard Windows environments, 
                    // SendKeys is used here. For low-level game-injection, you would 
                    // replace this with a user32.dll SendInput P/Invoke wrapper.
                    if (isDown)
                    {
                        // Wrap in simple format for SendKeys (modifiers need special handling depending on complexity)
                        string keyString = "{" + keyToPress.ToString().ToUpper() + "}";
                        try { SendKeys.SendWait(keyString); } catch { /* Handle special keys mapping if needed */ }
                    }

                    lastTiming = currentTiming;
                }
            }
        }
    }
}



    internal static class Keymapper
{
    internal static List<KeyMappingEntry> MappedKeys = [];

    public static void CaptureKey(KeyEventArgs e)
    {
        // 1. Überprüfung auf einfache Key-Code-Abmatches (MappedKeyCodeValues)
        foreach (var entry in MappedKeys)
        {
            if (entry.MappedKeyCodeValues != null && entry.MappedKeyCodeValues.ToList().Contains(e.KeyCode.ToString()))
            {
                // Aktion ausführen (Platzhalter)
                System.Diagnostics.Debug.WriteLine($"Mapped Key Action Executed for KeyCode: {e.KeyCode}");
                return;
            }
        }

        // 2. Überprüfung auf TargetKey-Abmatches (TargetKey)
        foreach (var entry in MappedKeys)
        {
            if (entry.TargetKey != null && entry.TargetKey.Equals(e.KeyCode))
            {
                // Aktion ausführen (Platzhalter)
                System.Diagnostics.Debug.WriteLine($"Mapped Target Key Action Executed for Key: {e.KeyCode}");
                return;
            }
        }

        // Hier müsste die Logik für MappedKeyDownValues, Timings und InputStringSerially implementiert werden.
    }
}

internal class KeyMappingEntry
{
    // The key that the user has selected to be mapped
    internal Keys? TargetKey { get; set; }
    internal string? TargetKeyFallbackString { get; set; } = null;

    // The key or action-chain/-combination that the user has selected to be triggered when the TargetKey is pressed
    internal Keys[]? MappedKeyValues { get; set; } = null; // Key(s) to trigger, preferred if available through System.Windows.Forms.Keys
    internal string[]? MappedKeyCodeValues { get; set; } = null; // Fallback for key codes, if keys are not available or special
    internal bool[]? MappedKeyDownValues { get; set; } = null; // true for key down, false for key up, if null/empty/mismatching, assume all down events
    internal TimeSpan?[]? MappedKeyTimings { get; set; } = null; // Timings for series, for combos if element is null, if null/empty/mismatching, assume modifyer+combo are simultaneous, others with 500ms delya

    internal string? MappedInputStringValue { get; set; } = null; // Will be pasted/inputted/entered, char-wise ('fake-manually')
    internal bool EnterInputStringSerially { get; set; } = false; // If true and MappedInputStringValue is not null, enter char-wise with a short delay in between (about 100ms)

    // Toggle / switch
    internal bool Enabled { get; set; } = true;




}


