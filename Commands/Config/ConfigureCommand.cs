using Discord;
using Discord.WebSocket;
using WinstonBot.Services;
using WinstonBot.Attributes;

namespace WinstonBot.Commands.Config
{
    [SubCommand("command", "Configure command permissions", typeof(ConfigCommand))]
    internal class ConfigureCommandSubCommand : CommandBase
    {
        public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
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

        [SubCommand(Name = "add-role", ParentCommand = typeof(ConfigureCommandSubCommand))]
        private class AddRoleOperation : CommandBase
        {
            [CommandOption("command", "The command to modify", dataProvider: typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

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
                var commandEntry = GetCommandEntry(configService, context.Guild.Id, TargetCommand);
                if (Utility.AddUnique(commandEntry.Roles, TargetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Added role {TargetRole.Mention} to command {TargetCommand}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{TargetCommand} already contains role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand(Name = "remove-role", ParentCommand = typeof(ConfigureCommandSubCommand))]
        private class RemoveRoleOperation : CommandBase
        {
            [CommandOption("command", "The command to modify", dataProvider: typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

            [CommandOption("role", "The role to remove")]
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
                var commandEntry = GetCommandEntry(configService, context.Guild.Id, TargetCommand);
                if (!commandEntry.Roles.Contains(TargetRole.Id))
                {
                    commandEntry.Roles.Remove(TargetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Removed role {TargetRole.Mention} from command {TargetCommand}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{TargetCommand} doesn't contain role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand(Name = "view-roles", ParentCommand = typeof(ConfigureCommandSubCommand))]
        private class ViewRolesOperation : CommandBase
        {
            [CommandOption("command", "The command to view the roles for", dataProvider: typeof(CommandNameDataProvider))]
            public string TargetCommand { get; set; }

            public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
            {
                return new ConfigCommandContext(client, arg, services);
            }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                var commandEntry = GetCommandEntry(context.ConfigService, context.Guild.Id, TargetCommand);
                if (commandEntry.Roles.Count > 0)
                {
                    await context.RespondAsync($"Roles for {TargetCommand}: \n{Utility.JoinRoleMentions(context.Guild, commandEntry.Roles)}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"Roles for {TargetCommand}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
