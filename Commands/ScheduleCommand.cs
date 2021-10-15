using Chrono;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Reflection;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    // TODO: sub commands to list scheduled events and command to cancel
    // TODO: we could custom build this command and create schedule subcommands for each existing command
    [Command("schedule", "Schedule things", DefaultPermission.AdminOnly, excludeFromCommandProvider: true)]
    internal class ScheduleCommand : CommandBase
    {
        // TODO: would be cool if it could pass in the default built command so we could just append to that.
        public static new SlashCommandBuilder BuildCommand()
        {
            var builder = new SlashCommandBuilder()
                .WithName("schedule")
                .WithDescription("Schedule commands");

            var commandSub = new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("The command to schedule")
                .WithType(ApplicationCommandOptionType.SubCommandGroup);

            
            // TODO: since we define these as subcommands it doesn't work as we're not actually defining those sub commands
            // An alternative is to allow commands to optionally handle the parameter handling.
            // Or we can just make the commands a choice if we can find a way to still show their argument options, but i don't think that's possible.
            foreach (CommandInfo info in CommandHandler.CommandEntries.Values)
            {
                if (info.Type.GetCustomAttribute<ScheduableCommandAttribute>() == null)
                {
                    continue;
                }

                var subComamnd = new SlashCommandOptionBuilder()
                    .WithName(info.Name)
                    .WithDescription(info.Description)
                    .WithType(ApplicationCommandOptionType.SubCommand);

                subComamnd.AddOption("start-timestamp", ApplicationCommandOptionType.Integer, "When to start");
                subComamnd.AddOption("frequency", ApplicationCommandOptionType.String, "How often to run the command");

                var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
                foreach (SubCommandInfo subCommandInfo in subCommands)
                {
                    subComamnd.AddOption(CommandBuilder.BuildSlashCommandOption(subCommandInfo));
                }

                foreach (CommandOptionInfo optionInfo in info.Options)
                {
                    subComamnd.AddOption(CommandBuilder.BuildSlashCommandOption(optionInfo));
                }

                commandSub.AddOption(subComamnd);
            }

            builder.AddOption(commandSub);

            builder.AddOption(new SlashCommandOptionBuilder()
                .WithName("list")
                .WithDescription("List scheduled events")
                .WithType(ApplicationCommandOptionType.SubCommand));

            return builder;
        }

        [SubCommand("command", "Schedule a command", typeof(ScheduleCommand))]
        internal class CommandSubCommand : CommandBase
        {
            [CommandOption("command", "The command to schedule.", required: true, typeof(CommandNameDataProvider))]
            public string CommandName { get; set; }

            [CommandOption("start-timestamp", "The UTC timestamp for when the command should first run.", required: true)]
            public long StartTimestamp { get; set; }

            [CommandOption("frequency", "How often to run the command (eg 1 day, 2 hours, 34 minutes etc).", required: true)]
            public string FrequencyString { get; set; }

            [CommandOption("args", "The args to pass to the command.", required: false)]
            public string? Args { get; set; }

            public override async Task HandleCommand(CommandContext context)
            {
                var startDate = DateTimeOffset.FromUnixTimeSeconds(StartTimestamp);
                //if (startDate < DateTimeOffset.UtcNow)
                //{
                //    await context.RespondAsync("The provided start date is in the past.", ephemeral: true);
                //    return;
                //}

                var parser = new TimeParser();
                TimeSpan frequency = parser.GetSpanFromString(FrequencyString);
                if (frequency < TimeSpan.FromSeconds(10)) // TODO: change to 10 minutes when not testing.
                {
                    await context.RespondAsync($"Minimum allowed frequency is 10 seconds");
                    return;
                }

                Console.WriteLine($"Starting command {CommandName} with args {Args} on {startDate} and running every {frequency}");

                if (!CommandHandler.CommandEntries.ContainsKey(CommandName))
                {
                    await context.RespondAsync($"No command '{CommandName}' found", ephemeral: true);
                    return;
                }

                var channel = (SocketGuildChannel)context.Channel;
                var guild = channel.Guild;

                CommandInfo commandInfo = CommandHandler.CommandEntries[CommandName];
                SocketApplicationCommand appCommand = await guild.GetApplicationCommandAsync(commandInfo.AppCommandId);

                try
                {
                    // Verify the args are valid
                    var propertyValues = ScheduledCommandService.ParseArgs(Args, appCommand.Options);
                }
                catch (ScheduledCommandService.CommandParseException ex)
                {
                    await context.RespondAsync($"Failed to schedule command: {ex.Message}", ephemeral: true);
                    return;
                }

                await context.ServiceProvider.GetRequiredService<ScheduledCommandService>()
                    .AddRecurringEvent(context.ServiceProvider, guild.Id, context.Channel.Id, startDate, frequency, CommandName, Args);
            }
        }
    }
}
