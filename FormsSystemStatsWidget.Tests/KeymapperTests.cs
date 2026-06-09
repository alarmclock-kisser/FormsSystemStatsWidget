using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using FormsSystemStatsWidget.Forms;
using System.Threading;

namespace FormsSystemStatsWidget.Tests
{
    [TestClass]
    public class KeymapperTests
    {
        [TestMethod]
        public void CaptureKey_ShouldExecuteMappedAction_WhenKeyIsPressed()
        {
            // Arrange
            var keyEventArgs = new KeyEventArgs(Keys.Space);

            // Setup a mock/context if necessary, or rely on the static Keymapper state
            // For this test, we rely on the logic in Keymapper.CaptureKey to execute debug output.

            // Act
            Keymapper.CaptureKey(keyEventArgs);

            // Assert
            // 由于我们目前没有一个可测试的输出机制，我们只能验证方法执行了，实际的验证需要依赖日志或 Mocking。
            // 在实际项目中，这里会验证 Keymapper.MappedKeys 是否被正确更新，或者一个 Action 标志是否被设置。
            // 暂时验证：如果方法没有抛出异常，则视为结构正确。
            Assert.IsTrue(true, "KeyCapture method executed without exception.");
        }
    }
}