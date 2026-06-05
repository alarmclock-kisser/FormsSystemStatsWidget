namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Zentrale Stellschrauben für die Smart-Prompt-Optimierung im Llama-Request-Sanitizer.
    /// </summary>
    public static class SmartPromptOptimizationSettings
    {
        /// <summary>
        /// Aktiviert oder deaktiviert die Smart-Prompt-Optimierung vor der Hard-Limit-Kappung.
        /// </summary>
        public static bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Anteil des Kontextfensters, der maximal für den Prompt genutzt werden darf.
        /// </summary>
        public static double PromptSafetyRatio { get; set; } = 0.90;

        /// <summary>
        /// Anteil vom Hard-Limit, auf den Smart-Optimierung frühzeitig trimmt.
        /// </summary>
        public static double SmartBudgetRatio { get; set; } = 0.75;

        /// <summary>
        /// Schwellwert, ab dem eine Nachricht als groß gilt und komprimiert wird.
        /// </summary>
        public static int LargeMessageThresholdChars { get; set; } = 2400;

        /// <summary>
        /// Maximale Anzahl strukturierter Zeilen bei der Code-Skeleton-Reduktion.
        /// </summary>
        public static int SkeletonMaxLines { get; set; } = 60;

        /// <summary>
        /// Maximale Anzahl extrahierter Fokus-Keywords für Relevanz-Scoring.
        /// </summary>
        public static int FocusKeywordLimit { get; set; } = 12;

        /// <summary>
        /// Zusätzliche Zeichen, die bei finaler Last-Message-Kürzung bevorzugt vom Ende erhalten bleiben.
        /// </summary>
        public static int TailKeepBonusChars { get; set; } = 500;
    }
}
