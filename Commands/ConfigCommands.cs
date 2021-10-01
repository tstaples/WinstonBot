using Discord.Commands;
using WinstonBot.Services;
using WinstonBot.MessageHandlers;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    public class ConfigCommand : ICommand
    {
        public string Name => "configure-command";

        public int Id => 3;// TODO

        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
        };

        private IEnumerable<ICommand> _otherCommands;

        public ConfigCommand(IEnumerable<ICommand> otherCommands)
        {
            _otherCommands = otherCommands;
        }

        public SlashCommandProperties BuildCommand()
        {
            //_client.Rest.BatchEditGuildCommandPermissions
            // could we just configure different commands with options?
            // /configure command:host-pvm action:host role:@pvm-teacher
            // /configure command:host-pvm action:complete role:@pvm-teacher
            // TODO: we might want to use sub commands so we can also do configure view or something. or just make it a separate command.
            var configureCommands = new SlashCommandBuilder()
                .WithName("configure-command")
                .WithDescription("Set role permissions for the various action");

            var commandOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("The command to configure")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Integer);

            var actionOptionBuilder = new SlashCommandOptionBuilder()
                .WithName("action")
                .WithDescription("The action to configure")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Integer);
            foreach (ICommand command in _otherCommands)
            {
                // Skip ourself.
                if (command.Name == this.Name)
                {
                    continue;
                }

                commandOptionBuilder.AddChoice(command.Name, command.Id);
                foreach (IAction action in command.Actions)
                {
                    // TODO: action id could just be its index.
                    actionOptionBuilder.AddChoice(action.Name, action.Id);
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

        public Task HandleCommand(SocketSlashCommand slashCommand)
        {
            return Task.CompletedTask;
        }
    }
}
