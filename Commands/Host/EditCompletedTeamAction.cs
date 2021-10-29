using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-edit-team")]
    internal class EditCompletedTeamAction : ActionBase
    {
        public static string ActionName = "pvm-edit-team";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public EditCompletedTeamAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            // Re-grab the message as it may have been modified by a concurrent action.
            var message = await context.Channel.GetMessageAsync(context.Message.Id);
            if (!context.Message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            if (!context.TryMarkMessageForEdit(context.Message.Id))
            {
                await context.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                return;
            }

            var guild = ((SocketGuildChannel)context.Message.Channel).Guild;

            var allIds = new List<ulong>();
            if (context.OriginalSignupsForMessage.ContainsKey(context.Message.Id))
            {
                allIds = context.OriginalSignupsForMessage[context.Message.Id].ToList();
            }
            else
            {
                Logger.LogWarning($"[EditCompletedTeamAction] Failed to find message data for {context.Message.Id}. Cannot retrieve original names." +
                           $"Updating list with currently selected names.");
            }

            int teamIndex = 0;
            HashSet<ulong> allSelectedIds = new();
            List<Embed> teamSelectionEmbeds = new();
            List<Embed> originalEmbeds = new();
            foreach (var currentEmbed in message.Embeds)
            {
                Dictionary<string, ulong> selectedIds = HostHelpers.ParseNamesToRoleIdMap(currentEmbed);

                // Re-build the full id list
                if (!context.OriginalSignupsForMessage.ContainsKey(context.Message.Id))
                {
                    allIds = allIds.Concat(selectedIds.Values).ToList();
                }

                allSelectedIds = allSelectedIds.Concat(selectedIds.Values).ToHashSet();

                Guid historyId = HostHelpers.ParseHistoryIdFromFooter(currentEmbed.Footer.Value.Text);
                teamSelectionEmbeds.Add(HostHelpers.BuildTeamSelectionEmbed(guild, context.Channel.Id, context.Message.Id, historyId, confirmedBefore:true, teamIndex, BossEntry, selectedIds));

                originalEmbeds.Add(
                    Utility.CreateBuilderForEmbed(currentEmbed)
                    .WithFooter($"Being edited by {context.User.Username}")
                    .Build());

                ++teamIndex;
            }

            // If we weren't able to recover the original signups then populate them now with all the people we know about.
            if (!context.OriginalSignupsForMessage.ContainsKey(context.Message.Id))
            {
                context.OriginalSignupsForMessage.TryAdd(context.Message.Id, new ReadOnlyCollection<ulong>(allIds.ToArray()));
            }

            List<ulong> unselectedIds = allIds
                .Where(id => !allSelectedIds.Contains(id))
                .ToList();

            // Update original message to set the "Edited by" footer and disable the buttons.
            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Embeds = originalEmbeds.ToArray();
                msgProps.Components = HostHelpers.BuildFinalTeamComponents(BossIndex, disabled:true);
            });

            // Send a DM with the team selection menu
            var replyMessage = await context.User.SendMessageAsync(
                "Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nPress cancel to discard this edit.",
                embeds: teamSelectionEmbeds.ToArray(),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, allSelectedIds, unselectedIds));

            // TODO: do this via context instead?
            //context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }
    }
}
