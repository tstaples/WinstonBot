using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class EventControl
    {
        public const int MaxWarnings = 3;
        private const ulong SuspendedRole = 950947102438064128;
        private const int DefaultSuspensionDays = 7;
        private static readonly TimeSpan DefaultDuration = TimeSpan.FromDays(DefaultSuspensionDays);

        private readonly ILogger<EventControl> _logger;
        private EventControlDB _database;

        public EventControl(ILogger<EventControl> logger, EventControlDB db)
        {
            _logger = logger;
            _database = db;
        }

        public bool IsUserSuspended(SocketGuildUser user)
        {
            return Utility.DoesUserHaveAnyRequiredRole(user, new ulong[] { SuspendedRole });
        }

        public SuspensionInfo? GetSuspensionInfo(SocketGuildUser user)
        {
            return _database.GetUserEntry(user);
        }

        public enum SuspendResult
        {
            Success,
            AlreadySuspended,
        }

        public async Task<SuspendResult> SuspendUser(SocketGuildUser user, string reason, bool notifyUser, DateTime? expiryOverride)
        {
            var info = _database.GetUserEntry(user);
            if (info != null && info.Value.Expiry > DateTime.UtcNow)
            {
                return SuspendResult.AlreadySuspended;
            }

            DateTime calculatedExpiry = DateTime.UtcNow + (info.HasValue
                ? TimeSpan.FromDays(DefaultSuspensionDays * (info.Value.TimesSuspended + 1))
                : DefaultDuration);
            DateTime expiry = expiryOverride ?? calculatedExpiry;

            _logger.LogInformation($"Suspending {user.Nickname} until {expiry}");

            _database.AddUserEntry(user, expiry, reason);

            await user.AddRoleAsync(SuspendedRole);

            var formattedExpiry = Discord.TimestampTag.FromDateTime(expiry);
            await SendDirectMessage(user, $"You have been suspended from PVM Events until {formattedExpiry} for reason: {reason}");

            return SuspendResult.Success;
        }

        public async Task RemoveSuspensionFromUser(SocketGuildUser user, bool notify = true)
        {
            var info = _database.GetUserEntry(user);
            if (info == null) throw new ArgumentNullException(nameof(info));

            _logger.LogInformation($"Removing suspension from {user.Nickname}");

            _database.ClearExpirationForUser(user);
            await user.RemoveRoleAsync(SuspendedRole);

            if (notify)
            {
                try
                {
                    var channel = await user.CreateDMChannelAsync();
                    await channel.SendMessageAsync($"Your event suspension in {user.Guild.Name} for reason: '{info.Value.Reason}' has expired. You may sign up again.");
                }
                catch (Discord.Net.HttpException ex)
                {
                    _logger.LogError($"Failed to DM user {user.Id} - {user.Username} about suspension removal: {ex.Message}");
                }
            }
        }

        public void ResetSuspensionCount(SocketGuildUser user)
        {
            _logger.LogInformation($"Resetting suspension count for {user.Nickname}");
            _database.ResetSuspensionCountForUser(user);
        }

        public void ResetWarningCount(SocketGuildUser user)
        {
            _logger.LogInformation($"Resetting warning count for {user.Nickname}");
            _database.ResetWarningCountForUser(user);
        }

        public IEnumerable<SuspensionInfo> GetSuspendedUsers(ulong guildId)
        {
            List<SuspensionInfo> suspensionInfos = new List<SuspensionInfo>();
            var guilds = _database.GetDatabase().Guilds;
            if (guilds.ContainsKey(guildId))
            {
                foreach (var userEntry in guilds[guildId].Users)
                {
                    suspensionInfos.Add(new SuspensionInfo()
                    {
                        UserId = userEntry.Key,
                        Expiry = userEntry.Value.SuspensionExpiry,
                        Reason = userEntry.Value.LastSuspensionReason,
                        TimesSuspended = userEntry.Value.TimesSuspended,
                        TimesWarned = userEntry.Value.TimesWarned
                    });
                }
            }
            return suspensionInfos;
        }

        public enum NoShowAction
        {
            Warned,
            Suspended,
            AlreadySuspended
        }

        public struct NoShowResult
        {
            public NoShowAction ActionTaken;
            public int TimesWarned;
            public DateTime? SuspensionExpiry;
        }

        public async Task<NoShowResult> HandlePvmNoShow(SocketGuildUser user, bool notifyUser)
        {
            _logger.LogInformation($"Handling pvm no show for {user.Nickname}");

            SuspensionInfo? info = _database.GetUserEntry(user);
            if (info == null)
            {
                int warningCount = await WarnUser(user, "Not showing up to pvm event without giving prior notice", notifyUser);
                _logger.LogInformation($"{user.Nickname} was warned for the first time");

                return new NoShowResult()
                {
                    ActionTaken = NoShowAction.Warned,
                    TimesWarned = warningCount
                };
            }

            if (info.Value.TimesWarned < MaxWarnings)
            {
                int warningCount = await WarnUser(user, "Not showing up to pvm event without giving prior notice", notifyUser);
                _logger.LogInformation($"{user.Nickname} was warned {warningCount}/{MaxWarnings}");

                return new NoShowResult()
                {
                    ActionTaken = NoShowAction.Warned,
                    TimesWarned = warningCount
                };
            }

            SuspendResult result = await SuspendUser(user, "Exceeded maximum warnings for not showing up to a pvm event without notice", notifyUser, null);
            if (result == SuspendResult.Success)
            {
                // grab updated data
                info = _database.GetUserEntry(user);
                return new NoShowResult()
                {
                    ActionTaken = NoShowAction.Suspended,
                    TimesWarned = info.Value.TimesWarned,
                    SuspensionExpiry = info.Value.Expiry
                };
            }

            _logger.LogInformation($"{user.Nickname} is already suspended");

            return new NoShowResult()
            {
                ActionTaken = NoShowAction.AlreadySuspended,
                TimesWarned = info.Value.TimesWarned,
                SuspensionExpiry = info.Value.Expiry
            };
        }

        private async Task<int> WarnUser(SocketGuildUser user, string reason, bool notify)
        {
            int warningCount = _database.IncrementWarningCountForUser(user);

            _logger.LogInformation($"Warned {user.Nickname} - {warningCount}/{MaxWarnings}");

            if (notify)
            {
                await SendDirectMessage(user, $"You have received a warning from {user.Guild.Name} for {reason}.\n" +
                        $"You've receieved {warningCount}/{MaxWarnings} warnings. " +
                        $"After you've been warned the max number of times you will be suspended for each infraction afterwards.");
            }
            return warningCount;
        }

        private async Task SendDirectMessage(SocketGuildUser user, string message)
        {
            try
            {
                var channel = await user.CreateDMChannelAsync();
                await channel.SendMessageAsync(message);
            }
            catch (Discord.Net.HttpException ex)
            {
                _logger.LogError($"Failed to DM user {user.Id} - {user.Username} message {message}: {ex.Message}");
            }
        }
    }
}
