using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using WinstonBot.Commands;
using Newtonsoft.Json;
using Discord.Rest;

namespace WinstonBot.Services
{
    internal class ScheduledCommandService
    {
        internal class Entry
        {
            public Guid Guid {  get; set; }
            public DateTimeOffset StartDate { get; set; }
            public TimeSpan Frequency { get; set; }
            public bool DeletePreviousMessage { get; set; }
            public ulong ScheduledBy { get; set; }
            public ulong ChannelId { get; set; }
            public string Command { get; set; }
            public List<CommandDataOption> Args { get; set; }

            // Set when the event is first run
            public DateTimeOffset LastRun { get; set; }
            public ulong PreviousMessageId { get; set; } = 0;
        }

        private class TimerCommandData
        {
            public Entry Entry { get; set; }
            public IServiceProvider Services { get; set; }
            public ulong GuildId { get; set; }
        }

        private class TimerEntry : IDisposable
        {
            public Guid Id { get; private set; }
            public Timer Timer { get; private set; }

            public TimerEntry(Guid id, Timer timer)
            {
                Id = id;
                Timer = timer;
            }

            public void Dispose()
            {
                if (Timer != null)
                {
                    Timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        private string _saveFilePath;
        private DiscordSocketClient _client;
        private Dictionary<ulong, List<Entry>> _entries = new();
        private List<TimerEntry> _timers = new();
        private object _fileLock = new();

        public ScheduledCommandService(string savePath, DiscordSocketClient client)
        {
            _saveFilePath = savePath;
            _client = client;
        }

        // this will probably need to be specific to schedule-command so it can serialize the args and such.
        public Guid AddRecurringEvent(
            IServiceProvider serviceProvider,
            ulong guildId,
            ulong scheduledByUserId,
            ulong channelId,
            DateTimeOffset start,
            TimeSpan frequency,
            bool deletePreviousMessage,
            string command,
            IEnumerable<CommandDataOption>? args)
        {
            var entry = new Entry()
            {
                Guid = Guid.NewGuid(),
                StartDate = start,
                Frequency = frequency,
                DeletePreviousMessage = deletePreviousMessage,
                ScheduledBy = scheduledByUserId,
                ChannelId = channelId,
                Command = command,
                Args = args != null ? args.ToList() : new(),
                LastRun = DateTimeOffset.MinValue
            };

            lock (_entries)
            {
                Utility.GetOrAdd(_entries, guildId).Add(entry);
            }

            Save();

            StartTimerForEntry(serviceProvider, guildId, entry);

            return entry.Guid;
        }

        public ImmutableArray<Entry> GetEntries(ulong guildId) 
            => _entries[guildId].ToImmutableArray();

        public bool RemoveEvent(ulong guildId, Guid eventId)
        {
            bool result = false;
            lock (_entries)
            {
                if (_entries.ContainsKey(guildId))
                {
                    var entry = _entries[guildId].Where(entry => entry.Guid == eventId).FirstOrDefault();
                    if (entry != null)
                    {
                        Console.WriteLine($"Removing scheduled event {entry.Command} - {eventId}");
                        _entries[guildId].Remove(entry);

                        var timer = _timers.Find(timer => timer.Id == entry.Guid);
                        if (timer != null)
                        {
                            timer.Dispose();
                            _timers.Remove(timer);
                        }

                        result = true;
                    }
                }
            }

            if (result)
            {
                Save();
            }
            return result;
        }

        public void StartEvents(IServiceProvider serviceProvider)
        {
            Load();

            Console.WriteLine("[ScheduledCommandService] Starting loaded events");
            lock (_entries)
            {
                foreach ((ulong guildId, List<Entry> entries) in _entries)
                {
                    // Only start timers for guilds that are valid
                    if (_client.GetGuild(guildId) != null)
                    {
                        entries.ForEach(entry => StartTimerForEntry(serviceProvider, guildId, entry));
                    }
                }
            }
        }

        public static TimeSpan GetTimeUntilEventRuns(Entry entry)
        {
            var now = DateTimeOffset.Now;
            if (entry.StartDate > now)
            {
                return entry.StartDate - now;
            }

            if ((entry.LastRun + entry.Frequency) < now)
            {
                return TimeSpan.Zero;
            }
            else
            {
                return (entry.LastRun + entry.Frequency) - now;
            }
        }

        private void StartTimerForEntry(IServiceProvider serviceProvider, ulong guildId, Entry entry)
        {
            Console.WriteLine($"Starting scheduled event {entry.Command} - {entry.Guid}");

            var data = new TimerCommandData()
            {
                Entry = entry,
                Services = serviceProvider,
                GuildId = guildId,
            };

            var now = DateTimeOffset.Now;
            if (entry.StartDate > now)
            {
                TimeSpan diff = entry.StartDate - now;

                Console.WriteLine($"Start date is in the future, starting in {diff}");
                var timer = new Timer(Timer_Elapsed, data, diff, entry.Frequency);
                _timers.Add(new TimerEntry(entry.Guid, timer));
                return;
            }

            if (entry.StartDate < now)
            {
                if ((entry.LastRun + entry.Frequency) < now)
                {
                    Console.WriteLine($"Start date is in the past and it's been more time since the last run than our frequency, running now");
                    var timer = new Timer(Timer_Elapsed, data, TimeSpan.Zero, entry.Frequency);
                    _timers.Add(new TimerEntry(entry.Guid, timer));
                    return;
                }
                else
                {
                    TimeSpan diff = (entry.LastRun + entry.Frequency) - now;
                    Console.WriteLine($"Start date is in the past but not enough time has elapsed since we last ran. Starting in {diff}");

                    var timer = new Timer(Timer_Elapsed, data, diff, entry.Frequency);
                    _timers.Add(new TimerEntry(entry.Guid, timer));
                }
            }
        }

        private void Timer_Elapsed(object state)
        {
            var data = (TimerCommandData)state;
            Console.WriteLine($"Timer elapsed for {data.GuildId}: {data.Entry.Command}");

            var context = new ScheduledCommandContext(data.Entry, data.GuildId, _client, data.Services);
            if (data.Entry.DeletePreviousMessage && data.Entry.PreviousMessageId != 0)
            {
                Console.WriteLine($"Deleting previous message {data.Entry.PreviousMessageId}");
                context.DeleteMessageAsync(data.Entry.PreviousMessageId);
            }

            try
            {
                data.Entry.LastRun = DateTime.Now;
                data.Entry.PreviousMessageId = 0;

                CommandInfo commandInfo = CommandHandler.CommandEntries[data.Entry.Command];
                Task.Run(async () =>
                {
                    await CommandHandler.ExecuteCommand(commandInfo, context, data.Entry.Args);
                    Save();
                });
            }
            catch (Exception ex)
            {
                context.RespondAsync($"error running command: {ex.Message}", ephemeral: true).Forget();
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

                Console.WriteLine($"Saving scheduled command data");
                lock (_fileLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_saveFilePath));
                    File.WriteAllText(_saveFilePath, jsonData);
                }
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
                lock (_fileLock)
                {
                    data = File.ReadAllText(_saveFilePath);
                }
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
            public override ulong ChannelId => _channel.Id;
            public override SocketGuild Guild => _guild;
            public override IUser User => Guild.GetUser(_entry.ScheduledBy);

            protected override ISocketMessageChannel Channel => _channel;
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
                // Since we have no interaction we just send a regular channel message.
                var message = await base.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference:null, component);
                _entry.PreviousMessageId = message.Id;
            }

            public override async Task<RestUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent component = null, ISticker[] stickers = null)
            {
                var message = await base.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, component, stickers);
                _entry.PreviousMessageId = message.Id;
                return message;
            }
        }

    }
}
