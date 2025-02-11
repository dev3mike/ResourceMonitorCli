namespace ResourceMonitorCli.Utils
{
    public static class FormatUtils
    {
        /// <summary>
        /// Converts a byte count into a human‑readable string.
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            var len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}