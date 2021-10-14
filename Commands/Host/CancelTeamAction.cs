﻿using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Action("pvm-cancel-team-confirmation")]
    internal class CancelTeamConfirmationAction : IAction
    {
        public static string ActionName = "pvm-cancel-team-confirmation";
        public string Name => ActionName;

        [ActionParam]
        public long BossIndex { get; set; }

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

            var names = HostHelpers.ParseNamesToList(originalMessage.Embeds.First().Description);

            await context.OriginalChannel.ModifyMessageAsync(context.OriginalMessageData.MessageId, msgProps =>
            {
                if (!context.OriginalMessageData.TeamConfirmedBefore)
                {
                    msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                    msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex);
                }
                else
                {
                    // Don't need to change the embed since it hasn't been modified yet.
                    msgProps.Embed = Utility.CreateBuilderForEmbed(originalMessage.Embeds.First())
                        .WithFooter(new Discord.EmbedFooterBuilder())
                        .Build();
                    msgProps.Components = HostHelpers.BuildEditButton(BossIndex, false);
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
