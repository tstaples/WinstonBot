using Discord;
using Discord.WebSocket;
using WinstonBot.Services;
using WinstonBot.Attributes;

namespace WinstonBot.Commands.Config
{
    [SubCommand("action", "Configure command action permissions", typeof(ConfigCommand))]
    internal class ConfigureActionSubCommand : CommandBase
    {
        public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
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

        [SubCommand("add-role", "Add a role requirement to an action.", typeof(ConfigureActionSubCommand))]
        private class AddRoleOperation : CommandBase
        {
            [CommandOption("command", "The command to modify", dataProvider:typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

            [CommandOption("action", "The action to modify", dataProvider:typeof(ActionDataProvider))]
            public string TargetAction { get; set; }

            [CommandOption("role", "The role to add")]
            public SocketRole TargetRole { get; set; }

            public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot add {context.Guild.EveryoneRole.Mention} to commands as it is the default.\n" +
                        $"To set a command to {context.Guild.EveryoneRole.Mention}, remove all roles for it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (Utility.AddUnique(actionRoles, TargetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Added role {TargetRole.Mention} to {TargetCommand}:{TargetAction}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{TargetCommand}:{TargetAction} already contains role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand("remove-role", "Remove a role requirement from an action.", typeof(ConfigureActionSubCommand))]
        private class RemoveRoleOperation : CommandBase
        {
            [CommandOption("command", "The command to modify", dataProvider: typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

            [CommandOption("action", "The action to modify", dataProvider: typeof(ActionDataProvider))]
            public string TargetAction { get; set; }

            [CommandOption("role", "The role to add")]
            public SocketRole TargetRole { get; set; }

            public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;
                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot remove {context.Guild.EveryoneRole.Mention} from commands as it is the default.\n" +
                        $"To make a command not available to {context.Guild.EveryoneRole.Mention}, add additional roles to it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                List<ulong> actionRoles;
                GetActionRoles(configService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (actionRoles.Contains(TargetRole.Id))
                {
                    actionRoles.Remove(TargetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Removed role {TargetRole.Mention} from {TargetCommand}:{TargetAction}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{TargetCommand}:{TargetAction} doesn't contain role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand("view-roles", "View the role requirements for an action.", typeof(ConfigureActionSubCommand))]
        private class ViewRolesOperation : CommandBase
        {
            [CommandOption("command", "The command to modify", dataProvider: typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

            [CommandOption("action", "The action to modify", dataProvider: typeof(ActionDataProvider))]
            public string TargetAction { get; set; }

            public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                List<ulong> actionRoles;
                GetActionRoles(context.ConfigService, context.Guild.Id, TargetCommand, TargetAction, out actionRoles);
                if (actionRoles.Count > 0)
                {
                    await context.RespondAsync($"Roles for {TargetCommand}:{TargetAction}: \n{Utility.JoinRoleMentions(context.Guild, actionRoles)}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"Roles for {TargetCommand}:{TargetAction}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
