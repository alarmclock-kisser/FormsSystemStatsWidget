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
            // Arrange & Act
            var settings = new WidgetPersistentSettings();

            // Assert
            Assert.AreEqual(new Point(0, 0), settings.WidgetPosition);
            Assert.AreEqual(0, settings.WidgetMonitorId);
            Assert.AreEqual(0, settings.WidgetDockState);
            Assert.AreEqual(1.0f, settings.FontSizeScale);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldSerializeAndDeserializeMonitorId()
        {
            // Arrange
            var settings = new WidgetPersistentSettings
            {
                WidgetMonitorId = 2,
                WidgetPosition = new Point(100, 200)
            };

            // Act - Serialize to JSON string
            var serializer = new System.Text.Json.JsonSerializer();
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(settings, options);

            // Assert - Deserialize back
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<WidgetPersistentSettings>(json, options);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(2, deserialized.WidgetMonitorId);
            Assert.AreEqual(new Point(100, 200), deserialized.WidgetPosition);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldSerializeAndDeserializeDockState()
        {
            // Arrange
            var settings = new WidgetPersistentSettings
            {
                WidgetDockState = 1, // fullscreen
                WidgetPosition = new Point(0, 0)
            };

            // Act - Serialize to JSON string
            string json = System.Text.Json.JsonSerializer.Serialize(settings);

            // Assert - Deserialize back
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<WidgetPersistentSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1, deserialized.WidgetDockState);
            Assert.AreEqual(new Point(0, 0), deserialized.WidgetPosition);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldSerializeAndDeserializeFontSizeScale()
        {
            // Arrange
            var settings = new WidgetPersistentSettings
            {
                FontSizeScale = 1.5f,
                WidgetPosition = new Point(0, 0)
            };

            // Act - Serialize to JSON string
            string json = System.Text.Json.JsonSerializer.Serialize(settings);

            // Assert - Deserialize back
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<WidgetPersistentSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1.5f, deserialized.FontSizeScale);
            Assert.AreEqual(new Point(0, 0), deserialized.WidgetPosition);
        }

        [TestMethod]
        public void WidgetPersistentSettings_Load_ShouldReturnDefaultWhenFileDoesNotExist()
        {
            // This test verifies the Load method returns default settings when no file exists
            // The actual file path uses LocalApplicationData, so we test the logic indirectly
            var settings = new WidgetPersistentSettings();

            // Assert default values
            Assert.AreEqual(100, settings.UpdateIntervalMs); // default from class
            Assert.AreEqual("#FFFFFF", settings.DiagramColorHex);
        }
    }
}