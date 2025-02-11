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
            sb.AppendLine("*Resource Monitor*");
            sb.AppendLine($"_Timestamp:_ {metrics.Timestamp:O}");
            sb.AppendLine($"*CPU Usage:* {metrics.CpuUsage:0.00}%");
            sb.AppendLine($"*Memory Usage:* {metrics.MemoryUsage:0.00}%");
            sb.AppendLine("*Disk Usage:*");
            foreach (var disk in metrics.DiskUsages)
            {
                sb.AppendLine($"- {disk.Name}: {disk.UsagePercentage:0.00}% used, Free: {FormatUtils.FormatBytes(disk.FreeSpace)} / Total: {FormatUtils.FormatBytes(disk.TotalSize)}");
            }
            return sb.ToString();
        }
    }
}