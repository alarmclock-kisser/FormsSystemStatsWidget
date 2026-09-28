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
            Rectangle bounds = WindowWidget.ClampCmdWindowBounds(
                new Point(150, 160), new Size(800, 600), new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(150, 160, 800, 600), bounds);
        }

        [TestMethod]
        public void CmdWindowRestore_ShouldClampOffscreenBoundsToWorkingArea()
        {
            Rectangle bounds = WindowWidget.ClampCmdWindowBounds(
                new Point(-300, -100), new Size(800, 600), new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(0, 0, 800, 600), bounds);
        }
    }
}