using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-cancel-team-confirmation")]
    internal class CancelTeamConfirmationAction : ActionBase
    {
        public static string ActionName = "pvm-cancel-team-confirmation";

        public string Name => ActionName;

        [ActionParam]
        public long BossIndex { get; set; }

        public CancelTeamConfirmationAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;
            if (context.OriginalMessageData == null || !context.IsMessageDataValid)
            {
                throw new NullReferenceException($"Failed to get message metadat for {context.Message.Id}.");
            }

            var originalMessage = await context.GetOriginalMessage();
            if (originalMessage == null || context.OriginalChannel == null)
            {
                // This can happen if the original message is deleted but the edit window is still open.
                await context.RespondAsync("Failed to find the original message this interaction was created from.", ephemeral: true);
                return;
            }

            var names = HostHelpers.ParseNamesToList(originalMessage.Embeds.First().Description);

            var embed = originalMessage.Embeds.First();
            Dictionary<string, ulong> team = embed.Fields.ToDictionary(ks => ks.Name, vs => Utility.GetUserIdFromMention((string)vs.Value));
            var historyId = context.OriginalMessageData.HistoryId;

            await context.OriginalChannel.ModifyMessageAsync(context.OriginalMessageData.MessageId, msgProps =>
            {
                if (!context.OriginalMessageData.TeamConfirmedBefore)
                {
                    msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                    msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex);
                }
                else
                {
                    msgProps.Embed = HostHelpers.BuildFinalTeamEmbed(context.Guild, context.User.Username, BossData.Entries[BossIndex], team, historyId.Value);
                    msgProps.Components = HostHelpers.BuildFinalTeamComponents(BossIndex, false);
                }
            });

            context.EditFinishedForMessage(context.OriginalMessageData.MessageId);

            // Delete the edit team message from the DM
            await context.Message.DeleteAsync();

            // Ack the interaction so they don't see "interaction failed" after hitting complete team.
            await context.DeferAsync();
        }
    }
}
