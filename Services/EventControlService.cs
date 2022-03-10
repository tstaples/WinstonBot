using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class EventControlService : DiscordClientService
    {
        private struct TimerData
        {
            public ulong GuildId;
            public ulong UserId;
            public string Reason;
        }

        private struct TimerEntry
        {
            public ulong GuildId;
            public ulong UserId;
            public Timer Timer;
        }

        private EventControl _eventControl;
        private EventControlDB _database;
        private List<TimerEntry> _timers = new();

        public EventControlService(DiscordSocketClient client, ILogger<EventControlService> logger, EventControl eventControl, EventControlDB database)
            : base(client, logger)
        {
            _eventControl = eventControl;
            _database = database;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _database.Initialize();

            _eventControl.UserSuspended += OnUserSuspended;
            _eventControl.UserUnsuspended += OnUserUnsuspended;

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
                        AddSuspensionTimer(user, userInfo.Reason, userInfo.Expiry);
                    }
                    else
                    {
                        await _eventControl.RemoveSuspensionFromUser(user);
                    }
                }
            }
        }

        private void AddSuspensionTimer(SocketGuildUser user, string reason, DateTime expiry)
        {
            if (expiry < DateTime.UtcNow)
            {
                Logger.LogWarning($"Suspension expiry for {user.Nickname}-{user.Id} is in the past. Ignoring");
                return;
            }

            TimerData data = new TimerData()
            {
                GuildId = user.Guild.Id,
                UserId = user.Id,
                Reason = reason
            };

            Logger.LogInformation($"{user.Nickname}-{user.Id} currently suspended until {expiry}, starting timer");

            TimeSpan interval = expiry - DateTime.UtcNow;

            _timers.Add(new TimerEntry()
            {
                GuildId = user.Guild.Id,
                UserId = user.Id,
                Timer = new Timer(
                    Timer_Elapsed,
                    data,
                    interval,
                    Timeout.InfiniteTimeSpan)
            });
        }

        private void RemoveSuspensionTimer(ulong guildId, ulong userId)
        {
            int index = _timers.FindIndex((TimerEntry entry) => { return entry.GuildId == guildId && entry.UserId == userId; });
            if (index != -1)
            {
                _timers[index].Timer.Dispose();
                _timers.RemoveAt(index);
            }
        }

        private void Timer_Elapsed(object? state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var data = (TimerData)state;

            Task.Run(async () =>
            {
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

            RemoveSuspensionTimer(data.GuildId, data.UserId);
        }

        private void OnUserSuspended(SocketGuildUser user, string reason, DateTime expiry)
        {
            AddSuspensionTimer(user, reason, expiry);
        }

        private void OnUserUnsuspended(SocketGuildUser user)
        {
            RemoveSuspensionTimer(user.Guild.Id, user.Id);
        }
    }
}
