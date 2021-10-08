﻿using Discord;
using WinstonBot.Attributes;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    [Action("pvm-confirm-team")]
    internal class ConfirmTeamAction : IAction
    {
        public static string ActionName = "pvm-confirm-team";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public async Task HandleAction(ActionContext actionContext)
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
            var selectedNames = HostHelpers.ParseNamesToList(currentEmbed.Description);

            // TODO: ping the people that are going.
            // Should that be a separate message or should we just not use an embed for this?
            await context.OriginalChannel.ModifyMessageAsync(context.OriginalMessageData.MessageId, msgProps =>
            {
                msgProps.Embed = new EmbedBuilder()
                    .WithTitle($"Selected Team for {BossEntry.PrettyName}")
                    .WithFooter($"Finalized by {context.User.Username}")
                    .WithDescription(String.Join(Environment.NewLine, selectedNames))
                    .WithThumbnailUrl(BossEntry.IconUrl)
                    .Build();
                msgProps.Components = new ComponentBuilder()
                    .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{BossIndex}", ButtonStyle.Danger)
                    .Build();
            });

            var builder = ComponentBuilder.FromComponents(context.Message.Components);

            context.EditFinishedForMessage(context.OriginalMessageData.MessageId);

            // Delete the edit team message from the DM
            await context.Message.DeleteAsync();

            // Even though this is a DM, make it ephemeral so they can dismiss it as they can't delete the messages in DM.
            await context.RespondAsync("Team updated in original message.", ephemeral: true);
        }
    }
}
