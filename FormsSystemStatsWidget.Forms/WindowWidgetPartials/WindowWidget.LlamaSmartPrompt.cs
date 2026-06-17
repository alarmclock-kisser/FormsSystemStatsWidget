using FormsSystemStatsWidget.Core;
using System.Globalization;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private static string FormatRatio(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private void enableSmartPromptOptimizationsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            SmartPromptOptimizationSettings.IsEnabled = this.enableSmartPromptOptimizationsToolStripMenuItem.Checked;
            this._persistentSettings.SmartPromptEnabled = SmartPromptOptimizationSettings.IsEnabled;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_promptSafetyRatio_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_promptSafetyRatio,
                (string text, out double value) => TryParseDouble(text, out value) && value > 0.1 && value <= 1.0,
                FormatRatio,
                "Please enter a valid number between 0.10 and 1.00 for prompt safety ratio.",
                FormatRatio(SmartPromptOptimizationSettings.PromptSafetyRatio),
                value =>
                {
                    SmartPromptOptimizationSettings.PromptSafetyRatio = value;
                    this._persistentSettings.SmartPromptSafetyRatio = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_smartBudgetRatio_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_smartBudgetRatio,
                (string text, out double value) => TryParseDouble(text, out value) && value > 0.1 && value <= 1.0,
                FormatRatio,
                "Please enter a valid number between 0.10 and 1.00 for smart budget ratio.",
                FormatRatio(SmartPromptOptimizationSettings.SmartBudgetRatio),
                value =>
                {
                    SmartPromptOptimizationSettings.SmartBudgetRatio = value;
                    this._persistentSettings.SmartPromptBudgetRatio = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_largeMessageThresholdChars_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_largeMessageThresholdChars,
                (string text, out int value) => int.TryParse(text, out value) && value >= 256,
                FormatInvariant,
                "Please enter a valid integer >= 256 for large message threshold chars.",
                FormatInvariant(SmartPromptOptimizationSettings.LargeMessageThresholdChars),
                value =>
                {
                    SmartPromptOptimizationSettings.LargeMessageThresholdChars = value;
                    this._persistentSettings.SmartPromptLargeMessageThresholdChars = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_skeletonMaxLines_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_skeletonMaxLines,
                (string text, out int value) => int.TryParse(text, out value) && value >= 5,
                FormatInvariant,
                "Please enter a valid integer >= 5 for skeleton max lines.",
                FormatInvariant(SmartPromptOptimizationSettings.SkeletonMaxLines),
                value =>
                {
                    SmartPromptOptimizationSettings.SkeletonMaxLines = value;
                    this._persistentSettings.SmartPromptSkeletonMaxLines = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_focusKeywordLimit_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_focusKeywordLimit,
                (string text, out int value) => int.TryParse(text, out value) && value >= 1,
                FormatInvariant,
                "Please enter a valid integer >= 1 for focus keyword limit.",
                FormatInvariant(SmartPromptOptimizationSettings.FocusKeywordLimit),
                value =>
                {
                    SmartPromptOptimizationSettings.FocusKeywordLimit = value;
                    this._persistentSettings.SmartPromptFocusKeywordLimit = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_tailKeepBonusChars_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_tailKeepBonusChars,
                (string text, out int value) => int.TryParse(text, out value) && value >= 0,
                FormatInvariant,
                "Please enter a valid integer >= 0 for tail keep bonus chars.",
                FormatInvariant(SmartPromptOptimizationSettings.TailKeepBonusChars),
                value =>
                {
                    SmartPromptOptimizationSettings.TailKeepBonusChars = value;
                    this._persistentSettings.SmartPromptTailKeepBonusChars = value;
                    this.SavePersistentSettings();
                });
        }
    }
}
