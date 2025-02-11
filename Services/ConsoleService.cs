using ResourceMonitorCli.Models;
using ResourceMonitorCli.Utils;

namespace ResourceMonitorCli.Services
{
    public class ConsoleService(MetricsService metricsService)
    {
        public async Task RunConsoleMode(CancellationToken cancellationToken)
        {
            // Console mode: update the terminal every second with the latest metrics.
            while (!cancellationToken.IsCancellationRequested)
            {
                var metrics = metricsService.GetSystemMetrics();
                Console.Clear();
                Console.WriteLine("=== Resource Monitor ===");
                Console.WriteLine($"Timestamp: {metrics.Timestamp:O}");
                Console.WriteLine($"CPU Usage: {metrics.CpuUsage:0.00}%");
                Console.WriteLine($"Memory Usage: {metrics.MemoryUsage:0.00}%");
                Console.WriteLine("Disk Usage:");
                foreach (var disk in metrics.DiskUsages)
                {
                    Console.WriteLine($"  {disk.Name} - Used: {disk.UsagePercentage:0.00}%  Free: {FormatUtils.FormatBytes(disk.FreeSpace)} / Total: {FormatUtils.FormatBytes(disk.TotalSize)}");
                }
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}