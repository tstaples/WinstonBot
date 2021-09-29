using System.Collections.Concurrent;
using WinstonBot.MessageHandlers;
using Newtonsoft.Json;
using System.Diagnostics;

namespace WinstonBot.Services
{
    // TODO: this should store message ids of host messages in the DB so if the bot restarts the old messages still work.
    // We will need to remove the messages once the message gets deleted.
    public class MessageDatabase
    {
        private class GuildEntry
        {
            // message id -> handler
            public Dictionary<ulong, IMessageHandler> MessageHandlers { get; set; } = new Dictionary<ulong, IMessageHandler>();
        }

        private class Database
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

        public void Load()
        {
            if (File.Exists(_databasePath))
            {
                try
                {
                    var configText = File.ReadAllText(_databasePath);
                    _database = JsonConvert.DeserializeObject<Database>(configText, new JsonSerializerSettings()
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read config file: " + ex.Message);
                }
            }
            else
            {
                _database = new Database();
            }
        }

        private void Save()
        {
            string jsonData = JsonConvert.SerializeObject(_database, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

            File.WriteAllText(_databasePath, jsonData);
        }
    }
}
