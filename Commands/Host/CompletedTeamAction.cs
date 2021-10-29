using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;
using WinstonBot.Helpers;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    [Action("pvm-complete-team")]
    internal class CompleteTeamAction : ActionBase
    {
        public static string ActionName = "pvm-complete-team";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public CompleteTeamAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            // Re-grab the message as it may have been modified by a concurrent action.
            var message = await context.Channel.GetMessageAsync(context.Message.Id);
            if (!message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }
            
            var guild = ((SocketGuildChannel)context.Channel).Guild;

            var currentEmbed = message.Embeds.First();
            var ids = HostHelpers.ParseNamesToIdListWithValidation(guild, currentEmbed.Description);
            if (ids.Count == 0)
            {
                await context.RespondAsync("Not enough people signed up.", ephemeral: true);
                return;
            }

            if (!context.TryMarkMessageForEdit(message.Id, ids))
            {
                await context.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                return;
            }

            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            ITeamBuilder builder = HostHelpers.GetTeamBuilder(context.ServiceProvider, BossEntry);
            Dictionary<string, ulong> roleUserMap = builder.SelectTeam(ids);
            var unselectedids = ids.Where(id => !roleUserMap.ContainsValue(id));

            context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex, true);
                // footers can't show mentions, so use the username.
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names, context.User.Username);
            }).Wait();

            // Footed will say "finalized by X" if it's been completed before.
            bool hasBeenConfirmedBefore = currentEmbed.Footer.HasValue &&
                HostHelpers.ParseHistoryIdFromFooter(currentEmbed.Footer.Value.Text) != Guid.Empty;

            var replyMessage = await context.User.SendMessageAsync(
                "Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nPress cancel to discard this edit.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild, context.Channel.Id, context.Message.Id, null, hasBeenConfirmedBefore, BossEntry, roleUserMap),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, roleUserMap, unselectedids));

            // TODO: do this via context instead?
            //context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }
    }
}
