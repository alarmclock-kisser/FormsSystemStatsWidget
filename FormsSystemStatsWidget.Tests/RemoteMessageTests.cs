using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FormsSystemStatsWidget.Forms;

namespace FormsSystemStatsWidget.Tests
{
    [TestClass]
    public class RemoteMessageTests
    {
        [TestMethod]
        public void Port_IsValid_ShouldAcceptValidPorts()
        {
            Assert.IsTrue(RemoteMessage.IsValidPort(6553));
            Assert.IsTrue(RemoteMessage.IsValidPort(1));
            Assert.IsTrue(RemoteMessage.IsValidPort(65535));
        }

        [TestMethod]
        public void Port_IsValid_ShouldRejectInvalidPorts()
        {
            Assert.IsFalse(RemoteMessage.IsValidPort(0));
            Assert.IsFalse(RemoteMessage.IsValidPort(-1));
            Assert.IsFalse(RemoteMessage.IsValidPort(65536));
            Assert.IsFalse(RemoteMessage.IsValidPort(int.MaxValue));
        }

        [TestMethod]
        public void StatsMessage_ShouldRoundTripThroughJson()
        {
            var original = new RemoteMessage
            {
                Type = "stats",
                Data = new RemoteStatsData
                {
                    CpuAvgPercent = 42.5,
                    RamUsedGb = 12.345,
                    RamTotalGb = 32.0,
                    GpuPercent = 87.0,
                    GpuWatts = 210.5,
                    VramUsedGb = 6.123,
                    VramTotalGb = 16.0,
                    TrafficTitle = "Traffic",
                    TopTasks = new List<RemoteTopTask>
                    {
                        new() { Name = "chrome", CpuPercent = 12.5 },
                        new() { Name = "FSSWidget", CpuPercent = 3.2 }
                    }
                }
            };

            string json = RemoteMessage.Serialize(original);
            var roundTripped = RemoteMessage.Deserialize(json);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual("stats", roundTripped.Type);
            Assert.IsNotNull(roundTripped.Data);
            Assert.AreEqual(42.5, roundTripped.Data.CpuAvgPercent, 0.001);
            Assert.AreEqual(12.345, roundTripped.Data.RamUsedGb, 0.001);
            Assert.AreEqual(32.0, roundTripped.Data.RamTotalGb, 0.001);
            Assert.AreEqual(87.0, roundTripped.Data.GpuPercent, 0.001);
            Assert.AreEqual(210.5, roundTripped.Data.GpuWatts, 0.001);
            Assert.AreEqual(6.123, roundTripped.Data.VramUsedGb, 0.001);
            Assert.AreEqual(16.0, roundTripped.Data.VramTotalGb, 0.001);
            Assert.AreEqual(original.Data.TrafficTitle, roundTripped.Data.TrafficTitle);
            CollectionAssert.AreEquivalent(original.Data.TopTasks.Select(t => t.Name).ToArray(), roundTripped.Data.TopTasks.Select(t => t.Name).ToArray());
        }

        [TestMethod]
        public void MoveMessage_ShouldRoundTripThroughJson()
        {
            var original = new RemoteMessage { Type = "move", X = -120, Y = 340 };

            string json = RemoteMessage.Serialize(original);
            var roundTripped = RemoteMessage.Deserialize(json);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual("move", roundTripped.Type);
            Assert.AreEqual(-120, roundTripped.X);
            Assert.AreEqual(340, roundTripped.Y);
        }

        [TestMethod]
        public void ClickMessage_ShouldRoundTripThroughJson()
        {
            var original = new RemoteMessage { Type = "click", Action = "alwaysOnTop" };

            string json = RemoteMessage.Serialize(original);
            var roundTripped = RemoteMessage.Deserialize(json);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual("click", roundTripped.Type);
            Assert.AreEqual("alwaysOnTop", roundTripped.Action);
        }

        [TestMethod]
        public void HandshakeMessage_ShouldRoundTripThroughJson()
        {
            var original = new RemoteMessage { Type = "hello", ComputerName = "DESKTOP-ABC123" };

            string json = RemoteMessage.Serialize(original);
            var roundTripped = RemoteMessage.Deserialize(json);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual("hello", roundTripped.Type);
            Assert.AreEqual("DESKTOP-ABC123", roundTripped.ComputerName);
        }

        [TestMethod]
        public void Deserialize_WithInvalidJson_ShouldReturnNull()
        {
            Assert.IsNull(RemoteMessage.Deserialize("{ not valid json"));
            Assert.IsNull(RemoteMessage.Deserialize("[]"));
            Assert.IsNull(RemoteMessage.Deserialize("42"));
            Assert.IsNull(RemoteMessage.Deserialize("\"text\""));
        }

        [TestMethod]
        public void Deserialize_WithWrongMessageType_ShouldReturnNull()
        {
            string moveJson = RemoteMessage.Serialize(new RemoteMessage { Type = "move", X = 1, Y = 2 });
            Assert.AreEqual("move", RemoteMessage.Deserialize(moveJson)?.Type);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldHaveRemoteDefaults()
        {
            var settings = new WidgetPersistentSettings();

            Assert.IsFalse(settings.RemoteAllowAllConnections);
            Assert.AreEqual(6553, settings.RemotePort);
        }

        [TestMethod]
        public void WidgetPersistentSettings_ShouldRoundTripRemoteSettingsThroughJson()
        {
            var settings = new WidgetPersistentSettings
            {
                RemoteAllowAllConnections = true,
                RemotePort = 7000
            };

            string json = JsonSerializer.Serialize(settings);
            WidgetPersistentSettings? deserialized = JsonSerializer.Deserialize<WidgetPersistentSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.RemoteAllowAllConnections);
            Assert.AreEqual(7000, deserialized.RemotePort);
        }
    }
}
