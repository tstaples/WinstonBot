using System.Collections.Concurrent;
using WinstonBot.MessageHandlers;
using Newtonsoft.Json;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace WinstonBot.Services
{
    // This class tracks messages corresponding to commands that the bot responds to.
    // This can be serialized to allow the bot to respond to old commands even if it's restarted.
    public class MessageDatabase
    {
        public enum Version
        {
            None,
            Initial,
            Count,
            Current = Count - 1
        }

        public static Version CurrentVersion => Version.Current;

        private class GuildEntry
        {
            // message id -> handler
            public Dictionary<ulong, IMessageHandler> MessageHandlers { get; set; } = new Dictionary<ulong, IMessageHandler>();
        }

        private class DBVersion
        {
            public Version VersionNumber {  get; set; }
        }

        private class Database : DBVersion
        {
            // guild id -> entry
            public Dictionary<ulong, GuildEntry> GuildEntries { get; set; } = new Dictionary<ulong, GuildEntry>();
        }

        private string _databasePath;
        private Database _database;

        public MessageDatabase(string path)
        {
            _databasePath = path;
        }

        public void AddMessage(ulong guildId, ulong messageId, IMessageHandler handler)
        {
            // if we hit these asserts then queue the messages until the db is loaded then add them.
            Debug.Assert(_database != null);
            lock (_database)
            {
                GetOrAddGuild(guildId).MessageHandlers.Add(messageId, handler);
                Save();
            }
        }

        public bool HasMessage(ulong guildId, ulong messageId)
        {
            Debug.Assert(_database != null);
            lock (_database)
            {
                return _database.GuildEntries.ContainsKey(guildId) &&
                    _database.GuildEntries[guildId].MessageHandlers.ContainsKey(messageId);
            }
        }

        public IMessageHandler GetMessageHandler(ulong guildId, ulong messageId)
        {
            Debug.Assert(_database != null);
            lock (_database)
            {
                return _database.GuildEntries[guildId].MessageHandlers[messageId];
            }
        }

        public void RemoveMessage(ulong guildId, ulong messageId)
        {
            Debug.Assert(_database != null);
            lock (_database)
            {
                GetOrAddGuild(guildId).MessageHandlers.Remove(messageId);
                Save();
            }
        }

        private GuildEntry GetOrAddGuild(ulong guildId)
        {
            if (!_database.GuildEntries.ContainsKey(guildId))
            {
                _database.GuildEntries.Add(guildId, new GuildEntry());
            }
            return _database.GuildEntries[guildId];
        }

        public void Load(IServiceProvider services)
        {
            if (File.Exists(_databasePath))
            {
                try
                {
                    var data = File.ReadAllText(_databasePath);

                    DBVersion version = JsonConvert.DeserializeObject<DBVersion>(data);
                    if (version != null && version.VersionNumber == CurrentVersion)
                    {
                        _database = JsonConvert.DeserializeObject<Database>(data, new JsonSerializerSettings()
                        {
                            TypeNameHandling = TypeNameHandling.Auto
                        });
                    }
                    else
                    {
                        Console.WriteLine("Database is out of date. Creating a fresh one");
                        _database = new Database()
                        {
                            VersionNumber = CurrentVersion
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to database file: {ex.Message}. Creating new database.");
                    _database = new Database()
                    {
                        VersionNumber = CurrentVersion
                    };
                }
            }
            else
            {
                _database = new Database()
                {
                    VersionNumber = CurrentVersion
                };
            }

            // initialize handler data
            foreach (var guildEntryPair in _database.GuildEntries)
            {
                ulong guildId = guildEntryPair.Key;
                foreach (var messageIdHandlerPair in guildEntryPair.Value.MessageHandlers)
                {
                    var handler = messageIdHandlerPair.Value;
                    handler.ConstructContext(services);
                }
            }

            Console.WriteLine("Message Database initialized");
        }

        private void Save()
        {
            string jsonData = JsonConvert.SerializeObject(_database, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_databasePath));
                File.WriteAllText(_databasePath, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing database: " + ex.Message);
            }
        }
    }
}
