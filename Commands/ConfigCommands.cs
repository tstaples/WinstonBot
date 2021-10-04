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

        private CommandHandler _commandHandler;

        public ConfigCommand(CommandHandler commandHandler)
        {
            _commandHandler = commandHandler;
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
                if (options.Count != 3)
                {
                    Console.WriteLine("[Configure Command] Invalid number of options");
                    return;
                }

                string commandName = (string)options.ElementAt(0).Value;
                string actionName = (string)options.ElementAt(1).Value;
                var t = options.ElementAt(2).Value.GetType();
                SocketRole role = (SocketRole)options.ElementAt(2).Value;
                Console.WriteLine($"[ConfigureCommand] set {commandName}: {actionName} role to {role.Name}");

                var command = _commandHandler.Commands
                    .Where(cmd => cmd.Name == commandName)
                    .SingleOrDefault();
                var action = command?.Actions
                    .Where(action => action.Name == actionName)
                    .Single();

                if (action == null)
                {
                    Console.WriteLine("[ConfigureCommand] Failed to find action.");
                    return;
                }

                action.RoleId = role.Id;
            }
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new ActionContext(client, arg, services);
        }
    }
}
