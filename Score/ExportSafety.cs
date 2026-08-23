using System;
using System.IO;
using System.Linq;

namespace Score
{
    public static class ExportSafety
    {
        private static readonly string[] ReservedWindowsNames =
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string ToCsvCell(string value)
        {
            var safeValue = value ?? string.Empty;
            var firstNonWhitespace = safeValue.SkipWhile(char.IsWhiteSpace).FirstOrDefault();
            if (firstNonWhitespace == '=' || firstNonWhitespace == '+' || firstNonWhitespace == '-' || firstNonWhitespace == '@')
                safeValue = "'" + safeValue;
            return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }

        public static string SanitizeFileName(string value, string fallback)
        {
            var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) candidate = candidate.Replace(invalid, '-');
            candidate = candidate.Trim().TrimEnd('.');
            if (candidate.Length > 80) candidate = candidate.Substring(0, 80).TrimEnd();
            if (ReservedWindowsNames.Contains(candidate, StringComparer.OrdinalIgnoreCase)) candidate = candidate + "-quiz";
            return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
        }
    }
}
