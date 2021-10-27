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
            { "<@517886402466152450>" },
            { "<@141439679890325504>" },
            { "<@295027430299795456>" },
            { "<@668161362249121796>" },
            { "<@197872300802965504>" },
            { "<@159691258804174849>" },
            { "<@172497655992156160>" },
            { "<@414119139506913280>" },
            { "<@889961722314637342>" },
            { "<@746368167617495151>" },
            { "<@356622410750885888>" },
            { "<@168896466746736640>" },
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

            var buttons = HostHelpers.BuildSignupButtons(BossIndex);
#if DEBUG
            var embed = HostHelpers.BuildSignupEmbed(BossIndex, testNames);
#else
            var embed = HostHelpers.BuildSignupEmbed(BossIndex, new List<string>());
#endif

            await context.RespondAsync(message, embed: embed, component: buttons, allowedMentions: AllowedMentions.All);
        }

        public static new ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services, string owningCommand)
        {
            return new HostActionContext(client, arg, services, owningCommand);
        }
    }
}
