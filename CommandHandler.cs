﻿using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
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

        private class CommandOptionInfo
        {
            public string Name {  get; set; }
            public string PropertyName {  get; set; }
            public Type Type {  get; set; }
            public bool Required {  get; set; }
        }

        private class CommandInfo
        {
            public string Name {  get; set; }
            public Type Type {  get; set; }
            public List<CommandOptionInfo> Options { get; set; }
        }

        private class SubCommandInfo : CommandInfo
        {
            public Type ParentCommandType { get; set; }
        }

        //private class SubCommandEntry
        //{
        //    public SubCommandInfo Info { get; set; }
        //    public List<SubCommandInfo> SubCommands { get; set; }
        //}

        //private class CommandEntry
        //{
        //    public CommandInfo Info {  get; set; }
        //    public List<SubCommandEntry> SubCommands { get; set; }
        //}

        private List<CommandInfo> _commandEntries = new();
        private List<SubCommandInfo> _subCommandEntries = new();

        public CommandHandler(IServiceProvider services, DiscordSocketClient client)
        {
            _client = client;
            _services = services;

            //_commands = new List<ICommand>()
            //{
            //    new HostPvmSignup(),
            //    new ConfigCommand(this, _services), // not great but will do for now.
            //    new ForceRefreshCommands(this),
            //    new GenerateAoDMessageCommand(),
            //};
        }

        private async Task LoadCommands()
        {
            List<CommandOptionInfo> GetOptions(TypeInfo info)
            {
                return info.DeclaredProperties
                    .Where(prop => prop.GetCustomAttribute<Attributes.CommandOptionAttribute>() != null)
                    .Select(prop =>
                    {
                        var optionInfo = prop.GetCustomAttribute<Attributes.CommandOptionAttribute>();
                        return new CommandOptionInfo()
                        {
                            Name = optionInfo.Name,
                            PropertyName = prop.Name,
                            Required = optionInfo.Required,
                            Type = prop.PropertyType
                        };
                    })
                    .ToList();
            }

            var assembly = Assembly.GetEntryAssembly();
            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                var commandAttribute = typeInfo.GetCustomAttribute<Attributes.CommandAttribute>();
                if (commandAttribute != null)
                {
                    var commandInfo = new CommandInfo()
                    {
                        Name = commandAttribute.Name,
                        Type = typeInfo.AsType(),
                        Options = GetOptions(typeInfo)
                    };

                    _commandEntries.Add(commandInfo);
                }

                var subCommandAttribute = typeInfo.GetCustomAttribute<Attributes.SubCommandAttribute>();
                if (subCommandAttribute != null)
                {
                    if (subCommandAttribute.ParentCommand.GetCustomAttribute<Attributes.SubCommandAttribute>() == null &&
                        subCommandAttribute.ParentCommand.GetCustomAttribute<Attributes.CommandAttribute>() == null)
                    {
                        throw new Exception($"ParentCommand for type {typeInfo.Name} must have either a Command or SubCommand attribute");
                    }

                    var subCommandInfo = new SubCommandInfo()
                    {
                        Name = subCommandAttribute.Name,
                        Type = typeInfo.AsType(),
                        ParentCommandType = subCommandAttribute.ParentCommand,
                        Options = GetOptions(typeInfo)
                    };

                    _subCommandEntries.Add(subCommandInfo);
                }
            }
        }

        public async Task InstallCommandsAsync()
        {
            await LoadCommands();

            // Hook the MessageReceived event into our command handler
            _client.ButtonExecuted += HandleButtonExecuted;
            _client.InteractionCreated += HandleInteractionCreated;

            // TODO: cache the roles by hashing guild id + command name + action name
            //var configService = _services.GetRequiredService<ConfigService>();
            foreach (SocketGuild guild in _client.Guilds)
            {
                Console.WriteLine($"Registering commands for guild: {guild.Name}");

                //await ForceRefreshCommands.RegisterCommands(_client, guild, _commands);

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
            var configService = _services.GetRequiredService<ConfigService>();
            if (arg is SocketSlashCommand slashCommand)
            {
                foreach (CommandInfo command in _commandEntries)
                {
                    if (command.Name != slashCommand.Data.Name)
                    {
                        continue;
                    }

                    // TODO: we shouldn't need to do this check as we should be able to update the permissions on the command itself.
                    // Though this is likely much easier than dealing with role limits when batch editing.
                    if (arg.Channel is SocketGuildChannel guildChannel)
                    {
                        var user = (SocketGuildUser)arg.User;
                        // TODO: cache this per guild
                        var requiredRoleIds = GetRequiredRolesForCommand(configService, guildChannel.Guild, command.Name);
                        if (!Utility.DoesUserHaveAnyRequiredRole(user, requiredRoleIds))
                        {
                            await arg.RespondAsync($"You must have one of the following roles to use this command: {Utility.JoinRoleMentions(guildChannel.Guild, requiredRoleIds)}.", ephemeral: true);
                            return;
                        }
                    }

                    var dataOptions = slashCommand.Data.Options;

                    KeyValuePair<SubCommandInfo,IReadOnlyCollection<SocketSlashCommandDataOption>?>? FindDeepestSubCommand(IReadOnlyCollection<SocketSlashCommandDataOption>? options)
                    {
                        if (options == null)
                        {
                            return null;
                        }

                        foreach (var optionData in options)
                        {
                            if (optionData.Type == Discord.ApplicationCommandOptionType.SubCommandGroup ||
                                optionData.Type == Discord.ApplicationCommandOptionType.SubCommand)
                            {
                                var result = FindDeepestSubCommand(optionData.Options);
                                if (result != null)
                                {
                                    return result;
                                }

                                string subCommandName = optionData.Name;
                                SubCommandInfo? info = _subCommandEntries.Find(sub => sub.Name == subCommandName);
                                if (info != null)
                                {
                                    return new(info, optionData.Options);
                                }
                            }
                        }
                        return null;
                    }

                    ICommandBase? commandInstance = null;
                    CommandInfo commandInfo = command;
                    var subCommandResult = FindDeepestSubCommand(dataOptions);
                    if (subCommandResult != null)
                    {
                        commandInfo = subCommandResult.Value.Key;
                        dataOptions = subCommandResult.Value.Value;
                        commandInstance = Activator.CreateInstance(subCommandResult.Value.Key.Type) as ICommandBase;
                    }
                    else
                    {
                        commandInstance = Activator.CreateInstance(command.Type) as ICommandBase;
                    }

                    if (commandInstance == null)
                    {
                        throw new Exception($"Failed to construct command {command.Type}");
                    }

                    if (dataOptions != null)
                    {
                        HashSet<string> setProperties = new();
                        foreach (var optionData in dataOptions)
                        {
                            CommandOptionInfo? optionInfo = commandInfo.Options.Find(op => op.Name == optionData.Name);
                            if (optionInfo == null)
                            {
                                Console.WriteLine($"Could not find option {optionData.Name} for command {commandInfo.Name}");
                                continue;
                            }

                            PropertyInfo? property = commandInfo.Type.GetProperty(optionInfo.PropertyName);
                            if (property == null)
                            {
                                throw new Exception($"Failed to get property {optionInfo.PropertyName} from type {commandInfo.Type}");
                            }

                            setProperties.Add(optionData.Name);
                            property.SetValue(commandInstance, optionData.Value);
                        }

                        var requiredParamsNotSet = commandInfo.Options
                            .Where(opt => opt.Required && !setProperties.Contains(opt.Name))
                            .Select(opt => opt.Name);
                        if (requiredParamsNotSet.Any())
                        {
                            throw new ArgumentException($"Missing required arguments for command {commandInfo.Name}: {String.Join(',', requiredParamsNotSet)}");
                        }
                    }

                    Console.WriteLine($"Command {command.Name} handling interaction");
                    //var context = new Commands.CommandContext(_client, slashCommand, _services);
                    var context = commandInstance.CreateContext(_client, slashCommand, _services);
                    await commandInstance.HandleCommand(context);
                    return;
                }
            }
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            // TODO: handle actions with action attribute
            //var configService = _services.GetRequiredService<ConfigService>();
            //foreach (CommandInfo command in _commandEntries)
            //{
            //    foreach (IAction action in command.Actions)
            //    {
            //        if (!component.Data.CustomId.StartsWith(action.Name))
            //        {
            //            continue;
            //        }

            //        if (component.Channel is SocketGuildChannel guildChannel)
            //        {
            //            var user = (SocketGuildUser)component.User;
            //            // TODO: cache this per guild
            //            var requiredRoleIds = GetRequiredRolesForAction(configService, guildChannel.Guild, command.Name, action.Name);
            //            if (!Utility.DoesUserHaveAnyRequiredRole(user, requiredRoleIds))
            //            {
            //                await component.RespondAsync($"You must have one of the following roles to do this action: {Utility.JoinRoleMentions(guildChannel.Guild, requiredRoleIds)}.", ephemeral:true);
            //                return;
            //            }
            //        }

            //        // TODO: should we lock the action?
            //        // TODO: action could define params and we could parse them in the future.
            //        // wouldn't work with the interface though.
            //        Console.WriteLine($"Command {command.Name} handling button action: {action.Name}");
            //        var context = command.CreateActionContext(_client, component, _services);
            //        await action.HandleAction(context);
            //        return;
            //    }
            //}
        }

        private IEnumerable<ulong> GetRequiredRolesForCommand(ConfigService configService, SocketGuild guild, string commandName)
        {
            if (configService.Configuration.GuildEntries.ContainsKey(guild.Id))
            {
                var commands = configService.Configuration.GuildEntries[guild.Id].Commands;
                if (commands.ContainsKey(commandName))
                {
                    return commands[commandName].Roles;
                }
            }
            return new List<ulong>();
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
    }
}
