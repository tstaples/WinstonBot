using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WinstonBot.Commands;
using Newtonsoft.Json;

namespace WinstonBot.Services
{
    internal class ScheduledCommandService
    {
        internal class Entry
        {
            public Guid Guid {  get; set; }
            public DateTimeOffset StartDate { get; set; }
            public TimeSpan Frequency { get; set; }
            public ulong ScheduledBy { get; set; }
            public ulong ChannelId { get; set; }
            public string Command { get; set; }
            public List<CommandDataOption> Args { get; set; }
        }

        private string _saveFilePath;
        private DiscordSocketClient _client;
        private Dictionary<ulong, List<Entry>> _entries = new();

        public ScheduledCommandService(string savePath, DiscordSocketClient client)
        {
            _saveFilePath = savePath;
            _client = client;

            // read events
            Load();
        }

        // this will probably need to be specific to schedule-command so it can serialize the args and such.
        public async Task AddRecurringEvent(
            IServiceProvider serviceProvider,
            ulong guildId,
            ulong scheduledByUserId,
            ulong channelId,
            DateTimeOffset start,
            TimeSpan frequency,
            string command,
            IEnumerable<CommandDataOption>? args)
        {
            var entry = new Entry()
            {
                Guid = Guid.NewGuid(),
                StartDate = start,
                Frequency = frequency,
                ScheduledBy = scheduledByUserId,
                ChannelId = channelId,
                Command = command,
                Args = args != null ? args.ToList() : new()
            };

            lock (_entries)
            {
                Utility.GetOrAdd(_entries, guildId).Add(entry);

                Save();
            }

            await StartTimerForEntry(serviceProvider, guildId, entry);
        }

        public ImmutableArray<Entry> GetEntries(ulong guildId) 
            => _entries[guildId].ToImmutableArray();

        public bool RemoveEvent(ulong guildId, Guid eventId)
        {
            lock (_entries)
            {
                if (_entries.ContainsKey(guildId))
                {
                    var entry = _entries[guildId].Where(entry => entry.Guid == eventId).FirstOrDefault();
                    if (entry != null)
                    {
                        Console.WriteLine($"Removing scheduled event {entry.Command} - {eventId}");
                        _entries[guildId].Remove(entry);

                        Save();
                        return true;
                    }
                }
            }
            return false;
        }

        public void StartEvents(IServiceProvider serviceProvider)
        {
            lock (_entries)
            {
                foreach ((ulong guildId, List<Entry> entries) in _entries)
                {
                    entries.ForEach(async entry => await StartTimerForEntry(serviceProvider, guildId, entry));
                }
            }
        }

        private async Task StartTimerForEntry(IServiceProvider serviceProvider, ulong guildId, Entry entry)
        {
            Console.WriteLine($"Starting scheduled event {entry.Command} - {entry.Guid}");

            var context = new ScheduledCommandContext(entry, guildId, _client, serviceProvider);

            try
            {
                CommandInfo commandInfo = CommandHandler.CommandEntries[entry.Command];
                await CommandHandler.ExecuteCommand(commandInfo, context, entry.Args);
            }
            catch (Exception ex)
            {
                await context.RespondAsync($"error running command: {ex.Message}", ephemeral: true);
            }
        }

        private void Save()
        {
            try
            {
                string jsonData = String.Empty;
                lock (_entries)
                {
                    jsonData = JsonConvert.SerializeObject(_entries, new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented
                    });
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_saveFilePath));
                File.WriteAllText(_saveFilePath, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Save schedule command data: {ex}");
            }
        }

        private void Load()
        {
            if (!File.Exists(_saveFilePath))
            {
                return;
            }

            string data = string.Empty;
            try
            {
                data = File.ReadAllText(_saveFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read {_saveFilePath}: {ex.Message}");
                return;
            }

            var result = JsonConvert.DeserializeObject<Dictionary<ulong, List<Entry>>>(data);

            if (result != null)
            {
                lock (_entries)
                {
                    _entries = result;
                }
            }

            Console.WriteLine($"Scheduled Events - Loaded {_saveFilePath}");
        }

        private class ScheduledCommandContext : CommandContext
        {
            public override ISocketMessageChannel Channel => _channel;
            public override SocketGuild Guild => _guild;
            public override IUser User => Guild.GetUser(_entry.ScheduledBy);

            private SocketGuild _guild;
            private ISocketMessageChannel _channel;
            private Entry _entry;

            public ScheduledCommandContext(Entry entry, ulong guildId, DiscordSocketClient client, IServiceProvider serviceProvider)
                : base(client, null, serviceProvider)
            {
                _guild = client.GetGuild(guildId);
                _channel = _guild.GetTextChannel(entry.ChannelId);
                _commandName = entry.Command;
                _entry = entry;
            }

            public override async Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
            {
                await _channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference:null, component);
            }
        }

    }
}
