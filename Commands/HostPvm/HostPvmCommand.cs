using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;

namespace WinstonBot.Commands.HostPvm
{
    [Command(
    "host-pvm",
    "Create a new pvm event",
    actions: new Type[] {
        typeof(ChooseRoleAction),
        typeof(CompleteTeamAction),
    })]
    [ScheduableCommand]
    public class HostPvmCommand : CommandBase
    {
        [CommandOption("boss", "The boss to create an event for.", dataProvider: typeof(BossChoiceDataProvider))]
        public long BossIndex { get; set; }

        [CommandOption("message", "An optional message to display.", required: false)]
        public string? Message { get; set; }

        public static readonly RoleDefinition[] Roles = new RoleDefinition[]
        {
            new RoleDefinition(RaidRole.Base, "🛡", "Base"),
            new RoleDefinition(RaidRole.MainStun, "💥", "Main Stun"),
            new RoleDefinition(RaidRole.BackupStun, "⚡", "Backup Stun"),
            new RoleDefinition(RaidRole.Backup, "🇧", "Backup"),
            new RoleDefinition(RaidRole.Shark10, "🦈", "Shark 10"),
            new RoleDefinition(RaidRole.JellyWrangler, "🐡", "Jelly Wrangler"),
            new RoleDefinition(RaidRole.PT13, "1️⃣", "PT 1/3"),
            new RoleDefinition(RaidRole.NorthTank, "🐍", "North Tank"),
            new RoleDefinition(RaidRole.PoisonTank, "🤢", "Poison Tank"),
            new RoleDefinition(RaidRole.PT2, "2️⃣", "PT 2"),
            new RoleDefinition(RaidRole.Double, "🇩", "Double"),
            new RoleDefinition(RaidRole.CPR, "❤️", "CPR"),
            new RoleDefinition(RaidRole.NC, "🐕", "NC"),
            new RoleDefinition(RaidRole.Stun5, "5️⃣", "Stun 5", max:2),
            new RoleDefinition(RaidRole.Stun0, "0️⃣", "Stun 0"),
            new RoleDefinition(RaidRole.DPS, "⚔️", "DPS", 5), // TODO: confirm max
            new RoleDefinition(RaidRole.Fill, "🆓", "Fill", max:10),
            new RoleDefinition(RaidRole.Reserve, "💭", "Reserve", max:10),
        };

        public HostPvmCommand(ILogger logger) : base(logger)
        {
        }

        public async override Task HandleCommand(CommandContext context)
        {
            var entry = BossData.Entries[BossIndex];

            Embed embed;
            MessageComponent component;
            var roles = Helpers.GetRuntimeRoles();
            Helpers.BuildSignup(roles, entry, context.Guild, out embed, out component);

            await context.RespondAsync(text:Message, embed: embed, component: component);
        }
    }
}
