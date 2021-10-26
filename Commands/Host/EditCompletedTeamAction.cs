using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-edit-team")]
    internal class EditCompletedTeamAction : IAction
    {
        public static string ActionName = "pvm-edit-team";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public async Task HandleAction(ActionContext actionContext)
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

            var currentEmbed = message.Embeds.First();
            Dictionary<string, ulong> selectedIds = HostHelpers.ParseNamesToRoleIdMap(currentEmbed);
            if (selectedIds.Count == 0)
            {
                await context.RespondAsync("Not enough people signed up.", ephemeral: true);
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
                Console.WriteLine($"[EditCompletedTeamAction] Failed to find message data for {context.Message.Id}. Cannot retrieve original names." +
                    $"Updating list with currently selected names.");

                context.OriginalSignupsForMessage.TryAdd(context.Message.Id, new ReadOnlyCollection<ulong>(selectedIds.Values.ToArray()));
                allIds = selectedIds.Values.ToList();
            }

            List<ulong> unselectedIds = allIds
                .Where(id => !selectedIds.ContainsValue(id))
                .ToList();

            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Embed = Utility.CreateBuilderForEmbed(currentEmbed)
                .WithFooter($"Being edited by {context.User.Username}")
                .Build();
                msgProps.Components = new ComponentBuilder()
                    .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{BossIndex}", ButtonStyle.Danger, disabled: true)
                    .Build();
            });

            var replyMessage = await context.User.SendMessageAsync(
                "Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nPress cancel to discard this edit.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild, context.Channel.Id, context.Message.Id, true, BossEntry, selectedIds),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, selectedIds, unselectedIds));

            // TODO: make this general for any boss signup
            var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
            aodDb.RemoveLastRowFromHistory();

            // TODO: do this via context instead?
            //context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }
    }
}
