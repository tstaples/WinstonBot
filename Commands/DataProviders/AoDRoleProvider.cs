using Discord;

namespace WinstonBot.Commands
{
    internal class AoDRoleProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (var role in Enum.GetValues(typeof(Services.AoDDatabase.Roles)))
            {
                builder.AddChoice(role.ToString(), (int)role);
            }
        }
    }
}
