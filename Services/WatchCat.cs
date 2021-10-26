using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class WatchCat : DiscordClientService
    {
        private WatchCatDB _database;

        public WatchCat(DiscordSocketClient client, ILogger<DiscordClientService> logger, WatchCatDB database) 
            : base(client, logger)
        {
            _database = database;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _database.Initialize();

            await Client.WaitForReadyAsync(stoppingToken);

            Client.UserJoined += Client_UserJoined;
        }

        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            var entry = _database.GetEntry(arg.Guild.Id);
            foreach (var userEntry in entry.Entries)
            {
                if (CompareUser(arg, userEntry))
                {
                    Logger.LogInformation($"WatchCat has detected a watched user join: {arg.Mention} - {arg.Username}#{arg.Discriminator}");
                    await HandleUserAction(arg, userEntry.Action, entry.NotifyChannelId, entry.NotifyRoleId);
                    break;
                }
            }
        }

        private bool CompareUser(SocketGuildUser user, WatchCatDB.UserEntry entry)
        {
            if (entry.Id != null)
            {
                return user.Id == entry.Id;
            }

            return entry.Username == user.Username && entry.Discriminator == user.Discriminator;
        }

        private async Task HandleUserAction(SocketGuildUser user, WatchCatDB.UserAction action, ulong channelId, ulong pingRoleId)
        {
            switch (action)
            {
                case WatchCatDB.UserAction.Notify:
                    {
                        var channel = user.Guild.GetTextChannel(channelId);
                        if (channel != null)
                        {
                            var role = user.Guild.GetRole(pingRoleId);
                            await channel.SendMessageAsync($"{(role != null ? role.Mention : string.Empty)} WatchCat has detected user {user.Mention} joined.");
                        }
                        else
                        {
                            Logger.LogWarning($"Failed to send notify to channel as the channel id was not set.");
                        }
                    }
                    break;
            }
        }
    }
}
