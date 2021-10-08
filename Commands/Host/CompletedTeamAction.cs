﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-complete-team")]
    internal class CompleteTeamAction : IAction
    {
        public static string ActionName = "pvm-complete-team";

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
            
            var guild = ((SocketGuildChannel)context.Channel).Guild;

            var currentEmbed = context.Message.Embeds.First();
            var ids = HostHelpers.ParseNamesToIdListWithValidation(guild, currentEmbed.Description);
            if (ids.Count == 0)
            {
                await context.RespondAsync("Not enough people signed up.", ephemeral: true);
                return;
            }

            if (!context.TryMarkMessageForEdit(context.Message.Id, ids))
            {
                await context.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                return;
            }

            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            // TODO: calculate who should go.
            List<string> selectedNames = new();
            List<string> unselectedNames = new();
            int i = 0;
            foreach (var mention in names)
            {
                if (i++ < BossEntry.MaxPlayersOnTeam) selectedNames.Add(mention);
                else unselectedNames.Add(mention);
            }

            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex, true);
                // footers can't show mentions, so use the username.
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names, context.User.Username);
            });

            // Footed will say "finalized by X" if it's been completed before.
            bool hasBeenConfirmedBefore = currentEmbed.Footer.HasValue;

            var message = await context.User.SendMessageAsync("Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                "\nOnce you're finished making changes you can dismiss this message.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild.Id, context.Channel.Id, context.Message.Id, hasBeenConfirmedBefore, BossEntry, selectedNames),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, selectedNames, unselectedNames));

            // TODO: do this via context instead?
            context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }
    }
}
