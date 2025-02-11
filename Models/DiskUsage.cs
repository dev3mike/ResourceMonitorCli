namespace ResourceMonitorCli.Models;

public class DiskUsage
{
    public string Name { get; set; }
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public float UsagePercentage { get; set; }
}