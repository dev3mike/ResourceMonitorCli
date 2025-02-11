using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using ResourceMonitorCli.Models;

namespace ResourceMonitorCli.Services
{
    public class MetricsService
    {
        // Windows-specific CPU counter.
        private static PerformanceCounter? _cpuCounter;

        // Structure used for Unix-based CPU time calculations.
        private struct CpuTimes
        {
            public ulong IdleTime;
            public ulong TotalTime;
        }
        private static CpuTimes? _prevCpuTimes = null;

        public async Task<bool> Initialize()
        {
            // If running on Windows, initialize the PerformanceCounter.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    // Prime the counter (first reading is typically 0).
                    _cpuCounter.NextValue();
                    await Task.Delay(1000);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    await Console.Error.WriteLineAsync("Error: Administrator privileges are required to monitor CPU usage on Windows.");
                    await Console.Error.WriteLineAsync("Please run the application as Administrator.");
                    return false;
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Error initializing PerformanceCounter: {ex.Message}");
                    if (ex.Message.Contains("category does not exist"))
                    {
                        await Console.Error.WriteLineAsync("This error might occur if the Performance Counter DLLs are not registered.");
                        await Console.Error.WriteLineAsync("Try running 'lodctr /r' as Administrator to rebuild the Performance Counter Registry.");
                    }
                    return false;
                }
            }

