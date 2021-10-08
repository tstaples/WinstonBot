using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            };

            return types[type];
        }

        public static SlashCommandOptionBuilder BuildSlashCommandOption(SubCommandInfo info)
        {
            var buildFunc = Utility.GetInheritedStaticMethod(info.Type, CommandBase.BuildCommandOptionName);
            SlashCommandOptionBuilder builder = buildFunc.Invoke(null, null) as SlashCommandOptionBuilder;
            if (builder != null)
            {
                Console.WriteLine($"Using custom BuildCommand for {info.Name}");
                return builder;
            }

            var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
            var type = subCommands.Any() ? ApplicationCommandOptionType.SubCommandGroup : ApplicationCommandOptionType.SubCommand;

            builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithType(type);

            foreach (SubCommandInfo subInfo in subCommands)
            {
                builder.AddOption(BuildSlashCommandOption(subInfo));
            }

            foreach (CommandOptionInfo optionInfo in info.Options)
            {
                builder.AddOption(BuildSlashCommandOption(optionInfo));
            }
            return builder;
        }

        public static SlashCommandOptionBuilder BuildSlashCommandOption(CommandOptionInfo info)
        {
            var builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithRequired(info.Required)
                .WithType(GetOptionForType(info.Type));

            if (info.ChoiceProviderType != null)
            {
                Console.WriteLine($"Invoking choice provider: {info.ChoiceProviderType.Name} for option {info.Name}");
                try
                {
                    // TODO: how can we pass in contextual information when it depends on runtime value?
                    // I don't think that's something we had before either though so probably just don't support it?
                    info.ChoiceProviderType.GetMethod("PopulateChoices").Invoke(null, new object[] { builder });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error invoking choice provider: {ex.Message}");
                }
            }

            return builder;
        }

        public static SlashCommandBuilder BuildSlashCommand(CommandInfo info)
        {
            var buildFunc = Utility.GetInheritedStaticMethod(info.Type, CommandBase.BuildCommandName);
            SlashCommandBuilder builder = buildFunc.Invoke(null, null) as SlashCommandBuilder;
            if (builder != null)
            {
                Console.WriteLine($"Using custom BuildCommand for {info.Name}");
                return builder;
            }

            builder = new SlashCommandBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithDefaultPermission(info.DefaultPermission == DefaultPermission.Everyone);

            var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
            foreach (SubCommandInfo subCommandInfo in subCommands)
            {
                builder.AddOption(BuildSlashCommandOption(subCommandInfo));
            }

            foreach (CommandOptionInfo optionInfo in info.Options)
            {
                builder.AddOption(BuildSlashCommandOption(optionInfo));
            }

            return builder;
        }
    }
}
