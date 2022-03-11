using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WinstonBot.Services
{
    internal struct SuspensionInfo
    {
        public ulong UserId;
        public DateTime Expiry;
        public string Reason;
        public int TimesSuspended;
        public int TimesWarned;
    }

    internal class EventControlDB
    {
        internal enum Version
        {
            Initial,

            Count,
            Current = Count - 1
        }

        internal class UserEntry
        {
            public DateTime SuspensionExpiry { get; set; } = DateTime.MinValue;
            public int TimesSuspended { get; set; } = 0;
            public int TimesWarned { get; set; } = 0;
            public string LastSuspensionReason { get; set; }
        }

        internal class GuildStorage
        {
            public Dictionary<ulong, UserEntry> Users { get; set; } = new();
        }

        internal class Database
        {
            Version Version { get; set; } = Version.Current;
            public Dictionary<ulong, GuildStorage> Guilds { get; set; } = new();
        }

        private ILogger<EventControlDB> _logger;
        private Database _database = new();
        private string _path;

        public EventControlDB(ILogger<EventControlDB> logger, IConfiguration configuration)
        {
            _logger = logger;
            _path = configuration["event_suspension_db_path"];
        }

        public async Task Initialize()
        {
            Database? tempDB = null;
            try
            {
                if (_path != null && File.Exists(_path))
                {
                    var data = await File.ReadAllTextAsync(_path);
                    tempDB = JsonConvert.DeserializeObject<Database>(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read WatchCat database from {_path}: {ex.Message}");
            }

            if (tempDB != null)
            {
                lock (_database)
                {
                    _database = tempDB;
                }

                _logger.LogInformation("Loaded WatchCat DB");
            }
            else
            {
                _logger.LogInformation("No WatchCat DB found");
            }
        }

        public Database GetDatabase() => _database;

        public SuspensionInfo? GetUserEntry(SocketGuildUser user)
        {
            lock (_database)
            {
                if (_database.Guilds.ContainsKey(user.Guild.Id))
                {
                    var guildStorage = _database.Guilds[user.Guild.Id];
                    if (guildStorage.Users.ContainsKey(user.Id))
                    {
                        var entry = guildStorage.Users[user.Id];
                        return new SuspensionInfo
                        {
                            UserId = user.Id,
                            Expiry = entry.SuspensionExpiry,
                            Reason = entry.LastSuspensionReason,
                            TimesSuspended = entry.TimesSuspended,
                            TimesWarned = entry.TimesWarned,
                        };
                    }
                }
            }
            return null;
        }

        public void AddUserEntry(SocketGuildUser user, DateTime expiry, string reason)
        {
            lock (_database)
            {
                var storage = Utility.GetOrAdd(_database.Guilds, user.Guild.Id);
                var entry = Utility.GetOrAdd(storage.Users, user.Id);
                entry.SuspensionExpiry = expiry;
                entry.TimesSuspended++;
                entry.LastSuspensionReason = reason;
                Save();
            }
        }

        public void ResetSuspensionCountForUser(SocketGuildUser user)
        {
            VisitUserEntryIfExists(user, (UserEntry entry) =>
            {
                entry.TimesSuspended = 0;
                return true;
            });
        }

        public void ResetWarningCountForUser(SocketGuildUser user)
        {
            VisitUserEntryIfExists(user, (UserEntry entry) =>
            {
                entry.TimesWarned = 0;
                return true;
            });
        }

        public void ClearExpirationForUser(SocketGuildUser user)
        {
            VisitUserEntryIfExists(user, (UserEntry entry) =>
            {
                entry.SuspensionExpiry = DateTime.MinValue;
                return true;
            });
        }

        public int IncrementWarningCountForUser(SocketGuildUser user)
        {
            lock (_database)
            {
                var storage = Utility.GetOrAdd(_database.Guilds, user.Guild.Id);
                var entry = Utility.GetOrAdd(storage.Users, user.Id);
                ++entry.TimesWarned;
                Save();
                return entry.TimesWarned;
            }
        }

        private void VisitUserEntryIfExists(SocketGuildUser user, Func<UserEntry, bool> visitor)
        {
            lock (_database)
            {
                if (_database.Guilds.ContainsKey(user.Guild.Id))
                {
                    var guildStorage = _database.Guilds[user.Guild.Id];
                    if (guildStorage.Users.ContainsKey(user.Id))
                    {
                        if (visitor.Invoke(guildStorage.Users[user.Id]))
                        {
                            Save();
                        }
                    }
                }
            }
        }

        private void Save()
        {
            if (_path != null)
            {
                try
                {
                    string value = JsonConvert.SerializeObject(_database, Formatting.Indented);
                    File.WriteAllText(_path, value);
                    _logger.LogInformation("EventSuspensionDB Updated");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to write to db path {_path}: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning("Save failed: No path specified");
            }
        }
    }
}
