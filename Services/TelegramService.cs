using System.Text;
using System.Text.Json;
using ResourceMonitorCli.Models;
using ResourceMonitorCli.Utils;

namespace ResourceMonitorCli.Services
{
    public class TelegramService(string token, string chatId, MetricsService metricsService)
    {
        private readonly HttpClient _httpClient = new();

        public async Task StartSenderAsync(int intervalMinutes, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SystemMetrics metrics = metricsService.GetSystemMetrics();
                    string messageText = FormatMetricsForTelegram(metrics);
                    await SendMessageAsync(messageText, cancellationToken);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Error sending Telegram message: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SendMessageAsync(string messageText, CancellationToken cancellationToken)
        {
            try
            {
                string url = $"https://api.telegram.org/bot{token}/sendMessage";
                var payload = new
                {
                    chat_id = chatId,
                    text = messageText,
                    parse_mode = "Markdown"
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Exception in SendMessageAsync: {ex.Message}");
                throw;
            }
        }

        private string FormatMetricsForTelegram(SystemMetrics metrics)
        {
            var sb = new StringBuilder();
            sb.AppendLine("🖥️ *Resource Monitor*");
            sb.AppendLine($"🕒 _Timestamp:_ {metrics.Timestamp:O}");

            // CPU Usage
            var cpuEmoji = FormatUtils.GetResourceEmoji(metrics.CpuUsage);
            var cpuBar = FormatUtils.GetProgressBar(metrics.CpuUsage);
            sb.AppendLine($"\n⚡ *CPU Usage:*");
            sb.AppendLine($"`{cpuBar}` {cpuEmoji} {metrics.CpuUsage:0.00}%");

            // Memory Usage
            var memEmoji = FormatUtils.GetResourceEmoji(metrics.MemoryUsage);
            var memBar = FormatUtils.GetProgressBar(metrics.MemoryUsage);
            sb.AppendLine($"\n💾 *Memory Usage:*");
            sb.AppendLine($"`{memBar}` {memEmoji} {metrics.MemoryUsage:0.00}%");

            // Disk Usage
            sb.AppendLine($"\n💽 *Disk Usage:*");
            foreach (var disk in metrics.DiskUsages)
            {
                var diskEmoji = FormatUtils.GetResourceEmoji(disk.UsagePercentage);
                var diskBar = FormatUtils.GetProgressBar(disk.UsagePercentage);
                var diskName = FormatUtils.FormatDiskName(disk.Name);
                sb.AppendLine($"\n*{diskName}*");
                sb.AppendLine($"`{diskBar}` {diskEmoji} {disk.UsagePercentage:0.00}%");
                sb.AppendLine($"Free: {FormatUtils.FormatBytes(disk.FreeSpace)} / Total: {FormatUtils.FormatBytes(disk.TotalSize)}");
            }

            return sb.ToString();
        }
    }
}