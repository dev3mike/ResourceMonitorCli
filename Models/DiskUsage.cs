namespace ResourceMonitorCli.Models;

public class DiskUsage
{
    public required string Name { get; set; }
    public float UsagePercentage { get; set; }
    public ulong FreeSpace { get; set; }
    public ulong TotalSize { get; set; }
}