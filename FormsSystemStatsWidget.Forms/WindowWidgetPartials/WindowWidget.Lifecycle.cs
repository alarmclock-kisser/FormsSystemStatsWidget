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
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.ShouldBlockClose(e))
            {
                e.Cancel = true;
                Logger.Log("[WindowWidget] Unexpected form closing was blocked.");
                return;
            }

            this.ReleaseFormClosingResources();
            base.OnFormClosing(e);
        }

        private bool ShouldBlockClose(FormClosingEventArgs e)
        {
            return e.CloseReason != CloseReason.WindowsShutDown && !this._explicitWidgetCloseRequested;
        }

        private void ReleaseFormClosingResources()
        {
            this._closing = true;
            this.UpdateTimer.Stop();
            Logger.MessageLogged -= this.HandleLoggerMessageLogged;

            if (this._debugConsoleForm != null)
            {
                try { this._debugConsoleForm.Close(); } catch { }
                this._debugConsoleForm = null;
            }

            try { this.Gpu?.Dispose(); } catch { }
            try { this.Gpu2?.Dispose(); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmSysCommand)
            {
                long command = m.WParam.ToInt64() & 0xFFF0;
                if (command == ScClose)
                {
                    this._explicitWidgetCloseRequested = true;
                }
            }

            base.WndProc(ref m);
        }
    }
}
