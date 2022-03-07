using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Action("pvm-quit-signup")]
    internal class QuitAction : ActionBase
    {
        public static string ActionName = "pvm-quit-signup";

        [ActionParam]
        public long BossIndex { get; set; }

        public QuitAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            // Re-grab the message as it may have been modified by a concurrent action.
            var message = await context.Channel.GetMessageAsync(context.Message.Id);
            if (!message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var currentEmbed = message.Embeds.First();
            var names = HostHelpers.ParseMentionsStringToMentionList(currentEmbed.Description);
            var ids = HostHelpers.ParseMentionListToIdList(names);
            if (!ids.Contains(context.User.Id))
            {
                Logger.LogDebug($"{context.User.Mention} {context.Message.Id} isn't signed up: ignoring.");
                await context.RespondAsync("You're not signed up.", ephemeral: true);
                return;
            }

            Logger.LogInformation($"{context.User.Mention} has quit {context.Message.Id}!");
            var index = ids.IndexOf(context.User.Id);
            names.RemoveAt(index);

            context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex, HostHelpers.CalculateNumTeams(BossIndex, names.Count));
            }).Wait();
        }
    }
}
