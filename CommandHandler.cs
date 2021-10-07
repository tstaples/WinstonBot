using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Reflection;
using WinstonBot.Commands;
using WinstonBot.Services;

namespace WinstonBot
{
    public class CommandOptionInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string PropertyName { get; set; }
        public Type Type { get; set; }
        public bool Required { get; set; }
        public Type? ChoiceProviderType { get; set; }
    }

    public class ActionInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class CommandInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DefaultPermission DefaultPermission { get; set; }
        public Type Type { get; set; }
        public List<CommandOptionInfo> Options { get; set; }
        public MethodInfo? BuildCommandMethod { get; set; }
        public Dictionary<string, ActionInfo>? Actions { get; set; }
    }

    public class SubCommandInfo : CommandInfo
    {
        public Type ParentCommandType { get; set; }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private IServiceProvider _services;

        private static Dictionary<string, CommandInfo> _commandEntries = new();
        private static List<SubCommandInfo> _subCommandEntries = new();
        private static Dictionary<string, ActionInfo> _actionEntries = new();

        public static IReadOnlyDictionary<string, CommandInfo> CommandEntries => new ReadOnlyDictionary<string, CommandInfo>(_commandEntries);
        public static IReadOnlyCollection<SubCommandInfo> SubCommandEntries => new ReadOnlyCollection<SubCommandInfo>(_subCommandEntries);
        public static IReadOnlyDictionary<string, ActionInfo> ActionEntries => new ReadOnlyDictionary<string, ActionInfo>(_actionEntries);

        public CommandHandler(IServiceProvider services, DiscordSocketClient client)
        {
            _client = client;
            _services = services;
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
                            Description = optionInfo.Description,
                            PropertyName = prop.Name,
                            Required = optionInfo.Required,
                            Type = prop.PropertyType,
                            ChoiceProviderType = optionInfo.ChoiceDataProvider
                        };
                    })
                    .ToList();
            }

            MethodInfo? GetBuildMethod(TypeInfo info)
            {
                // TODO: make a more robust way to define the method (eg. attribute)
                return info.DeclaredMethods.Where(method => method.IsStatic && method.Name == "BuildCommand").SingleOrDefault();
            }

            Dictionary<string, ActionInfo>? GetActions(IEnumerable<Type>? actionTypes)
            {
                if (actionTypes == null)
                {
                    return null;
                }

                return actionTypes.Select(t =>
                {
                    var att = t.GetCustomAttribute<Attributes.ActionAttribute>();
                    if (att == null)
                    {
                        throw new ArgumentException($"Expected ActionAttribute on type {t.Name}");
                    }
                    return _actionEntries[att.Name];
                })
                .ToDictionary(a => a.Name, a => a);
            }

            var assembly = Assembly.GetEntryAssembly();

            // Build the list of actions first
            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                var actionAttribute = typeInfo.GetCustomAttribute<Attributes.ActionAttribute>();
                if (actionAttribute != null)
                {
                    var actionInfo = new ActionInfo()
                    {
                        Name = actionAttribute.Name,
                    };

                    if (!_actionEntries.TryAdd(actionAttribute.Name, actionInfo))
                    {
                        throw new Exception($"Tried to register duplicate action: {actionAttribute.Name}");
                    }
                }
            }

            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                var commandAttribute = typeInfo.GetCustomAttribute<Attributes.CommandAttribute>();
                if (commandAttribute != null)
                {
                    var commandInfo = new CommandInfo()
                    {
                        Name = commandAttribute.Name,
                        Description = commandAttribute.Description,
                        DefaultPermission = commandAttribute.DefaultPermission,
                        Type = typeInfo.AsType(),
                        Options = GetOptions(typeInfo),
                        BuildCommandMethod = GetBuildMethod(typeInfo),
                        Actions = GetActions(commandAttribute.Actions)
                    };

                    if (!_commandEntries.TryAdd(commandAttribute.Name, commandInfo))
                    {
                        throw new Exception($"Tried to register duplicate command: {commandAttribute.Name}");
                    }
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
                        Description = subCommandAttribute.Description,
                        Type = typeInfo.AsType(),
                        ParentCommandType = subCommandAttribute.ParentCommand,
                        Options = GetOptions(typeInfo),
                        BuildCommandMethod = GetBuildMethod(typeInfo),
                        Actions = GetActions(subCommandAttribute.Actions)
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

            foreach (SocketGuild guild in _client.Guilds)
            {
                Console.WriteLine($"Registering commands for guild: {guild.Name}");

                await ForceRefreshCommands.RegisterCommands(_client, guild);
            }
        }

        private async Task HandleInteractionCreated(SocketInteraction arg)
        {
            var configService = _services.GetRequiredService<ConfigService>();
            var slashCommand = arg as SocketSlashCommand;
            if (slashCommand == null)
            {
                return;
            }

            if (!_commandEntries.ContainsKey(slashCommand.CommandName))
            {
                return;
            }

            var command = _commandEntries[slashCommand.CommandName];

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
                        SubCommandInfo? info = _subCommandEntries.Find(sub => sub.Name == subCommandName && sub.ParentCommandType == command.Type);
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
            var context = commandInstance.CreateContext(_client, slashCommand, _services);
            await commandInstance.HandleCommand(context);
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            // Via interaction service
            //CommandInfo interactionOwner = GetInteractionOwner(component.Message.Id);
            CommandInfo interactionOwner = new CommandInfo();

            var configService = _services.GetRequiredService<ConfigService>();
            foreach (ActionInfo action in interactionOwner.Actions.Values)
            {
                if (!component.Data.CustomId.StartsWith(action.Name))
                {
                    continue;
                }

                // If this was executed in a guild channel check the user has permission to use it.
                if (component.Channel is SocketGuildChannel guildChannel)
                {
                    var user = (SocketGuildUser)component.User;
                    // How do we know what command this action belongs to if an action can belong to multiple commands?
                    // should we encode something in the customid?
                    var requiredRoleIds = GetRequiredRolesForAction(configService, guildChannel.Guild, interactionOwner.Name, action.Name);
                    if (!Utility.DoesUserHaveAnyRequiredRole(user, requiredRoleIds))
                    {
                        await component.RespondAsync($"You must have one of the following roles to do this action: {Utility.JoinRoleMentions(guildChannel.Guild, requiredRoleIds)}.", ephemeral: true);
                        return;
                    }
                }
            }

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
