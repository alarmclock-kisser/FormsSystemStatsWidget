using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Windows.Forms;
using FormsSystemStatsWidget.Forms;

namespace FormsSystemStatsWidget.Tests
{
    [TestClass]
    public class WindowPositionTests
    {
        [TestMethod]
        public void WidgetPersistentSettings_ShouldHaveDefaultValues()
        {
            var settings = new WidgetPersistentSettings();

            Assert.AreEqual(new Point(0, 0), settings.WidgetPosition);
            Assert.AreEqual(0, settings.WidgetMonitorId);
            Assert.AreEqual(0, settings.WidgetDockState);
            Assert.AreEqual(1.0f, settings.FontSizeScale);

            // CMD window defaults
            Assert.AreEqual(new Point(0, 0), settings.CmdWindowPosition);
            Assert.AreEqual(Size.Empty, settings.CmdWindowSize);
            Assert.AreEqual(0, settings.CmdWindowMonitorId);
            Assert.AreEqual(0, settings.CmdWindowDockState);
            Assert.AreEqual(12, settings.CmdWindowFontSize);
            Assert.AreEqual(0, settings.CmdWindowFontFamily);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldSerializeAndDeserializeCmdWindowFields()
        {
            var settings = new WidgetPersistentSettings
            {
                CmdWindowPosition = new Point(100, 200),
                CmdWindowSize = new Size(800, 600),
                CmdWindowMonitorId = 3,
                CmdWindowDockState = 2,
                CmdWindowFontSize = 14,
                CmdWindowFontFamily = 2
            };

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<WidgetPersistentSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(new Point(100, 200), deserialized.CmdWindowPosition);
            Assert.AreEqual(new Size(800, 600), deserialized.CmdWindowSize);
            Assert.AreEqual(3, deserialized.CmdWindowMonitorId);
            Assert.AreEqual(2, deserialized.CmdWindowDockState);
            Assert.AreEqual(14, deserialized.CmdWindowFontSize);
            Assert.AreEqual(2, deserialized.CmdWindowFontFamily);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldSerializeAndDeserializeWidgetFields()
        {
            var settings = new WidgetPersistentSettings
            {
                WidgetMonitorId = 2,
                WidgetPosition = new Point(100, 200),
                WidgetDockState = 1,
                FontSizeScale = 1.5f
            };

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<WidgetPersistentSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(2, deserialized.WidgetMonitorId);
            Assert.AreEqual(new Point(100, 200), deserialized.WidgetPosition);
            Assert.AreEqual(1, deserialized.WidgetDockState);
            Assert.AreEqual(1.5f, deserialized.FontSizeScale);
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldKeepSavedBoundsWhenTheyFitOnScreen()
        {
            Rectangle bounds = WindowWidget.GetCmdWindowRestoreBounds(
                new Point(150, 160), new Size(800, 600), 0, new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(150, 160, 800, 600), bounds);
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldClampOffscreenBoundsToWorkingArea()
        {
            Rectangle bounds = WindowWidget.GetCmdWindowRestoreBounds(
                new Point(-300, -100), new Size(800, 600), 0, new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(0, 0, 800, 600), bounds);
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldRecreateTopHalfSnapOnSelectedMonitor()
        {
            Rectangle selectedMonitorWorkingArea = new Rectangle(-1600, 0, 1600, 900);

            Rectangle bounds = WindowWidget.GetCmdWindowRestoreBounds(
                new Point(-1500, 20), new Size(1580, 430), 4, selectedMonitorWorkingArea);

            Assert.AreEqual(new Rectangle(-1600, 0, 1600, 450), bounds);
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldRecreateLeftAndRightHalfSnaps()
        {
            var area = new Rectangle(0, 0, 1920, 1080);

            Assert.AreEqual(new Rectangle(0, 0, 960, 1080), WindowWidget.GetCmdWindowRestoreBounds(Point.Empty, new Size(960, 1080), 2, area));
            Assert.AreEqual(new Rectangle(960, 0, 960, 1080), WindowWidget.GetCmdWindowRestoreBounds(Point.Empty, new Size(960, 1080), 3, area));
        }

        [TestMethod]
        public void CmdWindowCapture_ShouldConvertNativeRectEdgesToPositionAndSize()
        {
            Rectangle captured = WindowWidget.ConvertNativeWindowRect(-1080, 0, 0, 936);

            Assert.AreEqual(new Rectangle(-1080, 0, 1080, 936), captured);
        }

        [TestMethod]
        public void CmdWindowCapture_ShouldDetectTopHalfDockOnPortraitMonitor()
        {
            Rectangle monitorWorkingArea = new(-1080, 0, 1080, 1872);
            Rectangle captured = WindowWidget.ConvertNativeWindowRect(-1087, 0, 7, 950);

            Assert.AreEqual(4, WindowWidget.GetDockStateFromBounds(captured, monitorWorkingArea));
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldRecoverLegacyCorruptRectAndRestoreTopHalfSnap()
        {
            bool recovered = WindowWidget.TryRecoverLegacyCmdWindowBounds(
                new Point(-1087, 0), new Size(7, 943), out Rectangle recoveredBounds);
            Rectangle monitorWorkingArea = new(-1080, 0, 1080, 1872);

            Assert.IsTrue(recovered);
            Assert.AreEqual(new Rectangle(-1087, 0, 1094, 943), recoveredBounds);
            int dockState = WindowWidget.GetDockStateFromBounds(recoveredBounds, monitorWorkingArea);
            Assert.AreEqual(4, dockState);
            Assert.AreEqual(new Rectangle(-1080, 0, 1080, 936),
                WindowWidget.GetCmdWindowRestoreBounds(recoveredBounds.Location, recoveredBounds.Size, dockState, monitorWorkingArea));
        }

        [TestMethod]
        public void TerminalStart_ShouldUseAvailableTerminalAndPreserveBatchPathWithSpaces()
        {
            System.Diagnostics.ProcessStartInfo startInfo = WindowWidget.CreateTerminalStartInfo(
                @"C:\Model Load\Qwen Server.bat", "FSSWidget_Qwen_Server", new WidgetPersistentSettings());

            Assert.IsFalse(startInfo.UseShellExecute);
            if (startInfo.FileName.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase))
            {
                CollectionAssert.Contains(startInfo.ArgumentList.ToArray(), "FSSWidget_Qwen_Server");
                CollectionAssert.Contains(startInfo.ArgumentList.ToArray(), "--suppressApplicationTitle");
                CollectionAssert.Contains(startInfo.ArgumentList.ToArray(), @"call ""C:\Model Load\Qwen Server.bat""");
            }
            else
            {
                Assert.AreEqual(System.IO.Path.Combine(Environment.SystemDirectory, "cmd.exe"), startInfo.FileName);
                StringAssert.Contains(startInfo.Arguments, @"title FSSWidget_Qwen_Server & call ""C:\Model Load\Qwen Server.bat""");
            }
        }
    }
}