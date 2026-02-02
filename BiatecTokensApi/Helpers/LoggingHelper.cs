using System.Text;

namespace BiatecTokensApi.Helpers
{
    /// <summary>
    /// Helper class for secure logging operations
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Sanitizes user-provided input for safe logging to prevent log injection attacks
        /// </summary>
        /// <param name="input">The string to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        public static string SanitizeLogInput(string? input)
        {
            // Normalize null or empty values to a constant to avoid logging raw null/empty user input
            if (string.IsNullOrEmpty(input))
            {
                return "UNKNOWN";
            }

            // Remove any control characters (including newlines) that could be used for log injection
            var builder = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                // Keep only non-control characters (ASCII 0x20–0x7E and all non-ASCII)
                if (ch >= 0x20 && ch != 0x7F)
                {
                    builder.Append(ch);
                }
            }

            var sanitized = builder.ToString();

            // Enforce a maximum length to avoid log pollution from excessively long user-controlled values
            const int maxLength = 50; // Increased from 20 to allow for longer IDs/addresses
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength) + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes multiple string inputs for logging
        /// </summary>
        /// <param name="inputs">Array of strings to sanitize</param>
        /// <returns>Array of sanitized strings</returns>
        public static string[] SanitizeLogInputs(params string?[] inputs)
        {
            return inputs.Select(SanitizeLogInput).ToArray();
        }
    }
}