using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WinstonBot.Services;
using WinstonBot.Data;
using System.Diagnostics;
using WinstonBot.Commands;

namespace WinstonBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private IServiceProvider _services;
        IEnumerable<ICommand> _guildCommands; // TODO: maybe make this a service?

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(IServiceProvider services, DiscordSocketClient client, IEnumerable<ICommand> commands)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = client;
            _services = services;
            _guildCommands = commands;
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.ButtonExecuted += HandleButtonExecuted;
            _client.InteractionCreated += HandleInteractionCreated;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: _services);
        }

        private bool UserHasRoleForCommand(SocketUser user, SocketRole requiredRole)
        {
            if (user is SocketGuildUser guildUser)
            {
                return guildUser.Roles.Contains(requiredRole);
            }
            return false;
        }

        private async Task HandleInteractionCreated(SocketInteraction arg)
        {
            if (arg is SocketSlashCommand slashCommand)
            {
                foreach (ICommand guildCommand in _guildCommands)
                {
                    if (guildCommand.Name == slashCommand.Data.Name)
                    {
                        await guildCommand.HandleCommand(slashCommand);
                        return;
                    }
                }
            }
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            foreach (ICommand guildCommand in _guildCommands)
            {
                foreach (IAction action in guildCommand.Actions)
                {
                    if (component.Data.CustomId.StartsWith(action.Name))
                    {
                        // TODO: action could define params and we could parse them in the future.
                        // wouldn't work with the interface though.
                        await action.HandleAction(component);
                        return;
                    }
                }
            }
        }
    }
}
