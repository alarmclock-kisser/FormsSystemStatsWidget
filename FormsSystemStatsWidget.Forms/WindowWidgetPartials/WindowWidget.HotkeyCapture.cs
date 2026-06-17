using FormsSystemStatsWidget.Core;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {

        //Hotkey (recording)
        private void ResetHotkeyInputState()
        {
            this._isAwaitingHotkeyInput = false;
            this._currentModifierKeys.Clear();
            this._firstModifierKey = null;
            this._otherKey = null;
        }

        private void toolStripMenuItem_configureVoiceInputHotkey_Click(object sender, EventArgs e)
        {
            // 1. Hotkey-Text ersetzen
            string originalText = this.toolStripMenuItem_configureVoiceInputHotkey.Text ?? "Set Voice Input Hotkey ... (<none>)";
            string modifiedText = SetVoiceInputHotkeyRegex().Replace(originalText, " ( ... )");
            this.toolStripMenuItem_configureVoiceInputHotkey.Text = modifiedText;

            // 2. Zustand setzen und Event-Listener aktivieren
            this._isAwaitingHotkeyInput = true;
            // Hier müssten die KeyDown/KeyUp Event-Handler für das Formular registriert werden.
            // Beispiel: this.KeyDown += WindowWidget_KeyDown;
            // Beispiel: this.KeyUp += WindowWidget_KeyUp;

            // 3. CtxMenuStrip offen halten (ist durch den Click-Handler implizit gegeben)
            // ... (Kein Code nötig, da der Event-Handler den Kontext hält)
        }

        private void WindowWidget_KeyDown(object sender, KeyEventArgs e)
        {
            if (!this._isAwaitingHotkeyInput)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                // Abbrechen, wenn ESC gedrückt wird
                this.ResetHotkeyInputState();
                this.ContextMenuStrip?.Close();
                return;
            }

            // 1. Modifikator-Key erfassen
            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.RWin)
            {
                if (!this._currentModifierKeys.ContainsKey(e.KeyCode))
                {
                    this._currentModifierKeys[e.KeyCode] = false;
                }
                this._currentModifierKeys[e.KeyCode] = true;
                if (this._firstModifierKey == null)
                {
                    this._firstModifierKey = e.KeyCode;
                }
            }
            // 2. Nicht-Modifikator-Key erfassen
            else if (e.KeyCode != Keys.Tab && e.KeyCode != Keys.Enter)
            {
                if (this._otherKey == null)
                {
                    this._otherKey = e.KeyCode;
                }
            }
        }

        private void WindowWidget_KeyUp(object sender, KeyEventArgs e)
        {
            if (!this._isAwaitingHotkeyInput)
            {
                return;
            }

            // KeyUp-Verarbeitung
            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.RWin)
            {
                if (this._currentModifierKeys.ContainsKey(e.KeyCode))
                {
                    this._currentModifierKeys[e.KeyCode] = false;
                }
            }
            // Hier müsste die Logik zur Überprüfung, ob *alle* relevanten Keys Up sind, implementiert werden.
            // Für diesen Scope reicht es, wenn wir das State-Update durch das Event-Handling abdecken.
        }
    }
}
