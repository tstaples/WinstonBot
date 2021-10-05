using Discord.Commands;
using WinstonBot.Services;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using System.Diagnostics;

namespace WinstonBot.Commands
{
    public class ConfigCommand : ICommand
    {
        public string Name => "configure";
        public ICommand.Permission DefaultPermission => ICommand.Permission.AdminOnly;
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
        };

        private enum RoleOperation
        {
            Add,
            Remove,
            View
        }

        private CommandHandler _commandHandler;
        private IServiceProvider _serviceProvider;

        public ConfigCommand(CommandHandler commandHandler, IServiceProvider serviceProvider)
        {
            _commandHandler = commandHandler;
            _serviceProvider = serviceProvider;
        }

        public SlashCommandProperties BuildCommand()
        {
            IEnumerable<ICommand> commandList = _commandHandler.Commands
                .Where(cmd => cmd.Name != this.Name);

            // TODO: user sub commands for add/set/get.

            //_client.Rest.BatchEditGuildCommandPermissions
            // could we just configure different commands with options?
            // /configure command:host-pvm action:host role:@pvm-teacher
            // /configure command:host-pvm action:complete role:@pvm-teacher
            // TODO: we might want to use sub commands so we can also do configure view or something. or just make it a separate command.
            var configureCommands = new SlashCommandBuilder()
                .WithName(Name)
                .WithDefaultPermission(false)
                .WithDescription("Set role permissions for the various action");

            var commandOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("The command to configure")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String);

