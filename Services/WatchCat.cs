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

        private async Task HandleUserAction(SocketGuildUser user, WatchCatDB.UserAction action, ulong channelId, ulong notifyRoleId)
        {
            switch (action)
            {
                case WatchCatDB.UserAction.Notify:
                    {
                        await SendMessageToNotifyChannel(user.Guild, channelId, notifyRoleId, $"WatchCat has detected user {user.Mention} joined.");
                    }
                    break;

                case WatchCatDB.UserAction.Kick:
                    {
                        await SendMessageToNotifyChannel(user.Guild, channelId, notifyRoleId, $"WatchCat kicking user {user.Username}${user.Discriminator} from guild {user.Guild.Name}");
                        await user.KickAsync("You were automatically kicked by WatchCat.");
                    }
                    break;

                case WatchCatDB.UserAction.Ban:
                    {
                        await SendMessageToNotifyChannel(user.Guild, channelId, notifyRoleId, $"WatchCat banning user {user.Username}${user.Discriminator} from guild {user.Guild.Name}");
                        await user.Guild.AddBanAsync(user, reason: "You were automatically banned by WatchCat.");
                    }
                    break;
            }
        }

        private async Task SendMessageToNotifyChannel(SocketGuild guild, ulong channelId, ulong notifyRoleId, string message)
        {
            var channel = guild.GetTextChannel(channelId);
            if (channel != null)
            {
                var role = guild.GetRole(notifyRoleId);

                string fullMessage = $"{(role != null ? role.Mention : string.Empty)} {message}";
                Logger.LogInformation(fullMessage);
                await channel.SendMessageAsync(fullMessage);
            }
            else
            {
                Logger.LogWarning($"Failed to send notify to channel as the channel id was not set.");
            }
        }
    }
}
