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
                if (!entry.SupportsSignup)
                {
                    builder.AddChoice(entry.CommandName, (int)entry.Id);
                }
            }
        }
    }

    public class SignupBossChoiceDataProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (var entry in BossData.Entries)
            {
                if (entry.SupportsSignup)
                {
                    builder.AddChoice(entry.CommandName, (int)entry.Id);
                }
            }
        }
    }
}
