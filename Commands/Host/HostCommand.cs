using Discord;
using WinstonBot.Data;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using WinstonBot.Attributes;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    [Command(
        "host-pvm-signup",
        "Create a signup for a pvm event",
        actions: new Type[] {
            typeof(SignupAction),
            typeof(QuitAction),
            typeof(CompleteTeamAction),
            typeof(ConfirmTeamAction),
            typeof(CancelTeamConfirmationAction),
            typeof(EditCompletedTeamAction),
            typeof(AddUserToTeamAction),
            typeof(RemoveUserFromTeamAction),
            typeof(RevertToSignupAction)
        }
    )]
    [ScheduableCommand]
    [ConfigurableCommand]
    public class HostPvmSignup : CommandBase
    {
        [CommandOption("boss", "The boss to create an event for.", dataProvider: typeof(SignupBossChoiceDataProvider))]
        public long BossIndex { get; set; }

        [CommandOption("message", "An optional message to display.", required: false)]
        public string? Message { get; set; }

        // we only have the mention string in the desc.
#if DEBUG
        private static readonly List<string> testNames = new List<string>()
        {
            { "<@141439679890325504>" },
            { "<@517886402466152450>" },
            { "<@115935510681223172>" },
            { "<@197872300802965504>" },
            { "<@172497655992156160>" },
            { "<@154154510145683456>" },
            { "<@668161362249121796>" },
            { "<@356622410750885888>" },
            { "<@737450291376685120>" },
            { "<@263004003816833024>" },
            { "<@121017588401700864>" },
            { "<@131255563970543616>" },

            //{ "<@141439679890325504>" },
            //{ "<@115935510681223172>" },
            //{ "<@356622410750885888>" },
            //{ "<@414119139506913280>" },
            //{ "<@295027430299795456>" },
            //{ "<@197872300802965504>" },
            //{ "<@517886402466152450>" },
            //{ "<@121017588401700864>" },
            //{ "<@668161362249121796>" },
            //{ "<@154154510145683456>" },
            //{ "<@737450291376685120>" },
            //{ "<@269966416348839936>" },
            //{ "<@146798592047316992>" },
            //{ "<@138827043482763264>" },
            //{ "<@159691258804174849>" },
            //{ "<@227268711780843520>" },
            //{ "<@204793753691619330>" },
            //{ "<@263004003816833024>" },


            //{ "<@168896466746736640>" },
            //{ "<@517886402466152450>" },
            //{ "<@141439679890325504>" },
            //{ "<@295027430299795456>" },
            //{ "<@668161362249121796>" },
            //{ "<@197872300802965504>" },
            //{ "<@159691258804174849>" },
            //{ "<@172497655992156160>" },
            //{ "<@414119139506913280>" },
            //{ "<@889961722314637342>" },
            //{ "<@746368167617495151>" },
            //{ "<@356622410750885888>" },
            //{ "<@168896466746736640>" },
        };
#endif

        public HostPvmSignup(ILogger logger) : base(logger) { }

        public async override Task HandleCommand(CommandContext context)
        {
            if (!BossData.ValidBossIndex(BossIndex))
            {
                await context.RespondAsync($"Invalid boss index {BossIndex}. Max Index is {(long)BossData.Boss.Count - 1}", ephemeral: true);
                return;
            }

            var bossPrettyName = BossData.Entries[BossIndex].PrettyName;
            string message = Message ?? $"Sign up for {bossPrettyName}"; // default message

#if DEBUG
            var embed = HostHelpers.BuildSignupEmbed(BossIndex, testNames);
            var buttons = HostHelpers.BuildSignupButtons(BossIndex, HostHelpers.CalculateNumTeams(BossIndex, testNames.Count));
#else
            var embed = HostHelpers.BuildSignupEmbed(BossIndex, new List<string>());
            var buttons = HostHelpers.BuildSignupButtons(BossIndex, 1);
#endif

            await context.RespondAsync(message, embed: embed, component: buttons, allowedMentions: AllowedMentions.All);
        }

        public static new ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services, string owningCommand)
        {
            return new HostActionContext(client, arg, services, owningCommand);
        }
    }

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
            public RaidRole RoleType {  get; set; }
            public string Emoji { get; set; }
            public string Name {  get; set; }
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
                builder.AddField($"{role.Emoji} {role.Name}", "Empty", inline:true);
                componentBuilder.WithButton(new ButtonBuilder()
                    .WithEmote(new Emoji(role.Emoji))
                    .WithStyle(ButtonStyle.Secondary)
                    .WithCustomId($"pvm-event_{BossIndex}_{(int)role.RoleType}"));
            }

            componentBuilder.WithButton(emote: new Emoji("✅"), customId:"pvm-event-complete", style:ButtonStyle.Success);

            await context.RespondAsync(embed: builder.Build(), component: componentBuilder.Build());
        }
    }
}
