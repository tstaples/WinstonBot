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
    [Command("schedule", "Schedule things", DefaultPermission.AdminOnly)]
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

                subComamnd.AddOption("start-timestamp", ApplicationCommandOptionType.Integer, "The UTC timestamp in seconds of when to start firing this command.");
                subComamnd.AddOption("frequency", ApplicationCommandOptionType.String, "How often to run the command (eg 1 day, 1 hour, 30 minutes).");

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
            public override bool WantsToHandleSubCommands => true;

            public override async Task HandleSubCommand(CommandContext context, CommandInfo subCommandInfo, IReadOnlyCollection<SocketSlashCommandDataOption>? options)
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

                long startTimestamp = (long)remainingOptions.Find(opt => opt.Name == "start-timestamp").Value;
                string frequencyString = (string)remainingOptions.Find(opt => opt.Name == "frequency").Value;
                var args = remainingOptions.GetRange(2, remainingOptions.Count - 2);

                var startDate = DateTimeOffset.FromUnixTimeSeconds(startTimestamp);
                // TODO: uncomment when we start testing real timers
                //if (startDate < DateTimeOffset.UtcNow)
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

                Console.WriteLine($"Starting command {commandName} on {startDate} and running every {frequency}");

                if (!CommandHandler.CommandEntries.ContainsKey(commandName))
                {
                    await context.RespondAsync($"No command '{commandName}' found", ephemeral: true);
                    return;
                }

                var channel = (SocketGuildChannel)context.Channel;
                var guild = channel.Guild;

                await context.ServiceProvider.GetRequiredService<ScheduledCommandService>()
                    .AddRecurringEvent(context.ServiceProvider, guild.Id, context.Channel.Id, startDate, frequency, commandName, args);
            }
        }
    }
}
