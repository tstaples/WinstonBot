using Discord;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
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
