using Discord;

namespace WinstonBot.Commands
{
    public class ActionDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            // TODO: we'll probably want a way to restrict these to the actions for a specific command
            foreach (var entry in CommandHandler.ActionEntries.Values)
            {
                builder.AddChoice(entry.Name, entry.Name);
            }
        }
    }
}
