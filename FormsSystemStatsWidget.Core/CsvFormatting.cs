using System.Globalization;

namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Reusable, culture-invariant CSV helpers shared across the application.
    /// Numbers are always formatted with <see cref="CultureInfo.InvariantCulture"/> so that
    /// generated CSV files are portable regardless of the current UI culture.
    /// </summary>
    public static class CsvFormatting
    {
        /// <summary>
        /// Formats a number using the invariant culture and the supplied numeric format string.
        /// </summary>
        public static string FormatNumber(double value, string format = "0.00")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a nullable number using the invariant culture, returning an empty string for <see langword="null"/>.
        /// </summary>
        public static string FormatNullableNumber(double? value, string format = "0.00")
        {
            return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>
        /// Escapes a single CSV field according to RFC 4180: doubles embedded quotes and wraps the
        /// value in quotes when it contains a separator, quote, or line break.
        /// </summary>
        public static string EscapeValue(string value)
        {
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            return value.Contains(';') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
                ? $"\"{value}\""
                : value;
        }
    }
}
