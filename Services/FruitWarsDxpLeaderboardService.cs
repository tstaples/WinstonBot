using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Discord.Addons.Hosting.Util;
using Discord;

namespace WinstonBot.Services
{
    internal class FruitWarsDxpLeaderboardService : DiscordClientService
    {
        private System.Timers.Timer _timer;
        private bool _oneHrIntervalSet = false;
        private ISocketMessageChannel _channel;

        public FruitWarsDxpLeaderboardService(DiscordSocketClient client, ILogger<FruitWarsDxpLeaderboardService> logger) : base(client, logger)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Client.WaitForReadyAsync(stoppingToken);

            var guild = Client.GetGuild(769476224363397140); // Vought
#if DEBUG
            _channel = guild.GetTextChannel(937252551928197141); // bot dev channel
#else
            _channel = guild.GetTextChannel(945079989466980373); // leaderboard channel
#endif

            

            _timer = new System.Timers.Timer();
            _timer.Interval = GetInitialInterval();
            _timer.Elapsed += TimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();

            Logger.LogInformation($"Posting next update in {TimeSpan.FromMilliseconds(_timer.Interval)}");
        }

        private void TimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Task.Run(async () =>
            {
                Logger.LogInformation("Auto-posting dxp results");

                var timestamp = TimestampTag.FromDateTime(DateTime.Now);
                timestamp.Style = TimestampTagStyles.ShortDateTime;
                await _channel.SendMessageAsync($"Results as of {timestamp}");

                await Commands.FruitWarsCommands.PostResults(_channel, Logger);
            }).Forget();

            if (!_oneHrIntervalSet)
            {
                _timer.Interval = 60 * 60 * 1000;
                _oneHrIntervalSet = true;
            }

            Logger.LogInformation($"Posting next update in {TimeSpan.FromMilliseconds(_timer.Interval)}");
        }

        private double GetInitialInterval()
        {
            var now = DateTime.UtcNow;
            var nextHour = now
                .AddHours(1)
                .AddMinutes(-now.Minute)
                .AddSeconds(-now.Second)
                .AddMilliseconds(-now.Millisecond);

            TimeSpan offset = nextHour - now;
            return offset.TotalMilliseconds;
        }
    }
}
