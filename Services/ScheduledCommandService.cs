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
            public ulong ChannelId { get; set; }
            public string Command { get; set; }
            public string? Args { get; set; }
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
            ulong channelId,
            DateTimeOffset start,
            TimeSpan frequency,
            string command,
            string? args)
        {
            var entry = new Entry()
            {
                Guid = Guid.NewGuid(),
                StartDate = start,
                Frequency = frequency,
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

        public void RemoveEvent(ulong guildId, Guid eventId)
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
                    }
                }
            }
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

            var guild = _client.GetGuild(guildId);
            CommandInfo commandInfo = CommandHandler.CommandEntries[entry.Command];

            SocketApplicationCommand appCommand = await guild.GetApplicationCommandAsync(commandInfo.AppCommandId);

            var context = new ScheduledCommandContext(entry, guildId, _client, serviceProvider);

            try
            {
                var propertyValues = ParseArgs(entry.Args, appCommand.Options);

                await ExecuteCommand(_client, serviceProvider, commandInfo, context, propertyValues);
            }
            catch (CommandParseException ex)
            {
                await context.RespondAsync($"error: {ex.Message}", ephemeral: true);
            }
        }

        // TODO: move elsewhere
        internal class CommandParseException : Exception
        {
            public CommandParseException(string message) : base(message)
            {

            }
        }

        public static Dictionary<string, object> ParseArgs(string? args, IEnumerable<SocketApplicationCommandOption> options)
        {
            // TODO: we need better parsing than this.
            string[] argTokens = args?.Split(' ') ?? new string[] { };

            Dictionary<string, object> result = new();
            for (int i = 0; i < options.Count(); ++i)
            {
                var option = options.ElementAt(i);
                if ((!option.Required.HasValue || !option.Required.Value) && i >= argTokens.Length)
                {
                    continue;
                }
                else if (option.Required.HasValue && option.Required.Value && i >= argTokens.Length)
                {
                    throw new CommandParseException($"Missing required option: {option.Name}");
                }

                var arg = argTokens[i];
                if (!option.Choices.Equals(default(ImmutableArray<SocketApplicationCommandChoice>)) && option.Choices.Count > 0)
                {
                    var choices = option.Choices.Where(choice => choice.Name == arg);
                    if (choices.Any())
                    {
                        result.Add(option.Name, choices.First().Value);
                    }
                    else
                    {
                        throw new CommandParseException($"Invalid value for option: {option.Name}");
                    }
                }
                else
                {
                    object value = null;
                    // TODO: implement the other types
                    switch (option.Type)
                    {
                        case ApplicationCommandOptionType.String:
                            value = arg;
                            break;

                        case ApplicationCommandOptionType.Boolean:
                            value = bool.Parse(arg);
                            break;

                        case ApplicationCommandOptionType.Number:
                            value = double.Parse(arg);
                            break;

                        case ApplicationCommandOptionType.Integer:
                            value = int.Parse(arg);
                            break;

                        default:
                            throw new CommandParseException($"Unimplemented arg type: {option.Type}");
                    }

                    if (arg != null)
                    {
                        result.Add(option.Name, value);
                    }
                }
            }
            return result;
        }

        private class ScheduledCommandContext : CommandContext
        {
            public override ISocketMessageChannel Channel => _channel;

            private SocketGuild _guild;
            private ISocketMessageChannel _channel;

            public ScheduledCommandContext(Entry entry, ulong guildId, DiscordSocketClient client, IServiceProvider serviceProvider)
                : base(client, null, serviceProvider)
            {
                _guild = client.GetGuild(guildId);
                _channel = _guild.GetTextChannel(entry.ChannelId);
                _commandName = entry.Command;
            }

            public override async Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
            {
                await _channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference:null, component);
            }
        }

        private static async Task ExecuteCommand(
            DiscordSocketClient client,
            IServiceProvider serviceProvider,
            CommandInfo command,
            ScheduledCommandContext context,
            Dictionary<string, object> dataOptions)
        {
            // TODO: support sub commands?
            CommandBase? commandInstance = null;
            CommandInfo commandInfo = command;
            //var subCommandResult = FindDeepestSubCommand(command, dataOptions);
            //if (subCommandResult != null)
            //{
            //    commandInfo = subCommandResult.Value.Key;
            //    dataOptions = subCommandResult.Value.Value;
            //    commandInstance = Activator.CreateInstance(subCommandResult.Value.Key.Type) as CommandBase;
            //}
            //else
            {
                commandInstance = Activator.CreateInstance(command.Type) as CommandBase;
            }

            if (commandInstance == null)
            {
                throw new Exception($"Failed to construct command {command.Type}");
            }

            foreach ((string optionName, object value) in dataOptions)
            {
                CommandOptionInfo optionInfo = commandInfo.Options.Where(op => op.Name == optionName).First();
                PropertyInfo? property = commandInfo.Type.GetProperty(optionInfo.PropertyName);
                if (property == null)
                {
                    throw new Exception($"Failed to get property {optionInfo.PropertyName} from type {commandInfo.Type}");
                }

                property.SetValue(commandInstance, value);
            }

            Console.WriteLine($"Command {command.Name} handling interaction");
            var contextCreator = Utility.GetInheritedStaticMethod(command.Type, CommandBase.CreateContextName);
            if (contextCreator != null && contextCreator.DeclaringType != typeof(CommandBase))
            {
                throw new Exception($"Scheduled commands cannot use custom command contexts. Command: {command.Name}");
            }
            await commandInstance.HandleCommand(context);
        }
    }
}
