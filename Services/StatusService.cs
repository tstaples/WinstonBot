using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace WinstonBot.Services
{
    internal class StatusService : DiscordClientService
    {
        public StatusService(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Client.WaitForReadyAsync(stoppingToken);

            await Client.SetGameAsync($"Version {Assembly.GetEntryAssembly().GetName().Version}");
        }
    }
}
