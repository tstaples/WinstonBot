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
        typeof(EditTeamAction),
        typeof(ListTeamAction)
    })]
    [ScheduableCommand]
    public class HostPvmCommand : CommandBase
    {
        [CommandOption("boss", "The boss to create an event for.", dataProvider: typeof(BossChoiceDataProvider))]
        public long BossIndex { get; set; }

        [CommandOption("message", "An optional message to display.", required: false)]
        public string? Message { get; set; }

        public HostPvmCommand(ILogger logger) : base(logger)
        {
        }

        public async override Task HandleCommand(CommandContext context)
        {
            var entry = BossData.Entries[BossIndex];

            DateTimeOffset? displayTimestamp = null;
            if (context is Services.CommandScheduler.ScheduledCommandContext scheduleContext &&
                scheduleContext.DisplayTimestamp != null)
            {
                displayTimestamp = scheduleContext.DisplayTimestamp;
            }

            Embed embed;
            MessageComponent component;
            var roles = Helpers.GetRuntimeRoles();
            Helpers.BuildSignup(roles, entry, context.Guild, displayTimestamp, out embed, out component);

            // TODO: figure out why this doesn't ping.
            await context.RespondAsync(text: Message, embed: embed, component: component, allowedMentions: AllowedMentions.All);
        }
    }
}
