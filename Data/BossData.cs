using Discord;

namespace WinstonBot.Data
{
    public class BossData
    {
        public enum Boss
        {
            AoD,
            Raids,
            Count
        }

        public class Entry
        {
            public Boss Id { get; set; }
            public string CommandName { get; set; }
            public string PrettyName { get; set; }
            public string IconUrl { get; set; }
            public bool SupportsSignup { get; set; } = false;
            public uint MaxPlayersOnTeam { get; set; }
            public Color EmbedColor { get; set; }
            public Type? BuilderClass { get; set; }
            public Type? RolesEnumType { get; set; }
        }

        public static readonly Entry[] Entries = new Entry[]
        {
            new Entry()
            {
                Id = Boss.AoD,
                CommandName = "aod",
                PrettyName = "AoD",
                IconUrl = "https://runescape.wiki/images/2/2b/Nex_%28Angel_of_Death%29.png?00050",
                SupportsSignup = true,
                MaxPlayersOnTeam = 7, // TODO: allow optionally passing this in through the command for different team sizes.
                EmbedColor = Color.Red,
                BuilderClass = typeof(Commands.AoDTeamBuilder),
                RolesEnumType = typeof(Services.AoDDatabase.Roles)
            },
            new Entry()
            {
                Id = Boss.Raids,
                CommandName = "raids",
                PrettyName = "Liberation of Mazcab",
                IconUrl = "https://runescape.wiki/images/b/b8/Yakamaru.png?18623",
                MaxPlayersOnTeam = 10,
                EmbedColor = Color.Blue,
            },
        };

        public static bool ValidBossIndex(long index)
        {
            return index >= 0 && index < (long)Boss.Count;
        }

        public static bool ValidBossCommandName(string name)
        {
            return Entries.Where(entry => entry.CommandName == name).Any();
        }
    }
}
