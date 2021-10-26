using Discord;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    internal class WatchCatActionDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (var action in Enum.GetValues(typeof(WatchCatDB.UserAction)))
            {
                builder.AddChoice(action.ToString(), (int)action);
            }
        }
    }
}
