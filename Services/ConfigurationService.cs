using ResourceMonitorCli.Models;

namespace ResourceMonitorCli.Services
{
    public static class ConfigurationService
    {
        public static MonitorConfig? ParseCommandLineArguments(string?[] args)
        {
            var config = new MonitorConfig();

            // Parse arguments.
            for (var i = 0; i < args.Length; i++)
            {
                try
                {
                    if (args[i] == "--telegram" || args[i] == "-t")
                    {
                        if (i + 1 < args.Length)
                        {
                            config.TelegramToken = args[i + 1];
                            i++;
                        }
                        else
                        {
                            Console.Error.WriteLineAsync("Missing Telegram bot token.").Wait();
                            return null;
                        }
                    }
                    else if (args[i] == "--chat" || args[i] == "-c")
                    {
                        if (i + 1 < args.Length)
                        {
                            config.ChatId = args[i + 1];
                            i++;
                        }
                        else
                        {
                            Console.Error.WriteLineAsync("Missing Telegram chat ID.").Wait();
                            return null;
                        }
                    }
                    else if (args[i] == "--interval" || args[i] == "-i")
                    {
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int interval))
                        {
                            config.IntervalMinutes = interval;
                            i++;
                        }
                        else
                        {
                            Console.Error.WriteLineAsync("Invalid interval value. It must be an integer representing minutes.").Wait();
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync($"Error parsing argument '{args[i]}': {ex.Message}").Wait();
                    return null;
                }
            }

            if (config.TelegramMode && string.IsNullOrWhiteSpace(config.ChatId))
            {
                Console.Error.WriteLineAsync("Telegram mode requires a chat ID (--chat).").Wait();
                return null;
            }

            return config;
        }
    }
}