using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    [Command(
    "host-pvm",
    "Create a new pvm event",
    actions: new Type[] {
    })]
    [ScheduableCommand]
    [ConfigurableCommand]
    public class HostPvm : CommandBase
    {
        [CommandOption("boss", "The boss to create an event for.", dataProvider: typeof(BossChoiceDataProvider))]
        public long BossIndex { get; set; }

        [CommandOption("message", "An optional message to display.", required: false)]
        public string? Message { get; set; }

        public enum RaidRole
        {
            Base,
            MainStun,
            BackupStun,
            Backup,
            Shark10,
            JellyWrangler,
            PT13,
            NorthTank,
            PoisonTank,
            PT2,
            Double,
            CPR,
            NC,
            Stun5,
            Stun0,
            DPS,
            Fill,
            Reserve,
            None,
        }

        private class Role
        {
            public RaidRole RoleType { get; set; }
            public string Emoji { get; set; }
            public string Name { get; set; }
            public int MaxPlayers { get; set; }

            public Role(RaidRole role, string emoji, string name, int max = 1)
            {
                RoleType = role;
                Emoji = emoji;
                Name = name;
                MaxPlayers = max;
            }
        }

        private static readonly Role[] Roles = new Role[]
        {
            new Role(RaidRole.Base, "🛡", "Base"),
            new Role(RaidRole.MainStun, "💥", "Main Stun"),
            new Role(RaidRole.BackupStun, "⚡", "Backup Stun"),
            new Role(RaidRole.Backup, "🇧", "Backup"),
            new Role(RaidRole.Shark10, "🦈", "Shark 10"),
            new Role(RaidRole.JellyWrangler, "🐡", "Jelly Wrangler"),
            new Role(RaidRole.PT13, "1️⃣", "PT 1/3"),
            new Role(RaidRole.NorthTank, "🐍", "North Tank"),
            new Role(RaidRole.PoisonTank, "🤢", "Poison Tank"),
            new Role(RaidRole.PT2, "2️⃣", "PT 2"),
            new Role(RaidRole.Double, "🇩", "Double"),
            new Role(RaidRole.CPR, "❤️", "CPR"),
            new Role(RaidRole.NC, "🐕", "NC"),
            new Role(RaidRole.Stun5, "5️⃣", "Stun 5", max:2),
            new Role(RaidRole.Stun0, "0️⃣", "Stun 0"),
            new Role(RaidRole.DPS, "⚔️", "DPS", 5), // TODO: confirm max
            new Role(RaidRole.Fill, "🆓", "Fill", max:10),
            new Role(RaidRole.Reserve, "💭", "Reserve", max:10),
        };

        public HostPvm(ILogger logger) : base(logger)
        {
        }

        public async override Task HandleCommand(CommandContext context)
        {
            var entry = BossData.Entries[BossIndex];

            var builder = new EmbedBuilder()
                .WithTitle(entry.PrettyName)
                .WithDescription(Message);

            var componentBuilder = new ComponentBuilder();
            foreach (Role role in Roles)
            {
                builder.AddField($"{role.Emoji} {role.Name}", "Empty", inline: true);

                componentBuilder.WithButton(new ButtonBuilder()
                    .WithEmote(new Emoji(role.Emoji))
                    .WithStyle(ButtonStyle.Secondary)
                    .WithCustomId($"pvm-event_{BossIndex}_{(int)role.RoleType}"));
            }

            componentBuilder.WithButton(emote: new Emoji("✅"), customId: "pvm-event-complete", style: ButtonStyle.Success);

            await context.RespondAsync(embed: builder.Build(), component: componentBuilder.Build());
        }
    }
}
