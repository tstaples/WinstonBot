using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Reflection;
using WinstonBot.Commands;
using WinstonBot.Services;
using WinstonBot.Attributes;

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

    public class ActionOptionInfo
    {
        //public string Name { get; set; }
        public PropertyInfo Property { get; set; }
    }

    public class ActionInfo
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public List<ActionOptionInfo>? Options { get; set; }
    }

    public class CommandInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DefaultPermission DefaultPermission { get; set; }
        public Type Type { get; set; }
        public List<CommandOptionInfo> Options { get; set; }
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
                return info.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => prop.GetCustomAttribute<CommandOptionAttribute>() != null)
                    .Select(prop =>
                    {
                        var optionInfo = prop.GetCustomAttribute<CommandOptionAttribute>();
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

            Dictionary<string, ActionInfo>? GetActions(IEnumerable<Type>? actionTypes)
            {
                if (actionTypes == null)
                {
                    return null;
                }

                return actionTypes.Select(t =>
                {
                    var att = t.GetCustomAttribute<ActionAttribute>();
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
                var actionAttribute = typeInfo.GetCustomAttribute<ActionAttribute>();
                if (actionAttribute != null)
                {
                    var actionInfo = new ActionInfo()
                    {
                        Name = actionAttribute.Name,
                        Type = typeInfo,
                        Options = typeInfo.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
                            .Where(prop => prop.GetCustomAttribute<ActionParamAttribute>() != null)
                            .Select(prop => new ActionOptionInfo() { Property = prop })
                            .ToList()
                    };

                    if (!_actionEntries.TryAdd(actionAttribute.Name, actionInfo))
                    {
                        throw new Exception($"Tried to register duplicate action: {actionAttribute.Name}");
                    }
                }
            }

            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                var commandAttribute = typeInfo.GetCustomAttribute<CommandAttribute>();
                if (commandAttribute != null)
                {
                    var commandInfo = new CommandInfo()
                    {
                        Name = commandAttribute.Name,
                        Description = commandAttribute.Description,
                        DefaultPermission = commandAttribute.DefaultPermission,
                        Type = typeInfo,
                        Options = GetOptions(typeInfo),
                        Actions = GetActions(commandAttribute.Actions)
                    };

                    if (!_commandEntries.TryAdd(commandAttribute.Name, commandInfo))
                    {
                        throw new Exception($"Tried to register duplicate command: {commandAttribute.Name}");
                    }
                }

                var subCommandAttribute = typeInfo.GetCustomAttribute<SubCommandAttribute>();
                if (subCommandAttribute != null)
                {
                    if (subCommandAttribute.ParentCommand.GetCustomAttribute<SubCommandAttribute>() == null &&
                        subCommandAttribute.ParentCommand.GetCustomAttribute<CommandAttribute>() == null)
                    {
                        throw new Exception($"ParentCommand for type {typeInfo.Name} must have either a Command or SubCommand attribute");
                    }

                    var subCommandInfo = new SubCommandInfo()
                    {
                        Name = subCommandAttribute.Name,
                        Description = subCommandAttribute.Description,
                        Type = typeInfo,
                        ParentCommandType = subCommandAttribute.ParentCommand,
                        Options = GetOptions(typeInfo),
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
            CommandBase? commandInstance = null;
            CommandInfo commandInfo = command;
            var subCommandResult = FindDeepestSubCommand(command, dataOptions);
            if (subCommandResult != null)
            {
                commandInfo = subCommandResult.Value.Key;
                dataOptions = subCommandResult.Value.Value;
                commandInstance = Activator.CreateInstance(subCommandResult.Value.Key.Type) as CommandBase;
            }
            else
            {
                commandInstance = Activator.CreateInstance(command.Type) as CommandBase;
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
            var createContextFunction = Utility.GetInheritedStaticMethod(command.Type, CommandBase.CreateContextName);
            var context = createContextFunction?.Invoke(null, new object[] { _client, slashCommand, _services }) as CommandContext;
            if (context == null)
            {
                throw new ArgumentNullException($"Failed to create context for command {command.Name}");
            }
            await commandInstance.HandleCommand(context);
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            // For now just assume actions are unique per command.
            // If we want to change this in the future we'll have to implement the interaction service properly.
            CommandInfo? interactionOwner = null;
            ActionInfo? action = null;
            foreach (var pair in _commandEntries)
            {
                if (pair.Value.Actions == null)
                {
                    continue;
                }

                foreach ((string name, ActionInfo info) in pair.Value.Actions)
                {
                    if (component.Data.CustomId.StartsWith(name))
                    {
                        action = info;
                        interactionOwner = pair.Value;
                        break;
                    }
                }
            }

            if (interactionOwner == null || action == null)
            {
                Console.WriteLine($"No action found for interaction: {component.Data.CustomId}");
                return;
            }

            // If this was executed in a guild channel check the user has permission to use it.
            if (component.Channel is SocketGuildChannel guildChannel)
            {
                var user = (SocketGuildUser)component.User;
                // How do we know what command this action belongs to if an action can belong to multiple commands?
                // should we encode something in the customid?
                var configService = _services.GetRequiredService<ConfigService>();
                var requiredRoleIds = GetRequiredRolesForAction(configService, guildChannel.Guild, interactionOwner.Name, action.Name);
                if (!Utility.DoesUserHaveAnyRequiredRole(user, requiredRoleIds))
                {
                    await component.RespondAsync($"You must have one of the following roles to do this action: {Utility.JoinRoleMentions(guildChannel.Guild, requiredRoleIds)}.", ephemeral: true);
                    return;
                }
            }

            IAction actionInstance = Activator.CreateInstance(action.Type) as IAction;
            if (actionInstance == null)
            {
                throw new Exception($"Failed to construct action {action.Type}");
            }

            var tokens = component.Data.CustomId.Split('_');
            if (tokens.Length > 1)
            {
                // Skip the first token which is the action name
                tokens = tokens.TakeLast(tokens.Length - 1).ToArray();
                if (action.Options?.Count != tokens.Length)
                {
                    throw new Exception($"Action option mismatch. Got {tokens.Length}, expected {action.Options?.Count}");
                }

                for (int i = 0; i < tokens.Length; ++i)
                {
                    ActionOptionInfo optionInfo = action.Options[i];
                    object value = null;
                    if (optionInfo.Property.PropertyType == typeof(string))
                    {
                        value = tokens[i];
                    }
                    else if (optionInfo.Property.PropertyType == typeof(long))
                    {
                        value = long.Parse(tokens[i]);
                    }
                    else if (optionInfo.Property.PropertyType == typeof(ulong))
                    {
                        value = ulong.Parse(tokens[i]);
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported action option type: {optionInfo.Property.PropertyType}");
                    }

                    optionInfo.Property.SetValue(actionInstance, value);
                }
            }

            var createContextFunction = Utility.GetInheritedStaticMethod(interactionOwner.Type, CommandBase.CreateActionContextName);
            var context = createContextFunction?.Invoke(null, new object[] { _client, component, _services, interactionOwner.Name }) as ActionContext;
            if (context == null)
            {
                throw new ArgumentNullException($"Failed to create action context for {interactionOwner.Name}:{action.Name}");
            }

            await actionInstance.HandleAction(context);
        }

        KeyValuePair<SubCommandInfo, IReadOnlyCollection<SocketSlashCommandDataOption>?>? FindDeepestSubCommand(CommandInfo parent,
            IReadOnlyCollection<SocketSlashCommandDataOption>? options)
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
                    string subCommandName = optionData.Name;
                    SubCommandInfo? info = _subCommandEntries.Find(sub => sub.Name == subCommandName && sub.ParentCommandType == parent.Type);

                    if (info != null)
                    {
                        var result = FindDeepestSubCommand(info, optionData.Options);
                        if (result != null)
                        {
                            return result;
                        }

                        return new(info, optionData.Options);
                    }
                }
            }
            return null;
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
