using ResourceMonitorCli.Models;
using ResourceMonitorCli.Services;

namespace ResourceMonitorCli
{
    internal static class Program
    {
        private static async Task Main(string?[] args)
        {
            var config = ConfigurationService.ParseCommandLineArguments(args);
            if (config == null) return;

            var metricsService = new MetricsService();
            if (await metricsService.Initialize() == false) return;

            using var cancellationTokenSource = new CancellationTokenSource();
            await RunMonitor(config, metricsService, cancellationTokenSource);
        }

        private static async Task RunMonitor(MonitorConfig config, MetricsService metricsService, CancellationTokenSource cancellationTokenSource)
        {
            // If Telegram mode is enabled, start a background task to send messages.
            var telegramTask = Task.CompletedTask;
            if (config.TelegramMode)
            {
                var telegramService = new TelegramService(config.TelegramToken, config.ChatId, metricsService);
                telegramTask = Task.Run(() => telegramService.StartSenderAsync(config.IntervalMinutes, cancellationTokenSource.Token),
                    cancellationTokenSource.Token);
            }

            try
            {
                if (!config.TelegramMode)
                {
                    var consoleService = new ConsoleService(metricsService);
                    await consoleService.RunConsoleMode(cancellationTokenSource.Token);
                }
                else
                {
                    // In Telegram mode, simply wait (the background task sends messages at the set interval).
                    await telegramTask;
                }
            }
            catch (TaskCanceledException)
            {
                // Graceful shutdown.
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Unhandled error in main loop: {ex.Message}");
            }
            finally
            {
                await cancellationTokenSource.CancelAsync();
                try
                {
                    await telegramTask;
                }
                catch (TaskCanceledException) { }
            }
        }
    }
}
