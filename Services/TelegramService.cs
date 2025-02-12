using System.Text;
using System.Text.Json;
using ResourceMonitorCli.Models;
using ResourceMonitorCli.Utils;

namespace ResourceMonitorCli.Services
{
    public class TelegramService(string token, string chatId, MetricsService metricsService)
    {
        private readonly HttpClient _httpClient = new();
        private long? _lastMessageId;

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

        private async Task DeleteLastMessageAsync(CancellationToken cancellationToken)
        {
            if (_lastMessageId == null) return;

            try
            {
                string url = $"https://api.telegram.org/bot{token}/deleteMessage";
                var payload = new
                {
                    chat_id = chatId,
                    message_id = _lastMessageId
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

                // Don't throw if deletion fails, just log the error
                if (!response.IsSuccessStatusCode)
                {
                    await Console.Error.WriteLineAsync($"Failed to delete message {_lastMessageId}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - we still want to send the new message
                await Console.Error.WriteLineAsync($"Error deleting last message: {ex.Message}");
            }
            finally
            {
                _lastMessageId = null;
            }
        }

        private async Task SendMessageAsync(string messageText, CancellationToken cancellationToken)
        {
            try
            {
                // Try to delete the last message first
                await DeleteLastMessageAsync(cancellationToken);

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

                // Parse the response to get the message ID
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (responseObj.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("message_id", out var messageId))
                {
                    _lastMessageId = messageId.GetInt64();
                }
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