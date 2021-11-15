using Discord;
using System.Reflection;
using WinstonBot.Attributes;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    public class CommandNameDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (CommandInfo command in CommandHandler.CommandEntries.Values)
            {
                if (command.Type.GetCustomAttribute<ConfigurableCommandAttribute>() != null)
                {
                    builder.AddChoice(command.Name, command.Name);
                }
            }
        }
    }

    public class CommandsWithActionsDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (CommandInfo command in CommandHandler.CommandEntries.Values)
            {
                if (command.Type.GetCustomAttribute<ConfigurableCommandAttribute>() != null &&
                    command.Actions != null)
                {
                    builder.AddChoice(command.Name, command.Name);
                }
            }
        }
    }
}
