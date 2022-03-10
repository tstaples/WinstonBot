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
        private Timer _pollTimer;

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

            foreach (var guild in Client.Guilds)
            {
                await guild.DownloaderPromise.WaitAsync(stoppingToken);
            }

#if DEBUG
            _pollTimer = new Timer(CheckTimers, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
#else
            _pollTimer = new Timer(CheckTimers, null, TimeSpan.Zero, TimeSpan.FromMinutes(15));
#endif
        }

        private void CheckTimers(object? state)
        {
            Logger.LogInformation("Checking for expired timers");

            Task.Run(async () =>
            {
                foreach (var guild in Client.Guilds)
                {
                    var users = _eventControl.GetSuspendedUsers(guild.Id);
                    foreach (SuspensionInfo userInfo in users)
                    {
                        var user = guild.GetUser(userInfo.UserId);

                        if (!_eventControl.IsUserSuspended(user))
                        {
                            continue;
                        }

                        // DateTime.MinValue is our sentinel to indicate they're not currently suspended
                        if (userInfo.Expiry < DateTime.UtcNow &&
                            userInfo.Expiry > DateTime.MinValue)
                        {
                            await _eventControl.RemoveSuspensionFromUser(user);
                        }                     
                    }
                }
            });
        }
    }
}
