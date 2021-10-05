using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Commands;
using WinstonBot.Services;

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
                new ConfigCommand(this, _services), // not great but will do for now.
                new ForceRefreshCommands(this),
                new GenerateAoDMessageCommand(),
            };
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.ButtonExecuted += HandleButtonExecuted;
            _client.InteractionCreated += HandleInteractionCreated;

            // TODO: cache the roles by hashing guild id + command name + action name
            //var configService = _services.GetRequiredService<ConfigService>();
            foreach (SocketGuild guild in _client.Guilds)
            {
                Console.WriteLine($"Registering commands for guild: {guild.Name}");

                await ForceRefreshCommands.RegisterCommands(_client, guild, _commands);

                //Console.WriteLine($"Setting action permissions for guild: {guild.Name}");

                //// Set action roles from the config values
                //GuildEntry? guildEntry = null;
                //configService.Configuration.GuildEntries.TryGetValue(guild.Id, out guildEntry);

                //foreach (ICommand command in _commands)
                //{
                //    Dictionary<string, ulong> actionRoles = new();
                //    guildEntry?.Commands.TryGetValue(command.Name, out actionRoles);

                //    foreach (IAction action in command.Actions)
                //    {
                //        ulong roleId = guild.EveryoneRole.Id;
                //        if (!actionRoles.TryGetValue(action.Name, out roleId))
                //        {
                //            roleId = guild.EveryoneRole.Id;
                //        }

                //        var role = guild.GetRole(roleId);
                //        Console.WriteLine($"Setting {command.Name}: {action.Name} role to {role.Name}");
                //        action.RoleId = roleId;
                //    }
                //}
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

        private IEnumerable<ulong> GetRequiredRolesForAction(ConfigService configService, SocketGuild guild, string commandName, string actionName)
        {
            if (configService.Configuration.GuildEntries.ContainsKey(guild.Id))
            {
                var commandRoles = configService.Configuration.GuildEntries[guild.Id].Commands;
                if (commandRoles.ContainsKey(commandName))
                {
                    var command = commandRoles[commandName];
                    if (command.ActionRoles.ContainsKey(actionName))
                    {
                        return command.ActionRoles[actionName];
                    }
                }
            }
            return new List<ulong>();
        }

        private bool DoesUserHaveAnyRequiredRole(SocketGuildUser user, IEnumerable<ulong> roles)
        {
            return !roles.Any() || user.Roles
                .Where(role => roles.Contains(role.Id))
                .Any();
        }

        private string GetRoleNames(SocketGuild guild, IEnumerable<ulong> roles)
        {
            return String.Join(',', roles.Select(roleId => guild.GetRole(roleId)));
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            var configService = _services.GetRequiredService<ConfigService>();
            foreach (ICommand command in _commands)
            {
                foreach (IAction action in command.Actions)
                {
                    if (!component.Data.CustomId.StartsWith(action.Name))
                    {
                        continue;
                    }

                    if (component.Channel is SocketGuildChannel guildChannel)
                    {
                        var user = (SocketGuildUser)component.User;
                        // TODO: cache this per guild
                        var requiredRoleIds = GetRequiredRolesForAction(configService, guildChannel.Guild, command.Name, action.Name);
                        if (!DoesUserHaveAnyRequiredRole(user, requiredRoleIds))
                        {
                            await component.RespondAsync($"You must have one of the following roles to do this action: {GetRoleNames(guildChannel.Guild, requiredRoleIds)}.", ephemeral:true);
                            return;
                        }
                    }

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
