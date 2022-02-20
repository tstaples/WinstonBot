using Discord;
using WinstonBot.Attributes;
using System.Reflection;

namespace WinstonBot.Commands
{
    public class ActionDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            HashSet<ActionInfo> actions = new();
            foreach (var command in CommandHandler.CommandEntries.Values)
            {
                if (command.Type.GetCustomAttribute<ConfigurableCommandAttribute>() != null &&
                    command.Actions != null)
                {
                    actions = actions.Concat(command.Actions.Values).ToHashSet();
                }
            }
            // TODO: we'll probably want a way to restrict these to the actions for a specific command
            foreach (var entry in actions)
            {
                builder.AddChoice(entry.Name, entry.Name);
            }
        }
    }
}
