using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class EventControlService : DiscordClientService
    {
        private EventControl _eventControl;
        private EventControlDB _database;
        private List<Timer> _timers = new();

        private struct TimerData
        {
            public ulong GuildId;
            public ulong UserId;
            public string Reason;
        }

        public EventControlService(DiscordSocketClient client, ILogger<EventControlService> logger, EventControl eventControl, EventControlDB database)
            : base(client, logger)
        {
            _eventControl = eventControl;
            _database = database;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _database.Initialize();

            await Client.WaitForReadyAsync(stoppingToken);

            await InitializeTimers();
        }

        private async Task InitializeTimers()
        {
            foreach (var guild in Client.Guilds)
            {
                // TODO: find nicer way to wait for users to be downloaded
                await guild.DownloadUsersAsync();

                var users = _eventControl.GetSuspendedUsers(guild.Id);
                foreach (SuspensionInfo userInfo in users)
                {
                    var user = guild.GetUser(userInfo.UserId);

                    if (!_eventControl.IsUserSuspended(user))
                    {
                        continue;
                    }

                    if (userInfo.Expiry > DateTime.UtcNow)
                    {
                        TimerData data = new TimerData()
                        {
                            GuildId = guild.Id,
                            UserId = userInfo.UserId,
                            Reason = userInfo.Reason
                        };

                        Logger.LogInformation($"{user.Nickname}-{user.Id} currently suspended until {userInfo.Expiry}, starting timer");

                        _timers.Add(new Timer(
                            Timer_Elapsed,
                            data,
                            userInfo.Expiry.TimeOfDay,
                            Timeout.InfiniteTimeSpan));
                    }
                    else
                    {
                        await _eventControl.RemoveSuspensionFromUser(user);
                    }
                }
            }
        }

        private void Timer_Elapsed(object? state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            Task.Run(async () =>
            {
                var data = (TimerData)state;
                Logger.LogInformation($"Expiration timer for {data.UserId} expired.");

                var guild = Client.GetGuild(data.GuildId);
                if (guild == null)
                {
                    Logger.LogError($"Failed to find guild {data.GuildId}.");
                    return;
                }

                var user = guild.GetUser(data.UserId);
                if (user == null)
                {
                    Logger.LogError($"User {data.UserId} is invalid.");
                    return;
                }

                if (!_eventControl.IsUserSuspended(user))
                {
                    Logger.LogInformation($"User {data.UserId} is no longer suspended, canceling DM.");
                    return;
                }
                await _eventControl.RemoveSuspensionFromUser(user);
            });
        }
    }
}
