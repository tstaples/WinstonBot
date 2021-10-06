using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (info.BuildCommandMethod != null)
            {
                Console.WriteLine($"Using custom BuildCommand for {info.Name}");
                return (SlashCommandOptionBuilder)info.BuildCommandMethod.Invoke(null, null);
            }

            var subCommands = CommandHandler.SubCommandEntries.Where(sub => sub.ParentCommandType == info.Type);
            var type = subCommands.Any() ? ApplicationCommandOptionType.SubCommandGroup : ApplicationCommandOptionType.SubCommand;

            var builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithRequired(true)
                .WithType(type);
            return builder;
        }

        public static SlashCommandOptionBuilder BuildSlashCommandOption(CommandOptionInfo info)
        {
            var builder = new SlashCommandOptionBuilder()
                .WithName(info.Name)
                .WithDescription(info.Description)
                .WithRequired(true)
                .WithType(GetOptionForType(info.Type));
            return builder;
        }

        public static SlashCommandBuilder BuildSlashCommand(CommandInfo info)
        {
            if (info.BuildCommandMethod != null)
            {
                Console.WriteLine($"Using custom BuildCommand for {info.Name}");
                return (SlashCommandBuilder)info.BuildCommandMethod.Invoke(null, null);
            }

            var builder = new SlashCommandBuilder()
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
