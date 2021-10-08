using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
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

            var currentEmbed = context.Message.Embeds.First();
            var selectedNameIds = HostHelpers.ParseNamesToIdList(currentEmbed.Description);
            if (selectedNameIds.Count == 0)
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
                Console.WriteLine($"[EditCompletedTeamAction] Failed to find message data for {context.Message.Id}. Cannot retrieve original names.");
            }

            List<ulong> unselectedIds = allIds
                .Where(id => !selectedNameIds.Contains(id))
                .ToList();

            var selectedNames = Utility.ConvertUserIdListToMentions(guild, selectedNameIds);
            var unselectedNames = Utility.ConvertUserIdListToMentions(guild, unselectedIds);

            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Embed = Utility.CreateBuilderForEmbed(currentEmbed)
                .WithFooter($"Being edited by {context.User.Username}")
                .Build();
                msgProps.Components = new ComponentBuilder()
                    .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{BossIndex}", ButtonStyle.Danger, disabled: true)
                    .Build();
            });

            var message = await context.User.SendMessageAsync("Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                "\nOnce you're finished making changes you can dismiss this message.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild.Id, context.Channel.Id, context.Message.Id, true, BossEntry, selectedNames),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, selectedNames, unselectedNames));

            // TODO: do this via context instead?
            context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }
    }
}
