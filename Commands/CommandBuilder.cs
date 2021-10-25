using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    public class CommandBuilder
    {
        public static ApplicationCommandOptionType GetOptionForType(Type type)
        {
            Dictionary<Type, ApplicationCommandOptionType> types = new()
            {
                { typeof(string), ApplicationCommandOptionType.String },
                { typeof(int), ApplicationCommandOptionType.Integer },
                { typeof(long), ApplicationCommandOptionType.Integer },
                { typeof(bool), ApplicationCommandOptionType.Boolean },
                { typeof(SocketGuildUser), ApplicationCommandOptionType.User },
                { typeof(SocketGuildChannel), ApplicationCommandOptionType.Channel },
                { typeof(SocketRole), ApplicationCommandOptionType.Role },
                { typeof(double), ApplicationCommandOptionType.Number },
                { typeof(float), ApplicationCommandOptionType.Number },
            };

            return types[type];
        }

        public static SlashCommandOptionBuilder BuildSlashCommandOption(SubCommandInfo info, ILogger logger)
        {
            var buildFunc = Utility.GetInheritedStaticMethod(info.Type, CommandBase.BuildCommandOptionName);
            if (buildFunc == null)
                throw new ArgumentNullException("Expected valid builder function. Ensure your command inherits from CommandBase.");

            SlashCommandOptionBuilder? builder = buildFunc.Invoke(null, new object[] {logger}) as SlashCommandOptionBuilder;
            if (builder != null)
            {
                logger.LogDebug($"Using custom BuildCommand for {info.Name}");
                return builder;
            }

            var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
            var type = subCommands.Any() || info.HasDynamicSubCommands
                ? ApplicationCommandOptionType.SubCommandGroup
                : ApplicationCommandOptionType.SubCommand;

            builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithType(type);

            foreach (SubCommandInfo subInfo in subCommands)
            {
                builder.AddOption(BuildSlashCommandOption(subInfo, logger));
            }

            foreach (CommandOptionInfo optionInfo in info.Options)
            {
                builder.AddOption(BuildSlashCommandOption(optionInfo, logger));
            }
            return builder;
        }

        public static SlashCommandOptionBuilder BuildSlashCommandOption(CommandOptionInfo info, ILogger logger)
        {
            var builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithRequired(info.Required)
                .WithType(GetOptionForType(info.Type));

            if (info.ChoiceProviderType != null)
            {
                logger.LogDebug($"Invoking choice provider: {info.ChoiceProviderType.Name} for option {info.Name}, type: {info.Type}");
                try
                {
                    info.ChoiceProviderType.GetMethod("PopulateChoices").Invoke(null, new object[] { builder });
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error invoking choice provider: {ex.Message}");
                }
            }

            return builder;
        }

        public static SlashCommandBuilder BuildSlashCommand(CommandInfo info, ILogger logger)
        {
            var builder = new SlashCommandBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithDefaultPermission(info.DefaultPermission == DefaultPermission.Everyone);

            var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
            foreach (SubCommandInfo subCommandInfo in subCommands)
            {
                builder.AddOption(BuildSlashCommandOption(subCommandInfo, logger));
            }

            foreach (CommandOptionInfo optionInfo in info.Options)
            {
                builder.AddOption(BuildSlashCommandOption(optionInfo, logger));
            }

            var buildFunc = Utility.GetInheritedStaticMethod(info.Type, CommandBase.BuildCommandName);
            if (buildFunc == null)
                throw new ArgumentNullException("Expected valid builder function. Ensure your command inherits from CommandBase.");

            SlashCommandBuilder? customBuilder = buildFunc.Invoke(null, new object[] { builder, logger }) as SlashCommandBuilder;
            if (customBuilder != null)
            {
                logger.LogDebug($"Using custom BuildCommand for {info.Name}");
                return customBuilder;
            }

            return builder;
        }
    }
}