            // For non-Windows platforms, verify we can read the necessary files/commands
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!File.Exists("/proc/stat"))
                {
                    await Console.Error.WriteLineAsync("Error: Cannot access /proc/stat for CPU monitoring.");
                    return false;
                }
                if (!File.Exists("/proc/meminfo"))
                {
                    await Console.Error.WriteLineAsync("Error: Cannot access /proc/meminfo for memory monitoring.");
                    return false;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "top",
                        Arguments = "-l 1 -n 0",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit();
                    if (proc?.ExitCode != 0)
                    {
                        await Console.Error.WriteLineAsync("Error: Cannot execute 'top' command for CPU monitoring.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Error testing macOS monitoring capabilities: {ex.Message}");
                    return false;
                }
            }
            return true;
        }

        public SystemMetrics GetSystemMetrics()
        {
            var metrics = new SystemMetrics();

            try
            {
                metrics.CpuUsage = GetCpuUsage();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving CPU usage: {ex.Message}");
                metrics.CpuUsage = 0;
            }

            try
            {
                metrics.MemoryUsage = GetMemoryUsage();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving Memory usage: {ex.Message}");
                metrics.MemoryUsage = 0;
            }

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        var total = (ulong)drive.TotalSize;
                        var free = (ulong)drive.TotalFreeSpace;
                        var usedPercent = total > 0 ? (float)((double)(total - free) / total * 100) : 0;
                        metrics.DiskUsages.Add(new DiskUsage
                        {
                            Name = drive.Name,
                            TotalSize = total,
                            FreeSpace = free,
                            UsagePercentage = usedPercent
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving disk usage: {ex.Message}");
            }

            return metrics;
        }

        private float GetCpuUsage()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return _cpuCounter!.NextValue();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading CPU usage on Windows: {ex.Message}");
                    return 0;
                }
            }
            else
            {
                var current = ReadCpuTimes();
                if (!_prevCpuTimes.HasValue)
                {
                    _prevCpuTimes = current;
                    return 0.0f;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // For macOS, directly use user + system percentage
                    return current.TotalTime;
                }
                else
                {
                    // For Linux, calculate the percentage from the difference
                    var idleDiff = current.IdleTime - _prevCpuTimes.Value.IdleTime;
                    var totalDiff = current.TotalTime - _prevCpuTimes.Value.TotalTime;
                    _prevCpuTimes = current;
                    return totalDiff > 0 ? (float)(100.0 * (totalDiff - idleDiff) / totalDiff) : 0.0f;
                }
            }
        }

        private CpuTimes ReadCpuTimes()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    var lines = File.ReadAllLines("/proc/stat");
                    var cpuLine = lines.FirstOrDefault(line => line.StartsWith("cpu "));
                    if (cpuLine == null)
                        return new CpuTimes();
                    var parts = cpuLine.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    // parts[0] is "cpu"; subsequent parts are times.

                    // Time spent running normal user processes
                    var user = ulong.Parse(parts[1]);

                    // Time spent running niced (low priority) user processes
                    var nice = ulong.Parse(parts[2]);

                    // Time spent running kernel/system processes
                    var system = ulong.Parse(parts[3]);

                    // Time spent doing nothing (CPU was idle)
                    var idle = ulong.Parse(parts[4]);

                    // Time spent waiting for disk or network operations to complete
                    var iowait = parts.Length > 5 ? ulong.Parse(parts[5]) : 0;

                    // Time spent handling hardware interrupts
                    var irq = parts.Length > 6 ? ulong.Parse(parts[6]) : 0;

                    // Time spent handling software interrupts
                    var softirq = parts.Length > 7 ? ulong.Parse(parts[7]) : 0;

                    // Time stolen by other virtual machines on the same host
                    var steal = parts.Length > 8 ? ulong.Parse(parts[8]) : 0;

                    // Add up all types of CPU time to get total CPU time
                    var total = user + nice + system + idle + iowait + irq + softirq + steal;

                    // Total time the CPU spent doing nothing (idle + waiting for IO)
                    var idleTime = idle + iowait;
                    return new CpuTimes { IdleTime = idleTime, TotalTime = total };
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading /proc/stat: {ex.Message}");
                    return new CpuTimes();
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "top",
                        Arguments = "-l 1 -n 0",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    var output = proc?.StandardOutput.ReadToEnd();
                    proc?.WaitForExit();

                    // Parse the CPU usage line
                    var cpuLine = output?.Split('\n')
                        .FirstOrDefault(line => line.Contains("CPU usage:"));

                    if (string.IsNullOrEmpty(cpuLine))
                        return new CpuTimes();

                    // Extract user and system percentages
                    var userMatch = System.Text.RegularExpressions.Regex.Match(cpuLine, @"(\d+\.?\d*)% user");
                    var systemMatch = System.Text.RegularExpressions.Regex.Match(cpuLine, @"(\d+\.?\d*)% sys");
                    var idleMatch = System.Text.RegularExpressions.Regex.Match(cpuLine, @"(\d+\.?\d*)% idle");

                    if (!userMatch.Success || !systemMatch.Success || !idleMatch.Success)
                        return new CpuTimes();

                    float user = float.Parse(userMatch.Groups[1].Value);
                    float system = float.Parse(systemMatch.Groups[1].Value);

                    // Store the actual CPU usage (user + system) directly
                    return new CpuTimes { IdleTime = 0, TotalTime = (ulong)(user + system) };
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading macOS CPU usage: {ex.Message}");
                    return new CpuTimes();
                }
            }

            return new CpuTimes();
        }

        private float GetMemoryUsage()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var query = new ObjectQuery("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
                    using var searcher = new ManagementObjectSearcher(query);
                    foreach (var result in searcher.Get())
                    {
                        // Values are in kilobytes.
                        var freeMemoryKb = (ulong)result["FreePhysicalMemory"];
                        var totalMemoryKb = (ulong)result["TotalVisibleMemorySize"];
                        return (float)(((double)(totalMemoryKb - freeMemoryKb) / totalMemoryKb) * 100);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error retrieving Windows memory info: {ex.Message}");
                    return 0;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetMemoryUsageLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMemoryUsageMac();
            }
            return 0;
        }

        private float GetMemoryUsageLinux()
        {
            try
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                var memTotal = 0UL;
                var memAvailable = 0UL;
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                            memTotal = ulong.Parse(parts[1]); // in kB
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                            memAvailable = ulong.Parse(parts[1]); // in kB
                    }
                }
                if (memTotal == 0)
                    return 0;
                var memUsed = memTotal - memAvailable;
                return (float)memUsed / memTotal * 100;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading /proc/meminfo: {ex.Message}");
                return 0;
            }
        }

        private float GetMemoryUsageMac()
        {
            try
            {
                // Retrieve total memory (in bytes) via sysctl.
                var psiTotal = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.memsize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var procTotal = Process.Start(psiTotal);
                var totalOutput = procTotal?.StandardOutput.ReadToEnd();
                procTotal?.WaitForExit();
                var totalMemory = ulong.Parse(totalOutput?.Trim() ?? string.Empty);

                // Retrieve free memory via vm_stat.
                var psiVm = new ProcessStartInfo
                {
                    FileName = "vm_stat",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var procVm = Process.Start(psiVm);
                var vmOutput = procVm?.StandardOutput.ReadToEnd();
                procVm?.WaitForExit();

                // Determine the page size (defaulting to 4096 bytes if parsing fails).
                var pageSize = 4096UL;
                var lines = vmOutput!.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("page size of"))
                    {
                        int start = line.IndexOf("page size of", StringComparison.Ordinal) + "page size of".Length;
                        int end = line.IndexOf("bytes", start, StringComparison.Ordinal);
                        if (end > start)
                        {
                            var sizeStr = line.Substring(start, end - start).Trim();
                            if (ulong.TryParse(sizeStr, out ulong ps))
                                pageSize = ps;
                        }
                        break;
                    }
                }

                ulong freePages = 0;
                ulong speculativePages = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("Pages free:"))
                    {
                        var parts = line.Split([':'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string numStr = parts[1].Trim().TrimEnd('.');
                            if (ulong.TryParse(numStr, out ulong free))
                                freePages = free;
                        }
                    }
                    else if (line.StartsWith("Pages speculative:"))
                    {
                        var parts = line.Split([':'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string numStr = parts[1].Trim().TrimEnd('.');
                            if (ulong.TryParse(numStr, out ulong spec))
                                speculativePages = spec;
                        }
                    }
                }
                var freeMemoryBytes = (freePages + speculativePages) * pageSize;
                if (totalMemory == 0)
                    return 0;
                var usedMemory = totalMemory - freeMemoryBytes;
                return (float)usedMemory / totalMemory * 100;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving macOS memory info: {ex.Message}");
                return 0;
            }
        }
    }
}