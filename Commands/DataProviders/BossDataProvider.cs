using Discord;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    // TODO: if we only support signups for certain bosses then add a version of this that supports that.
    public class BossChoiceDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (var entry in BossData.Entries)
            {
                builder.AddChoice(entry.CommandName, (int)entry.Id);
            }
        }
    }
}
