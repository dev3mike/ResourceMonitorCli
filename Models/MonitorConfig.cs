namespace ResourceMonitorCli.Models;

public class MonitorConfig
{
    public string? TelegramToken { get; set; }
    public string? ChatId { get; set; }
    public int IntervalMinutes { get; set; } = 60;
    public bool TelegramMode => !string.IsNullOrWhiteSpace(TelegramToken);
}