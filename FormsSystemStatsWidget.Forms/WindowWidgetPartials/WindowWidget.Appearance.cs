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
        private void toolStripTextBox_diagramColor_TextChanged(object sender, EventArgs e)
        {
            string hex = this.toolStripTextBox_diagramColor.Text.Replace("#", "");
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                // rgb is RRGGBB; ensure the color is created opaque by adding alpha 0xFF
                this._diagramColor = Color.FromArgb(unchecked((int) 0xFF000000 | rgb));
                this._persistentSettings.DiagramColorHex = $"#{hex.ToUpperInvariant()}";
                this.SavePersistentSettings();
            }
        }

        private void toolStripTextBox_percentageColor_EnabledChanged(object sender, EventArgs e)
        {
            // Do not clear the stored color when disabling; keep the selected color so it can be re-enabled.
            if (this.toolStripTextBox_percentageColor.Enabled)
            {
                this.toolStripTextBox_percentageColor.Text = this._percentageColor.HasValue ? $"#{this._percentageColor.Value.ToArgb() & 0xFFFFFF:X6}" : "";
            }
        }

        private void toolStripTextBox_diagramColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._diagramColor;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._diagramColor = colorDialog.Color;
                    this.toolStripTextBox_diagramColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                }
            }
        }

        private void toolStripTextBox_percentageColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._percentageColor ?? Color.White;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._percentageColor = colorDialog.Color;
                    this.toolStripTextBox_percentageColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                    this.toolStripTextBox_percentageColor.Enabled = true;
                }
            }
        }

        private void alwaysOnTopToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = this.alwaysOnTopToolStripMenuItem.Checked;
            _ = (this._debugConsoleForm?.TopMost = this.alwaysOnTopToolStripMenuItem.Checked);
            this._persistentSettings.AlwaysOnTop = this.alwaysOnTopToolStripMenuItem.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_opacity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.toolStripTextBox_opacity.Text = this.toolStripTextBox_opacity.Text.Trim().Replace(" ", "").Replace("%", "") + "%";

            float opacity = 0.0f;
            if (float.TryParse(this.toolStripTextBox_opacity.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedOpacity))
            {
                opacity = parsedOpacity;
            }
            else
            {
                // Try parse with percentage sign, e.g. "80%" or as int
                if (this.toolStripTextBox_opacity.Text.EndsWith("%") && float.TryParse(this.toolStripTextBox_opacity.Text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPercentage))
                {
                    opacity = parsedPercentage / 100f;
                }
                else if (int.TryParse(this.toolStripTextBox_opacity.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                {
                    opacity = parsedInt / 100f;
                }
                else
                {
                    opacity = 0.0f;
                }
            }

            opacity = Math.Clamp(opacity, 0.1f, 1.0f);
            this.toolStripTextBox_opacity.Text = (opacity >= 0.99f ? "100" : (opacity * 100).ToString("0")) + "%";

            // Set form + elements + border opacity 
            // this.Opacity = opacity;

            // Wende die Opacity auf alle direkt enthaltenen Controls an
            int initialStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            _ = SetWindowLong(this.Handle, GWL_EXSTYLE, initialStyle | WS_EX_LAYERED);
            _ = SetLayeredWindowAttributes(this.Handle, 0, (byte) (opacity * 255), 0x00000002);
            if (this._debugConsoleForm != null)
            {
                this._debugConsoleForm.ApplyOpacity(opacity);
            }
            CustomOpacity = opacity;

            // Save persistent settings
            this._persistentSettings.WindowOpacity = (int) (opacity * 100);
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_blackOutMode_CheckedChanged(Object sender, EventArgs e)
        {
            bool useBlackOutMode = this.toolStripMenuItem_blackOutMode.Checked;

            // Call the helper that actually does the visual transformation
            this.ApplyBlackOutMode(useBlackOutMode);

            // Remember the choice so the widget can be opened in the same state next time
            this._persistentSettings.BlackOutMode = useBlackOutMode;
            this.SavePersistentSettings();
        }

        private void ApplyBlackOutMode(bool enabled)
        {
            // 1️⃣ Window handle – colour the layered window background black and set foreground to white
            if (this.IsHandleCreated)
            {
                // Make sure the window can receive layered‑window attributes
                int style = GetWindowLong(this.Handle, GWL_EXSTYLE);
                _ = SetWindowLong(this.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED);

                // Apply the colour transformation – black background, white text
                // LWA_COLORKEY = 0x0001, LWA_ALPHA = 0x0002 – we use colour‑key with alpha 255 (opaque)

                // Scale current opacity float to byte 0..255
                byte alpha = (byte) (CustomOpacity >= 0.1f && CustomOpacity <= 1.0f ? CustomOpacity * 255 : 255);

                SetLayeredWindowAttributes(this.Handle, 0x0, alpha, 0x0002);
            }

            // 2️⃣ Form background / foreground
            if (enabled)
            {
                this.BackColor = Color.Black;
                this.ForeColor = Color.White;
            }
            else
            {
                // Restore default colours (Control skin)
                this.BackColor = SystemColors.Control;
                this.ForeColor = SystemColors.ControlText;
            }

            // 3️⃣ Recursively adjust all Label controls
            foreach (Control ctrl in this.Controls)
            {
                AdjustLabelColours(ctrl, enabled);
            }

            // 4️⃣ Invert the Statistics‑Recorder button colours
            var controls = this.Controls;
            foreach (Control ctrl in controls)
            {
                InvertStatisticsRecorderButton(ctrl);
            }

            BlackOutModeEnabled = enabled;
            this._debugConsoleForm?.ApplyBlackOutMode(enabled);
        }

        private static void AdjustLabelColours(Control ctrl, bool enable)
        {
            if (ctrl is Label lbl)
            {
                // Target colour: grey‑white when black‑out is on, black when it is off
                Color target = enable ? Color.FromArgb(0xCCCCCC) : Color.Black;

                // If the label currently uses the default Control text colour or plain black, switch it
                if (lbl.ForeColor == SystemColors.ControlText || lbl.ForeColor == Color.Black)
                {
                    lbl.ForeColor = target;
                }

                // Ensure the label background stays black when the mode is active
                if (enable && lbl.BackColor != Color.Black)
                {
                    lbl.BackColor = Color.Black;
                }
            }

            // Recurse into child controls
            foreach (Control child in ctrl.Controls)
            {
                AdjustLabelColours(child, enable);
            }
        }

        private static void InvertStatisticsRecorderButton(Control ctrl)
        {
            if (ctrl is Button btn && btn.Name.Equals("button_recordUsages", StringComparison.OrdinalIgnoreCase))
            {
                Color swap = btn.BackColor;
                btn.BackColor = btn.ForeColor;
                btn.ForeColor = swap;
                return;
            }

            foreach (Control child in ctrl.Controls)
            {
                InvertStatisticsRecorderButton(child);
            }
        }
    }
}
