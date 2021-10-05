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
        public string Name => "configure-command";
        public ICommand.Permission DefaultPermission => ICommand.Permission.AdminOnly;
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
        };

        private enum RoleOperation
        {
            Add,
            Remove
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

            var operationOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("operation")
                .WithDescription("Add or remove a role")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Integer)
                .AddChoice("add-role", (int)RoleOperation.Add)
                .AddChoice("remove-role", (int)RoleOperation.Remove);

            foreach (ICommand command in commandList)
            {
                Debug.Assert(command.Name != this.Name);

                commandOptionBuilder.AddChoice(command.Name, command.Name);
                foreach (IAction action in command.Actions)
                {
                    actionOptionBuilder.AddChoice(action.Name, action.Name);
                }
            }

            configureCommands.AddOption(commandOptionBuilder);
            configureCommands.AddOption(actionOptionBuilder);
            configureCommands.AddOption(operationOptionBuilder);
            configureCommands.AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("The role to set for this action")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true));

            return configureCommands.Build();
        }

        public async Task HandleCommand(Commands.CommandContext context)
        {
            if (context.SlashCommand.Channel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var options = context.SlashCommand.Data.Options;
                if (options.Count != 4)
                {
                    Console.WriteLine("[Configure Command] Invalid number of options");
                    return;
                }

                string commandName = (string)options.ElementAt(0).Value;
                string actionName = (string)options.ElementAt(1).Value;
                RoleOperation operation = (RoleOperation)(long)options.ElementAt(2).Value;
                var t = options.ElementAt(2).Value.GetType();
                SocketRole role = (SocketRole)options.ElementAt(3).Value;
                Console.WriteLine($"[ConfigureCommand] set {commandName}: {actionName} role to {role.Name}");

                var command = _commandHandler.Commands
                    .Where(cmd => cmd.Name == commandName)
                    .SingleOrDefault();
                var action = command?.Actions
                    .Where(action => action.Name == actionName)
                    .Single();

                if (action == null)
                {
                    Console.WriteLine($"[ConfigureCommand] Failed to find action '{actionName}' for command {commandName}.");
                    return;
                }

                // Update the config file.
                var configService = _serviceProvider.GetRequiredService<ConfigService>();
                var entries = configService.Configuration.GuildEntries;
                if (!entries.ContainsKey(guild.Id))
                {
                    entries.TryAdd(guild.Id, new GuildEntry());
                }

                var commandRoles = entries[guild.Id].Commands;
                if (!commandRoles.ContainsKey(commandName))
                {
                    commandRoles.Add(commandName, new CommandEntry());
                }

                List<ulong> roles = new();
                var commandEntry = commandRoles[commandName];
                if (!commandEntry.ActionRoles.ContainsKey(actionName))
                {
                    commandEntry.ActionRoles.Add(actionName, roles);
                }
                else
                {
                    roles = commandEntry.ActionRoles[actionName];
                }

                switch (operation)
                {
                    case RoleOperation.Add:
                        roles.Add(role.Id);
                        await context.SlashCommand.RespondAsync($"Added role {role.Name} to action {actionName} for command {commandName}", ephemeral: true);
                        break;

                    case RoleOperation.Remove:
                        if (roles.Contains(role.Id))
                        {
                            roles.Remove(role.Id);
                            await context.SlashCommand.RespondAsync($"Removed role {role.Name} to action {actionName} for command {commandName}", ephemeral: true);
                        }
                        else
                        {
                            await context.SlashCommand.RespondAsync($"Role {role.Name} isn't set for action {actionName} for command {commandName}", ephemeral: true);
                        }
                        break;
                }

                commandEntry.ActionRoles[actionName] = roles;

                configService.UpdateConfig(configService.Configuration);

            }
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new ActionContext(client, arg, services);
        }
    }
}
