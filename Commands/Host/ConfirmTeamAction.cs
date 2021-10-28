using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-confirm-team")]
    internal class ConfirmTeamAction : ActionBase
    {
        public static string ActionName = "pvm-confirm-team";

        [ActionParam]
        public long BossIndex { get; set; }

        // TODO: have buttons for 1,2 etc teams.
        public long NumberOfTeams { get; set; } = 1;

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public ConfirmTeamAction(ILogger logger) : base(logger)
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

            var currentEmbed = context.Message.Embeds.First();
            Dictionary<string, ulong> selectedIds = HostHelpers.ParseNamesToRoleIdMap(currentEmbed);

            // TODO: make this general for any boss signup
            var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
            var historyId = aodDb.AddTeamToHistory(selectedIds);

            // TODO: ping the people that are going.
            // Should that be a separate message or should we just not use an embed for this?
            await context.OriginalChannel.ModifyMessageAsync(context.OriginalMessageData.MessageId, msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildFinalTeamEmbed(context.Guild, context.User.Username, BossEntry, selectedIds, historyId);
                msgProps.Components = HostHelpers.BuildFinalTeamComponents(BossIndex);
            });

            context.EditFinishedForMessage(context.OriginalMessageData.MessageId);

            // Delete the edit team message from the DM
            await context.Message.DeleteAsync();

            // Even though this is a DM, make it ephemeral so they can dismiss it as they can't delete the messages in DM.
            await context.RespondAsync("Team updated in original message.", ephemeral: true);
        }
    }
}
