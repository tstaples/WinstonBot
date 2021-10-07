using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;

namespace WinstonBot.Commands.Config
{
    internal class ConfigureCommandSubCommand : ISubCommand
    {
        public string Name => "command";

        private List<ISubCommand> _subCommands = new()
        {
            new AddRoleOperation(),
            new RemoveRoleOperation(),
            new ViewRolesOperation()
        };

        public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
        }

        //public SlashCommandOptionBuilder Build()
        //{
        //    var actionCommandGroup = new SlashCommandOptionBuilder()
        //        .WithName("command")
        //        .WithDescription("Configure command action permissions")
        //        .WithRequired(false)
        //        .WithType(ApplicationCommandOptionType.SubCommandGroup);

        //    foreach (ISubCommand subCommand in _subCommands)
        //    {
        //        actionCommandGroup.AddOption(subCommand.Build());
        //    }

        //    return actionCommandGroup;
        //}

        public async Task HandleCommand(CommandContext context)
        {
            //if (options == null)
            //{
            //    Console.WriteLine($"Expected valid options for subcommand: {Name}");
            //    return;
            //}

            //string subCommandName = (string)options.First().Value;
            //foreach (ISubCommand subCommand in _subCommands)
            //{
            //    if (subCommand.Name == subCommandName)
            //    {
            //        await subCommand.HandleCommand(context, options.First().Options);
            //        return;
            //    }
            //}
        }

        private static CommandEntry GetCommandEntry(ConfigService configService, ulong guildId, string commandName)
        {
            var entries = configService.Configuration.GuildEntries;
            if (!entries.ContainsKey(guildId))
            {
                entries.TryAdd(guildId, new GuildEntry());
            }

            var commandEntries = entries[guildId].Commands;
            if (!commandEntries.ContainsKey(commandName))
            {
                commandEntries.Add(commandName, new CommandEntry());
            }

            return commandEntries[commandName];
        }

        private class AddRoleOperation : ISubCommand
        {
            public string Name => "add-role";

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                List<SocketSlashCommandDataOption> options = new();//temp

                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                SocketRole? targetRole = options.ElementAt(1).Value as SocketRole;
                if (targetCommand == null || targetRole == null)
                {
                    await context.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                if (targetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot add {context.Guild.EveryoneRole.Mention} to commands as it is the default.\n" +
                        $"To set a command to {context.Guild.EveryoneRole.Mention}, remove all roles for it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                var commandEntry = GetCommandEntry(configService, context.Guild.Id, targetCommand);
                if (Utility.AddUnique(commandEntry.Roles, targetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Added role {targetRole.Mention} to command {targetCommand}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{targetCommand} already contains role {targetRole.Mention}", ephemeral: true);
                }
            }
        }

        private class RemoveRoleOperation : ISubCommand
        {
            public string Name => "remove-role";

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                List<SocketSlashCommandDataOption> options = new();//temp
                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                SocketRole? targetRole = options.ElementAt(1).Value as SocketRole;
                if (targetCommand == null || targetRole == null)
                {
                    await context.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                if (targetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot remove {context.Guild.EveryoneRole.Mention} from commands as it is the default.\n" +
                        $"To make a command not available to {context.Guild.EveryoneRole.Mention}, add additional roles to it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                var commandEntry = GetCommandEntry(configService, context.Guild.Id, targetCommand);
                if (!commandEntry.Roles.Contains(targetRole.Id))
                {
                    commandEntry.Roles.Remove(targetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Removed role {targetRole.Mention} from command {targetCommand}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{targetCommand} doesn't contain role {targetRole.Mention}", ephemeral: true);
                }
            }
        }

        private class ViewRolesOperation : ISubCommand
        {
            public string Name => "view-roles";

            public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public SlashCommandOptionBuilder Build()
            {
                return new SlashCommandOptionBuilder();
            }

            public async Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                List<SocketSlashCommandDataOption> options = new();//temp
                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                if (targetCommand == null)
                {
                    await context.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                var commandEntry = GetCommandEntry(configService, context.Guild.Id, targetCommand);
                if (commandEntry.Roles.Count > 0)
                {
                    await context.RespondAsync($"Roles for {targetCommand}: \n{Utility.JoinRoleMentions(context.Guild, commandEntry.Roles)}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"Roles for {targetCommand}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
