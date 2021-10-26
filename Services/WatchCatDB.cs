using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace WinstonBot.Services
{
    internal class WatchCatDB
    {
        internal enum UserAction
        {
            Notify,
            Kick,
            Ban
        }

        internal class UserEntry
        {
            public string? Username {  get; set; }
            public string? Discriminator {  get; set; }
            public ulong? Id { get; set; }
            public UserAction Action { get; set; }
        }

        internal class GuildEntry
        {
            public List<UserEntry> Entries { get; set; } = new();
            public ulong NotifyChannelId { get; set; } = 0;
            public ulong NotifyRoleId { get; set; } = 0;
        }

        private class Database
        {
            public Dictionary<ulong, GuildEntry> GuildEntries { get; set; } = new();
        }

        ILogger<WatchCatDB> _logger;
        private string _path;
        private Database _database = new();

        public GuildEntry GetEntry(ulong guildId)
        {
            lock (_database)
            {
                return Utility.GetOrAdd(_database.GuildEntries, guildId);
            }
        }

        public WatchCatDB(ILogger<WatchCatDB> logger, IConfiguration configuration)
        {
            _logger = logger;
            _path = configuration["watchcat_path"];
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

        public void SetNotifyChannel(ulong guildId, ulong channelId)
        {
            lock (_database)
            {
                var entry = Utility.GetOrAdd(_database.GuildEntries, guildId);
                entry.NotifyChannelId = channelId;
                Save();
            }
        }

        public void SetNotifyRole(ulong guildId, ulong roleId)
        {
            lock (_database)
            {
                var entry = Utility.GetOrAdd(_database.GuildEntries, guildId);
                entry.NotifyRoleId = roleId;
                Save();
            }
        }

        public void AddUser(ulong guildId, string username, string discriminator, UserAction action)
        {
            var entry = new UserEntry()
            {
                Username = username,
                Discriminator = discriminator,
                Id = null,
                Action = action
            };

            _logger.LogInformation($"Adding user {username}#{discriminator} to watch in guild {guildId}");

            lock (_database)
            {
                Utility.GetOrAdd(_database.GuildEntries, guildId).Entries.Add(entry);
                Save();
            }
        }

        public void AddUser(ulong guildId, ulong id, UserAction action)
        {
            var entry = new UserEntry()
            {
                Username = null,
                Discriminator = null,
                Id = id,
                Action = action
            };

            _logger.LogInformation($"Adding user {id} to watch in guild {guildId}");

            lock (_database)
            {
                Utility.GetOrAdd(_database.GuildEntries, guildId).Entries.Add(entry);
                Save();
            }
        }

        public bool RemoveUser(ulong guildId, string username, string discriminator)
        {
            lock (_database)
            {
                GuildEntry entry;
                if (!_database.GuildEntries.TryGetValue(guildId, out entry))
                {
                    return false;
                }

                var userEntry = entry.Entries.Find(entry => entry.Username == username && entry.Discriminator == discriminator);
                if (userEntry != null)
                {
                    _logger.LogInformation($"Removing user {username}#{discriminator} from watch in guild {guildId}");
                    entry.Entries.Remove(userEntry);
                    Save();
                    return true;
                }
            }
            return false;
        }

        public bool RemoveUser(ulong guildId, ulong userId)
        {
            lock (_database)
            {
                GuildEntry entry;
                if (!_database.GuildEntries.TryGetValue(guildId, out entry))
                {
                    return false;
                }

                var userEntry = entry.Entries.Find(entry => entry.Id == userId);
                if (userEntry != null)
                {
                    _logger.LogInformation($"Removing user {userId} from watch in guild {guildId}");
                    entry.Entries.Remove(userEntry);
                    Save();
                    return true;
                }
            }
            return false;
        }

        private void Save()
        {
            if (_path != null)
            {
                try
                {
                    string value = JsonConvert.SerializeObject(_database, Formatting.Indented);
                    File.WriteAllText(_path, value);
                    _logger.LogInformation("WatchCatDB Updated");
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
