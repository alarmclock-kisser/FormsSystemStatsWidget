using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormsSystemStatsWidget.Forms.Services
{
    public enum KeyAction { Pressed, Released }

    public class HotkeyEventArgs : EventArgs
    {
        public KeyAction Action { get; }
        public HotkeyEventArgs(KeyAction action) => this.Action = action;
    }

    public class GlobalKeyboardHook : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed = false;

        public event EventHandler<HotkeyEventArgs>? HotkeyTriggered;

        public Keys TargetKey { get; set; }
        public Keys[] TargetModifiers { get; set; } = [];

        public void Start()
        {
            if (this._hookId != IntPtr.Zero)
            {
                return;
            }

            this._proc = this.HookCallback;
            this._hookId = SetWindowsHookEx(WH_KEYBOARD_LL, this._proc, GetModuleHandle(string.Empty), 0);
            if (this._hookId == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to install hook.");
            }
        }

        public void Stop()
        {
            if (this._hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(this._hookId);
                this._hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool isKeyDown = (wParam == (IntPtr) WM_KEYDOWN || wParam == (IntPtr) WM_SYSKEYDOWN);
                bool isKeyUp = (wParam == (IntPtr) WM_KEYUP || wParam == (IntPtr) WM_SYSKEYUP);

                if (isKeyDown || isKeyUp)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys pressedKey = (Keys) vkCode;

                    if (pressedKey == this.TargetKey)
                    {
                        bool modifiersHeld = this.TargetModifiers.All(this.IsModifierHeld);

                        if (modifiersHeld)
                        {
                            KeyAction action = isKeyDown ? KeyAction.Pressed : KeyAction.Released;
                            HotkeyTriggered?.Invoke(this, new HotkeyEventArgs(action));
                        }
                    }
                }
            }
            return CallNextHookEx(this._hookId, nCode, wParam, lParam);
        }

        private bool IsModifierHeld(Keys modifier)
        {
            int virtualKey = modifier switch
            {
                Keys.Control => (int) Keys.ControlKey,
                Keys.Shift => (int) Keys.ShiftKey,
                Keys.Alt => (int) Keys.Menu,
                _ => (int) modifier
            };

            return (GetKeyState(virtualKey) & 0x8000) != 0;
        }

        public void Dispose() { this.Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed) { if (disposing) { this.Stop(); } this._disposed = true; }
        }
        ~GlobalKeyboardHook() => this.Dispose(false);
    }
}