            var actionOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("action")
                .WithDescription("The action to configure")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String);

            var roleOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("The role to set for this action")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true);

            foreach (ICommand command in commandList)
            {
                Debug.Assert(command.Name != this.Name);

                commandOptionBuilder.AddChoice(command.Name, command.Name);
                foreach (IAction action in command.Actions)
                {
                    actionOptionBuilder.AddChoice(action.Name, action.Name);
                }
            }


            var actionCommandGroup = new SlashCommandOptionBuilder()
                .WithName("action")
                .WithDescription("Configure command action permissions")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.SubCommandGroup)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("add-role")
                    .WithDescription("Add a role that is allowed to use this action.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder)
                    .AddOption(actionOptionBuilder)
                    .AddOption(roleOptionBuilder))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove-role")
                    .WithDescription("Remove a role from this action.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder)
                    .AddOption(actionOptionBuilder)
                    .AddOption(roleOptionBuilder))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("view-roles")
                    .WithDescription("View the roles that are allowed to use this action.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder)
                    .AddOption(actionOptionBuilder));

            var commandCommandGroup = new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("Configure command action permissions")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.SubCommandGroup)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("add-role")
                    .WithDescription("Add a role that is allowed to use this command.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder)
                    .AddOption(roleOptionBuilder))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove-role")
                    .WithDescription("Remove a role from this command.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder)
                    .AddOption(roleOptionBuilder))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("view-roles")
                    .WithDescription("View the roles that are allowed to use this command.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(commandOptionBuilder));

            configureCommands.AddOption(actionCommandGroup);
            configureCommands.AddOption(commandCommandGroup);
            //configureCommands.AddOption(commandOptionBuilder);
            //configureCommands.AddOption(actionOptionBuilder);
            //configureCommands.AddOption(roleOptionBuilder);

            return configureCommands.Build();
        }

        public async Task HandleCommand(Commands.CommandContext context)
        {
            if (context.SlashCommand.Channel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var options = context.SlashCommand.Data.Options;

                string commandName = null;
                string actionName = null;
                SocketRole roleValue = null;
                RoleOperation operation = RoleOperation.View;

                var configureTarget = options.First().Name;
                var configureOperation = options.First().Options.First().Name;
                options = options.First().Options.First().Options;

                switch (configureTarget)
                {
                    case "action":
                        commandName = (string)options.ElementAt(0).Value;
                        actionName = (string)options.ElementAt(1).Value;
                        break;

                    case "command":
                        commandName = (string)options.ElementAt(0).Value;
                        break;
                }

                switch (configureOperation)
                {
                    case "add-role":
                        operation = RoleOperation.Add;
                        roleValue = (SocketRole)options.ElementAt(2).Value;
                        break;

                    case "remove-role":
                        operation = RoleOperation.Remove;
                        roleValue = (SocketRole)options.ElementAt(2).Value;
                        break;

                    case "view-roles":
                        operation = RoleOperation.View;
                        break;
                }

                Console.WriteLine($"[ConfigureCommand] running {configureTarget} {configureOperation} for command: {commandName}, action: {actionName}, with role: {roleValue?.Name}");

                var command = _commandHandler.Commands
                    .Where(cmd => cmd.Name == commandName)
                    .SingleOrDefault();
                if (command == null)
                {
                    Console.WriteLine($"[ConfigureCommand] Failed to find command '{commandName}'.");
                    return;
                }

                var action = actionName != null
                    ? command.Actions.Where(action => action.Name == actionName).Single()
                    : null;

                if (actionName != null && action == null)
                {
                    Console.WriteLine($"[ConfigureCommand] Failed to find action '{actionName}' for command {commandName}.");
                    return;
                }

                var configService = _serviceProvider.GetRequiredService<ConfigService>();
                CommandEntry entry = GetCommandEntry(configService, guild.Id, commandName);

                // TODO: create embed for view
                // TODO: remove the switch and do something nicer.
                // TODO: add view all command
                // TODO: add remove all command for particular action/command.
                switch (operation)
                {
                    case RoleOperation.Add:
                        if (action != null)
                        {
                            if (!entry.ActionRoles.ContainsKey(actionName))
                            {
                                entry.ActionRoles.Add(actionName, new List<ulong>());
                            }
                            
                            if (AddUnique(entry.ActionRoles[actionName], roleValue.Id))
                            {
                                await context.SlashCommand.RespondAsync($"Added role {roleValue.Mention} to {commandName}:{actionName}", ephemeral: true);
                            }
                            else
                            {
                                await context.SlashCommand.RespondAsync($"{commandName}:{actionName} already contains role {roleValue.Mention}", ephemeral: true);
                            }
                        }
                        else
                        {
                            if (AddUnique(entry.Roles, roleValue.Id))
                            {
                                await context.SlashCommand.RespondAsync($"Added role {roleValue.Mention} to command {commandName}", ephemeral: true);
                            }
                        }
                        break;

                    case RoleOperation.Remove:
                        if (action != null)
                        {
                            if (entry.ActionRoles.ContainsKey(actionName))
                            {
                                if (entry.ActionRoles[actionName].Remove(roleValue.Id))
                                {
                                    await context.SlashCommand.RespondAsync($"Removed role {roleValue.Mention} from {commandName}:{actionName}", ephemeral: true);
                                }
                            }
                        }
                        else
                        {
                            if (entry.Roles.Remove(roleValue.Id))
                            {
                                await context.SlashCommand.RespondAsync($"Removed role {roleValue.Mention} from command {commandName}", ephemeral: true);
                            }
                        }
                        break;

                    case RoleOperation.View:
                        if (action != null)
                        {
                            List<ulong> roles;
                            if (entry.ActionRoles.TryGetValue(actionName, out roles))
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}:{actionName}: {Utility.JoinRoleMentions(guild, roles)}", ephemeral: true);
                            }
                        }
                        else
                        {
                            if (entry.Roles.Count > 0)
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}: {Utility.JoinRoleMentions(guild, entry.Roles)}", ephemeral: true);
                            }
                        }
                        break;
                }

                configService.UpdateConfig(configService.Configuration);
            }
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new ActionContext(client, arg, services);
        }

        private bool AddUnique(List<ulong> list, ulong value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
                return true;
            }
            return false;
        }

        private CommandEntry GetCommandEntry(ConfigService configService, ulong guildId, string commandName)
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
    }
}
