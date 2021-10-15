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
            public IReadOnlyCollection<SocketSlashCommandDataOption>? Args { get; set; }
        }

        private DiscordSocketClient _client;
        private Dictionary<ulong, List<Entry>> _entries = new();

        public ScheduledCommandService(DiscordSocketClient client)
        {
            _client = client;
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
            IReadOnlyCollection<SocketSlashCommandDataOption>? args)
        {
            var entry = new Entry()
            {
                Guid = Guid.NewGuid(),
                StartDate = start,
                Frequency = frequency,
                ScheduledBy = scheduledByUserId,
                ChannelId = channelId,
                Command = command,
                Args = args
            };

            lock (_entries)
            {
                Utility.GetOrAdd(_entries, guildId).Add(entry);

                // TODO: serialize json to file
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

                        // TODO: update json file
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
