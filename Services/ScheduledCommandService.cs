using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class ScheduledCommandService : DiscordClientService
    {
        private CommandScheduler _scheduler;

        public ScheduledCommandService(DiscordSocketClient client, ILogger<ScheduledCommandService> logger, CommandScheduler scheduler)
            : base(client, logger)
        {
            _scheduler = scheduler;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Client.WaitForReadyAsync(stoppingToken);
            await _scheduler.StartEvents();
        }
    }
}
