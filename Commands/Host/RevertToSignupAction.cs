using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using WinstonBot.Attributes;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-revert-to-signup")]
    internal class RevertToSignupAction : ActionBase
    {
        public static string ActionName = "pvm-revert-to-signup";

        [ActionParam]
        public long BossIndex { get; set; }

        public RevertToSignupAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            // Get all the ids that signed up. If we don't have them stored then read them from the embed.
            ReadOnlyCollection<ulong> ids;
            if (!context.OriginalSignupsForMessage.TryGetValue(context.Message.Id, out ids))
            {
                await context.Channel.SendMessageAsync($"{context.User.Mention} Couldn't retrieve original signups for {context.Message.Id}: just using who was selected.");

                List<ulong> allIds = new();
                foreach (var currentEmbed in context.Message.Embeds)
                {
                    allIds = allIds.Concat(HostHelpers.ParseNamesToRoleIdMap(currentEmbed).Values).ToList();
                }

                // Remove any invalid ids as they won't be included in the signup
                allIds.RemoveAll(id => id == 0);
                ids = new ReadOnlyCollection<ulong>(allIds);
            }

            // Remove the teams from the history
            foreach (var currentEmbed in context.Message.Embeds)
            {
                if (!currentEmbed.Footer.HasValue)
                {
                    throw new ArgumentNullException($"Expected valid footer for in message");
                }

                Guid historyId = HostHelpers.ParseHistoryIdFromFooter(currentEmbed.Footer.Value.Text);

                // TODO: make this general for any boss signup
                var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
                aodDb.RemoveRowFromHistory(historyId);
            }

            var guild = ((SocketGuildChannel)context.Channel).Guild;
            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            await context.Channel.ModifyMessageAsync(context.Message.Id, msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex, HostHelpers.CalculateNumTeams(BossIndex, names.Count));
            });

            await context.DeferAsync();
        }
    }
}
