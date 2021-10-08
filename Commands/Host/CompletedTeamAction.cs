using Discord;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Action("pvm-complete-team")]
    internal class CompleteTeamAction : IAction
    {
        public static string ActionName = "pvm-complete-team";
        public string Name => ActionName;

        [ActionParam]
        public long BossIndex { get; set; }

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            if (!context.Message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var currentEmbed = context.Message.Embeds.First();
            var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
            if (names.Count == 0)
            {
                await context.RespondAsync("Not enough people signed up.", ephemeral: true);
                return;
            }

            if (!context.TryMarkMessageForEdit(context.Message.Id, HostHelpers.ParseNamesToIdList(names)))
            {
                await context.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                return;
            }

            // TODO: calculate who should go.
            List<string> selectedNames = new();
            List<string> unselectedNames = new();
            int i = 0;
            foreach (var mention in names)
            {
                if (i++ < context.BossEntry.MaxPlayersOnTeam) selectedNames.Add(mention);
                else unselectedNames.Add(mention);
            }

            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Components = HostHelpers.BuildSignupButtons(context.BossIndex, true);
                // footers can't show mentions, so use the username.
                msgProps.Embed = HostHelpers.BuildSignupEmbed(context.BossIndex, names, context.User.Username);
            });

            // Footed will say "finalized by X" if it's been completed before.
            bool hasBeenConfirmedBefore = currentEmbed.Footer.HasValue;
            var guild = context.Channel.Guild;

            await context.User.SendMessageAsync("Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                "\nOnce you're finished making changes you can dismiss this message.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild.Id, context.Channel.Id, context.Message.Id, hasBeenConfirmedBefore, context.BossEntry, selectedNames),
                component: HostHelpers.BuildTeamSelectionComponent(guild, context.BossIndex, selectedNames, unselectedNames));

            await context.DeferAsync();
        }
    }
}
