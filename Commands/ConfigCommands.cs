﻿using Discord.Commands;
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
            View,
            ViewAll
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
                    .AddOption(commandOptionBuilder))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("view-all")
                    .WithDescription("Display roles for all the commands and their actions.")
                    .WithType(ApplicationCommandOptionType.SubCommand));

            configureCommands.AddOption(actionCommandGroup);
            configureCommands.AddOption(commandCommandGroup);

            return configureCommands.Build();
        }

        public async Task HandleCommand(Commands.CommandContext context)
        {
            if (context.SlashCommand.Channel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var options = context.SlashCommand.Data.Options;

                string? commandName = null;
                string? actionName = null;
                SocketRole? roleValue = null;
                RoleOperation operation = RoleOperation.View;

                var configureTarget = options.First().Name;
                var configureOperation = options.First().Options.First().Name;
                options = options.First().Options.First().Options;

                T? TryGetValueAt<T>(int index) where T : class
                {
                    if (options != null && index < options.Count)
                    {
                        return (T)options.ElementAt(index).Value;
                    }
                    return null;
                }

                int roleIndex = -1;
                switch (configureTarget)
                {
                    case "action":
                        commandName = TryGetValueAt<string>(0);
                        actionName = TryGetValueAt<string>(1);
                        roleIndex = 2;
                        break;

                    case "command":
                        commandName = TryGetValueAt<string>(0);
                        roleIndex = 1;
                        break;
                }

                switch (configureOperation)
                {
                    case "add-role":
                        operation = RoleOperation.Add;
                        roleValue = TryGetValueAt<SocketRole>(roleIndex);
                        break;

                    case "remove-role":
                        operation = RoleOperation.Remove;
                        roleValue = TryGetValueAt<SocketRole>(roleIndex);
                        break;

                    case "view-roles":
                        // maybe?
                        operation = RoleOperation.View;
                        break;

                    case "view-all":
                        operation = RoleOperation.ViewAll;
                        break;
                }

                if (roleValue != null && roleValue.Id == guild.EveryoneRole.Id && (operation == RoleOperation.Add || operation == RoleOperation.Remove))
                {
                    await context.SlashCommand.RespondAsync($"Cannot add/remove {guild.EveryoneRole.Mention} to/from commands/actions as it is the default.\n" +
                        $"To set an action/command to {guild.EveryoneRole.Mention} remove all roles for it.", ephemeral:true);
                    return;
                }

                Console.WriteLine($"[ConfigureCommand] running {configureTarget} {configureOperation} for command: {commandName}, action: {actionName}, with role: {roleValue?.Name}");

                var command = _commandHandler.Commands
                    .Where(cmd => cmd.Name == commandName)
                    .SingleOrDefault();
                if (command == null && operation != RoleOperation.ViewAll)
                {
                    Console.WriteLine($"[ConfigureCommand] Failed to find command '{commandName}'.");
                    return;
                }

                var action = actionName != null && command != null
                    ? command.Actions.Where(action => action.Name == actionName).Single()
                    : null;

                if (actionName != null && action == null)
                {
                    Console.WriteLine($"[ConfigureCommand] Failed to find action '{actionName}' for command {commandName}.");
                    return;
                }

                var configService = _serviceProvider.GetRequiredService<ConfigService>();
                CommandEntry? entry = commandName != null ? GetCommandEntry(configService, guild.Id, commandName) : null;

                // TODO: remove the switch and do something nicer.
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
                                configService.UpdateConfig(configService.Configuration);
                                await context.SlashCommand.RespondAsync($"Added role {roleValue.Mention} to {commandName}:{actionName}", ephemeral: true);
                                return;
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
                                configService.UpdateConfig(configService.Configuration);
                                await context.SlashCommand.RespondAsync($"Added role {roleValue.Mention} to command {commandName}", ephemeral: true);
                            }
                            else
                            {
                                await context.SlashCommand.RespondAsync($"{commandName} already contains role {roleValue.Mention}", ephemeral: true);
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
                                    configService.UpdateConfig(configService.Configuration);
                                }
                                await context.SlashCommand.RespondAsync($"Removed role {roleValue.Mention} from {commandName}:{actionName}", ephemeral: true);
                            }
                            else
                            {
                                await context.SlashCommand.RespondAsync($"No roles set for {commandName}:{actionName}", ephemeral: true);
                            }
                        }
                        else
                        {
                            if (entry.Roles.Remove(roleValue.Id))
                            {
                                configService.UpdateConfig(configService.Configuration);
                            }
                            await context.SlashCommand.RespondAsync($"Removed role {roleValue.Mention} from command {commandName}", ephemeral: true);
                        }
                        break;

                    case RoleOperation.View:
                        if (action != null)
                        {
                            List<ulong> roles;
                            if (entry.ActionRoles.TryGetValue(actionName, out roles))
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}:{actionName}: \n{Utility.JoinRoleMentions(guild, roles)}", ephemeral: true);
                            }
                            else
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}:{actionName}: \n{guild.EveryoneRole.Mention}", ephemeral: true);
                            }
                        }
                        else if (command != null)
                        {
                            if (entry.Roles.Count > 0)
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}: \n{Utility.JoinRoleMentions(guild, entry.Roles)}", ephemeral: true);
                            }
                            else
                            {
                                await context.SlashCommand.RespondAsync($"Roles for {commandName}:{actionName}: \n{guild.EveryoneRole.Mention}", ephemeral: true);
                            }
                        }
                        break;

                    case RoleOperation.ViewAll:
                        {
                            var adminRoles = guild.Roles.Where(role => role.Id == 773757083904114689);

                            // show all commands
                            List<Embed> embeds = new();
                            var commandEntries = configService.Configuration.GuildEntries[guild.Id].Commands;
                            foreach (ICommand cmd in _commandHandler.Commands)
                            {
                                var embedBuilder = new EmbedBuilder()
                                    .WithTitle(cmd.Name);
                                var commandEntry = commandEntries.ContainsKey(cmd.Name) ? commandEntries[cmd.Name] : null;

                                var fieldBuilder = new EmbedFieldBuilder()
                                        .WithName("Roles for command");
                                if (commandEntry != null && commandEntry.Roles.Any())
                                {
                                    fieldBuilder.WithValue(Utility.JoinRoleMentions(guild, commandEntry.Roles));
                                }
                                else if (cmd.DefaultPermission == ICommand.Permission.AdminOnly)
                                {
                                    fieldBuilder.WithValue(String.Join('\n', adminRoles.Select(role => role.Mention)));
                                }
                                else
                                {
                                    fieldBuilder.WithValue(guild.EveryoneRole.Mention);
                                }

                                embedBuilder.AddField(fieldBuilder);

                                foreach (IAction myAction in cmd.Actions)
                                {
                                    ulong[] roles = new ulong[]{ guild.EveryoneRole.Id };
                                    if (commandEntry != null && commandEntry.ActionRoles.ContainsKey(myAction.Name) && commandEntry.ActionRoles[myAction.Name].Any())
                                    {
                                        roles = commandEntry.ActionRoles[myAction.Name].ToArray();
                                    }

                                    embedBuilder.AddField(new EmbedFieldBuilder()
                                        .WithName(myAction.Name)
                                        .WithValue(Utility.JoinRoleMentions(guild, roles)));
                                }

                                embeds.Add(embedBuilder.Build());
                            }

                            await context.SlashCommand.RespondAsync(embeds: embeds.ToArray(), ephemeral: true);
                            return;
                        }
                        break;
                }
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
