using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class EventControl
    {
        private const ulong SuspendedRole = 950947102438064128;
        private const int DefaultSuspensionDays = 7;
        private static readonly TimeSpan DefaultDuration = TimeSpan.FromDays(DefaultSuspensionDays);

        private readonly ILogger<EventControl> _logger;
        private EventControlDB _database;

        public event Action<SocketGuildUser, string, DateTime> UserSuspended;
        public event Action<SocketGuildUser> UserUnsuspended;

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

        public async Task<SuspendResult> SuspendUser(SocketGuildUser user, string reason, DateTime? expiryOverride)
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

            UserSuspended(user, reason, expiry);

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

            UserUnsuspended(user);
        }

        public void ResetCount(SocketGuildUser user)
        {
            _logger.LogInformation($"Resetting suspension count for {user.Nickname}");
            _database.ResetCountForUser(user);
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
                        TimesSuspended = userEntry.Value.TimesSuspended
                    });
                }
            }
            return suspensionInfos;
        }
    }
}
