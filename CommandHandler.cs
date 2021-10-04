using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WinstonBot.Services;
using WinstonBot.Data;
using System.Diagnostics;
using WinstonBot.Commands;
using Discord.Net;
using Newtonsoft.Json;

namespace WinstonBot
{
    public class CommandHandler
    {
        public IEnumerable<ICommand> Commands => _commands;

        private readonly DiscordSocketClient _client;
        private IServiceProvider _services;
        private List<ICommand> _commands;

        public CommandHandler(IServiceProvider services, DiscordSocketClient client)
        {
            _client = client;
            _services = services;

            _commands = new List<ICommand>()
            {
                new HostPvmSignup(),
                new ConfigCommand(this), // not great but will do for now.
                new ForceRefreshCommands(this),
                new GenerateAoDMessageCommand(),
            };
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.ButtonExecuted += HandleButtonExecuted;
            _client.InteractionCreated += HandleInteractionCreated;

            foreach (SocketGuild guild in _client.Guilds)
            {
                await ForceRefreshCommands.RegisterCommands(_client, guild, _commands);
            }
        }

        private async Task HandleInteractionCreated(SocketInteraction arg)
        {
            if (arg is SocketSlashCommand slashCommand)
            {
                foreach (ICommand command in _commands)
                {
                    if (command.Name == slashCommand.Data.Name)
                    {
                        // TODO: should we lock the command?
                        Console.WriteLine($"Command {command.Name} handling interaction");
                        var context = new Commands.CommandContext(_client, slashCommand, _services);
                        await command.HandleCommand(context);
                        return;
                    }
                }
            }
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            foreach (ICommand command in _commands)
            {
                foreach (IAction action in command.Actions)
                {
                    if (component.Data.CustomId.StartsWith(action.Name))
                    {
                        // TODO: should we lock the action?
                        // TODO: action could define params and we could parse them in the future.
                        // wouldn't work with the interface though.
                        Console.WriteLine($"Command {command.Name} handling button action: {action.Name}");
                        var context = command.CreateActionContext(_client, component, _services);
                        await action.HandleAction(context);
                        return;
                    }
                }
            }
        }
    }
}
