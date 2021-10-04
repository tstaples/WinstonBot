namespace WinstonBot.Commands
{
    internal class CancelTeamConfirmationAction : IAction
    {
        public static string ActionName = "pvm-cancel-team-confirmation";
        public string Name => ActionName;
        public long RoleId => throw new NotImplementedException();

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;
            if (context.OriginalMessageData == null || !context.IsMessageDataValid)
            {
                throw new NullReferenceException($"Failed to get message metadat for {context.Component.Message.Id}.");
            }

            var originalMessage = await context.GetOriginalMessage();
            if (originalMessage == null || context.Channel == null)
            {
                // This can happen if the original message is deleted but the edit window is still open.
                await context.Component.RespondAsync("Failed to find the original message this interaction was created from.", ephemeral: true);
                return;
            }

            var names = HostHelpers.ParseNamesToList(originalMessage.Embeds.First().Description);

            await context.Channel.ModifyMessageAsync(context.OriginalMessageData.MessageId, msgProps =>
            {
                if (!context.OriginalMessageData.TeamConfirmedBefore)
                {
                    msgProps.Content = $"Sign up for {context.BossEntry.PrettyName}";
                    msgProps.Embed = HostHelpers.BuildSignupEmbed(context.BossIndex, names);
                    msgProps.Components = HostHelpers.BuildSignupButtons(context.BossIndex);
                }
                else
                {
                    // Don't need to change the embed since it hasn't been modified yet.
                    msgProps.Content = $"Final team for {context.BossEntry.PrettyName}";
                    msgProps.Components = HostHelpers.BuildEditButton(context.BossIndex, false);
                }
            });

            context.EditFinishedForMessage(context.OriginalMessageData.MessageId);

            // Delete the edit team message from the DM
            await context.Component.Message.DeleteAsync();

            // Ack the interaction so they don't see "interaction failed" after hitting complete team.
            await context.Component.DeferAsync();
        }
    }
}
