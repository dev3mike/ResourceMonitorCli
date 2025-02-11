using System.Text;

namespace ResourceMonitorCli.Utils
{
    public static class FormatUtils
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// Converts a byte count into a human‑readable string.
        /// </summary>
        public static string FormatBytes(ulong bytes)
        {
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < SizeUnits.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {SizeUnits[order]}";
        }

        public static string GetProgressBar(float percentage, int length = 10)
        {
            int filledLength = (int)Math.Round(length * percentage / 100.0);
            int emptyLength = length - filledLength;

            return new string('█', filledLength) + new string('░', emptyLength);
        }

        public static string GetResourceEmoji(float percentage)
        {
            return percentage switch
            {
                >= 90 => "🔴",  // Critical
                >= 75 => "🟡",  // Warning
                >= 50 => "🟢",  // Normal
                _ => "🟦"       // Low
            };
        }

        public static string FormatDiskName(string name)
        {
            return name switch
            {
                "/" => "💽 Root",
                var n when n.Contains("/boot") => "🔄 Boot",
                var n when n.Contains("/home") => "🏠 Home",
                var n when n.Contains("/var") => "📁 Var",
                var n when n.Contains("/tmp") => "⚡ Temp",
                _ => $"💿 {name}"
            };
        }
    }
}