using Chrono;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Reflection;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Command("schedule", "Schedule things", DefaultPermission.AdminOnly)]
    internal class ScheduleCommand : CommandBase
    {
        public ScheduleCommand(ILogger logger) : base(logger) { }

        public static new SlashCommandBuilder BuildCommand(SlashCommandBuilder defaultBuider, ILogger logger)
        {
            var commandSub = defaultBuider.Options.Find(opt => opt.Name == "command");
            if (commandSub == null)
                throw new ArgumentNullException("command option is null");

            foreach (CommandInfo info in CommandHandler.CommandEntries.Values)
            {
                if (info.Type.GetCustomAttribute<ScheduableCommandAttribute>() == null)
                {
                    continue;
                }

                var subComamnd = new SlashCommandOptionBuilder()
                    .WithName(info.Name)
                    .WithDescription($"Schedule {info.Name}")
                    .WithType(ApplicationCommandOptionType.SubCommand);

                subComamnd.AddOption("start-timestamp", ApplicationCommandOptionType.Integer, "The UTC timestamp in seconds of when to start firing this command. 0 = now.", isRequired:true);
                subComamnd.AddOption("frequency", ApplicationCommandOptionType.String, "How often to run the command (eg 1 day, 1 hour, 30 minutes).", isRequired: true);
                subComamnd.AddOption("delete-previous", ApplicationCommandOptionType.Boolean, "Should the previous message be deleted when the command fires.", isRequired: true);
                subComamnd.AddOption("display-timestamp", ApplicationCommandOptionType.Integer, "The UTC timestamp in seconds of the display time. 0 = none", isRequired: true);

                var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
                foreach (SubCommandInfo subCommandInfo in subCommands)
                {
                    subComamnd.AddOption(CommandBuilder.BuildSlashCommandOption(subCommandInfo, logger));
                }

                foreach (CommandOptionInfo optionInfo in info.Options)
                {
                    subComamnd.AddOption(CommandBuilder.BuildSlashCommandOption(optionInfo, logger));
                }

                commandSub.AddOption(subComamnd);
            }

            return defaultBuider;
        }

        [SubCommand("command", "Schedule a command", typeof(ScheduleCommand), dynamicSubcommands: true)]
        internal class CommandSubCommand : CommandBase
        {
            public override bool WantsToHandleSubCommands => true;

            public CommandSubCommand(ILogger logger) : base(logger) { }

            public override async Task HandleSubCommand(CommandContext context, CommandInfo subCommandInfo, IEnumerable<CommandDataOption>? options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException("Expected valid options");
                }

                string commandName = (string)options.First().Name;
                var remainingOptions = options.First().Options.ToList();
                if (remainingOptions == null)
                {
                    throw new ArgumentNullException("Expected valid options");
                }

                // TODO: is there a way to inject these?
                long startTimestamp = (long)remainingOptions.Find(opt => opt.Name == "start-timestamp").Value;
                string frequencyString = (string)remainingOptions.Find(opt => opt.Name == "frequency").Value;
                bool deletePreviousMessage = (bool)remainingOptions.Find(opt => opt.Name == "delete-previous").Value;
                long displayTimestamp = (long)remainingOptions.Find(opt => opt.Name == "display-timestamp").Value;
                var args = remainingOptions.GetRange(4, remainingOptions.Count - 4);

                var startDate = startTimestamp <= 0 ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(startTimestamp);
                //if (startTimestamp > 0 && startDate < DateTimeOffset.UtcNow)
                //{
                //    await context.RespondAsync("The provided start date is in the past.", ephemeral: true);
                //    return;
                //}

                var parser = new TimeParser();
                TimeSpan frequency = parser.GetSpanFromString(frequencyString);
                if (frequency < TimeSpan.FromSeconds(10)) // TODO: change to 10 minutes when not testing.
                {
                    await context.RespondAsync($"Minimum allowed frequency is 10 seconds");
                    return;
                }

                Logger.LogDebug($"Starting command {commandName} on {startDate} and running every {frequency}. Args: {ArgsToString(args)}");

                if (!CommandHandler.CommandEntries.ContainsKey(commandName))
                {
                    await context.RespondAsync($"No command '{commandName}' found", ephemeral: true);
                    return;
                }

                var id = context.ServiceProvider.GetRequiredService<CommandScheduler>()
                    .AddRecurringEvent(
                    context.ServiceProvider,
                    context.Guild.Id,
                    context.User.Id,
                    context.ChannelId,
                    startDate,
                    frequency,
                    deletePreviousMessage,
                    commandName,
                    args,
                    displayTimestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(displayTimestamp) : null);

                await context.RespondAsync($"Command Scheduled. Id: {id}", ephemeral: true);
            }
        }

        [SubCommand("list", "List Scheduled Events", typeof(ScheduleCommand))]
        internal class ListSubCommand : CommandBase
        {
            public ListSubCommand(ILogger logger) : base(logger) { }

            public override async Task HandleCommand(CommandContext context)
            {
                var service = context.ServiceProvider.GetRequiredService<CommandScheduler>();

                var entries = service.GetEntries(context.Guild.Id);
                if (entries.IsEmpty)
                {
                    await context.RespondAsync("No events currently scheduled.", ephemeral: true);
                    return;
                }

                List<Embed> embeds = new();
                foreach (CommandScheduler.Entry entry in entries)
                {
                    string description = $"**Command**: {entry.Command}\n" +
                        $"**Args**: {ArgsToString(entry.Args)}\n" +
                        $"**Channel**: {context.Guild.GetChannel(entry.ChannelId)?.Name}\n" +
                        $"**Scheduled By**: {context.Guild.GetUser(entry.ScheduledBy).Mention}\n" +
                        $"**Starts**: {TimestampTag.FromDateTime(entry.StartDate.UtcDateTime)}\n" +
                        $"**Runs In** {CommandScheduler.GetTimeUntilEventRuns(entry)}";
                    var builder = new EmbedBuilder()
                        .WithTitle(entry.Guid.ToString())
                        .WithDescription(description)
                        .WithFooter($"Occurs every {entry.Frequency}");
                    embeds.Add(builder.Build());
                }

                await context.RespondAsync(embeds: embeds.ToArray(), ephemeral:true);
            }
        }

        [SubCommand("remove", "Remove a scheduled event", typeof(ScheduleCommand))]
        internal class RemoveSubCommand : CommandBase
        {
            [CommandOption("event-id", "The guid for the event to cancel. Can be found via the list command.")]
            public string GuidString { get; set; }

            public RemoveSubCommand(ILogger logger) : base(logger) { }

            public override async Task HandleCommand(CommandContext context)
            {
                Guid guid;
                if (Guid.TryParse(GuidString, out guid))
                {
                    var service = context.ServiceProvider.GetRequiredService<CommandScheduler>();
                    if (service.RemoveEvent(context.Guild.Id, guid))
                    {
                        await context.RespondAsync($"Removed event id: {guid}", ephemeral: true);
                    }
                    else
                    {
                        await context.RespondAsync($"No event found for id: {guid}", ephemeral: true);
                    }
                }
                else
                {
                    await context.RespondAsync($"Invalid event id: {guid}", ephemeral: true);
                }
            }
        }

        private static string ArgsToString(IEnumerable<CommandDataOption>? args)
        {
            string result = "None";
            if (args != null)
            {
                result = String.Empty;
                foreach (var arg in args)
                {
                    result += $"{arg.Name}: {arg.Value} ";
                }
            }
            return result;
        }
    }
}
