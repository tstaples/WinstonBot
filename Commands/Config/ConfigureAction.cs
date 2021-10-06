using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;

namespace WinstonBot.Commands.Config
{
    internal class ConfigureActionSubCommand : ISubCommand
    {
        public string Name => "action";

        private List<ISubCommand> _subCommands = new()
        {
            new AddRoleOperation(),
            new RemoveRoleOperation(),
            new ViewRolesOperation()
        };

        public SlashCommandOptionBuilder Build()
        {
            var actionCommandGroup = new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("Configure command action permissions")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.SubCommandGroup);

            foreach (ISubCommand subCommand in _subCommands)
            {
                actionCommandGroup.AddOption(subCommand.Build());
            }

            return actionCommandGroup;
        }

        public async Task HandleCommand(ConfigCommandContext context, IReadOnlyCollection<SocketSlashCommandDataOption>? options)
        {
            if (options == null)
            {
                Console.WriteLine($"Expected valid options for subcommand: {Name}");
                return;
            }

            string subCommandName = (string)options.First().Value;
            foreach (ISubCommand subCommand in _subCommands)
            {
                if (subCommand.Name == subCommandName)
                {
                    await subCommand.HandleCommand(context, options.First().Options);
                    return;
                }
            }
        }

        private static void GetActionRoles(ConfigService configService, ulong guildId, string commandName, string actionName, out List<ulong> roles)
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

            var commandEntry = commandEntries[commandName];
            if (!commandEntry.ActionRoles.ContainsKey(actionName))
            {
                commandEntry.ActionRoles.Add(actionName, new List<ulong>());
            }

            roles = commandEntry.ActionRoles[actionName];
        }

        private class AddRoleOperation : ISubCommand
        {
            public string Name => "add-role";

            public async Task HandleCommand(ConfigCommandContext context, IReadOnlyCollection<SocketSlashCommandDataOption>? options)
            {
                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                string? targetAction = options.ElementAt(1).Value as string;
                SocketRole? targetRole = options.ElementAt(2).Value as SocketRole;
                if (targetCommand == null || targetAction == null || targetRole == null)
                {
                    await context.SlashCommand.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                if (targetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.SlashCommand.RespondAsync($"Cannot add {context.Guild.EveryoneRole.Mention} to commands as it is the default.\n" +
                        $"To set a command to {context.Guild.EveryoneRole.Mention}, remove all roles for it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, targetCommand, targetAction, out actionRoles);
                if (Utility.AddUnique(actionRoles, targetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.SlashCommand.RespondAsync($"Added role {targetRole.Mention} to {targetCommand}:{targetAction}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"{targetCommand}:{targetAction} already contains role {targetRole.Mention}", ephemeral: true);
                }
            }
        }

        private class RemoveRoleOperation : ISubCommand
        {
            public string Name => "remove-role";

            public async Task HandleCommand(ConfigCommandContext context, IReadOnlyCollection<SocketSlashCommandDataOption>? options)
            {
                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                string? targetAction = options.ElementAt(1).Value as string;
                SocketRole? targetRole = options.ElementAt(2).Value as SocketRole;
                if (targetCommand == null || targetAction == null || targetRole == null)
                {
                    await context.SlashCommand.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                if (targetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.SlashCommand.RespondAsync($"Cannot remove {context.Guild.EveryoneRole.Mention} from commands as it is the default.\n" +
                        $"To make a command not available to {context.Guild.EveryoneRole.Mention}, add additional roles to it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, targetCommand, targetAction, out actionRoles);
                if (!actionRoles.Contains(targetRole.Id))
                {
                    actionRoles.Remove(targetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.SlashCommand.RespondAsync($"Removed role {targetRole.Mention} from {targetCommand}:{targetAction}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"{targetCommand}:{targetAction} doesn't contain role {targetRole.Mention}", ephemeral: true);
                }
            }
        }

        private class ViewRolesOperation : ISubCommand
        {
            public string Name => "view-roles";

            public async Task HandleCommand(ConfigCommandContext context, IReadOnlyCollection<SocketSlashCommandDataOption>? options)
            {
                if (options == null)
                {
                    Console.WriteLine($"Expected valid options for subcommand: {Name}");
                    return;
                }

                string? targetCommand = options.ElementAt(0).Value as string;
                string? targetAction = options.ElementAt(1).Value as string;
                if (targetCommand == null || targetAction == null)
                {
                    await context.SlashCommand.RespondAsync("Invalid arguments.", ephemeral: true);
                    return;
                }

                List<ulong> actionRoles;
                GetActionRoles(context.ConfigService, context.Guild.Id, targetCommand, targetAction, out actionRoles);
                if (actionRoles.Count > 0)
                {
                    await context.SlashCommand.RespondAsync($"Roles for {targetCommand}:{targetAction}: \n{Utility.JoinRoleMentions(context.Guild, actionRoles)}", ephemeral: true);
                }
                else
                {
                    await context.SlashCommand.RespondAsync($"Roles for {targetCommand}:{targetAction}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
