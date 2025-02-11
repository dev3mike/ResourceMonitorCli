namespace ResourceMonitorCli.Models;

public class SystemMetrics
{
    public float CpuUsage { get; set; }
    public float MemoryUsage { get; set; }
    public List<DiskUsage> DiskUsages { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}