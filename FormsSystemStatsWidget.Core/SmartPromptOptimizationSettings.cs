namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Central tuning options for smart prompt optimization in the Llama request sanitizer.
    /// </summary>
    public static class SmartPromptOptimizationSettings
    {
        /// <summary>
        /// Enables or disables smart prompt optimization before hard-limit trimming.
        /// </summary>
        public static bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Maximum share of the context window that may be used for the prompt.
        /// </summary>
        public static double PromptSafetyRatio { get; set; } = 0.90;

        /// <summary>
        /// Share of the hard limit that smart optimization trims to early.
        /// </summary>
        public static double SmartBudgetRatio { get; set; } = 0.75;

        /// <summary>
        /// Threshold at which a message is considered large and gets compressed.
        /// </summary>
        public static int LargeMessageThresholdChars { get; set; } = 2400;

        /// <summary>
        /// Maximum number of structural lines for code skeleton reduction.
        /// </summary>
        public static int SkeletonMaxLines { get; set; } = 60;

        /// <summary>
        /// Maximum number of extracted focus keywords for relevance scoring.
        /// </summary>
        public static int FocusKeywordLimit { get; set; } = 12;

        /// <summary>
        /// Extra characters that are preferentially preserved from the end during final last-message trimming.
        /// </summary>
        public static int TailKeepBonusChars { get; set; } = 500;

        /// <summary>
        /// Enables or disables strict tool calling rules injection.
        /// </summary>
        public static bool InjectStrictToolCallingRules { get; set; } = true;

        public static string StrictToolCallingRulesInjectionPrompt { get; set; } = "\n\n[CRITICAL SYSTEM INSTRUCTIONS FOR TOOLS & EDITS]\n" +
                           "1. NEVER wrap tool calls in Markdown code blocks (e.g., " + "``" + "`json). Output the raw JSON tool format directly.\n" +
                           "2. When using file edit/replace tools, your indentation and leading spaces MUST EXACTLY MATCH the original source file. Do not strip leading spaces.\n" +
                           "3. Output ONLY the valid JSON tool call. Use the exact schema: {\"name\": \"function_name\", \"arguments\": {...}} without extra conversational text.";
    }
}
