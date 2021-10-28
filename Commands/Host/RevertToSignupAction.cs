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

            var currentEmbed = context.Message.Embeds.First();
            if (currentEmbed == null)
            {
                throw new ArgumentNullException("Message is missing the embed");
            }

            ReadOnlyCollection<ulong> ids;
            if (!context.OriginalSignupsForMessage.TryGetValue(context.Message.Id, out ids))
            {
                Logger.LogWarning($"{context.User.Mention} Couldn't retrieve original signups for {context.Message.Id}: just using who was selected.");
                ids = new ReadOnlyCollection<ulong>(HostHelpers.ParseNamesToRoleIdMap(currentEmbed).Values.ToList());
                await context.Channel.SendMessageAsync($"{context.User.Mention} Couldn't retrieve original signups for {context.Message.Id}: just using who was selected.");
            }

            Guid historyId = HostHelpers.ParseHistoryIdFromFooter(currentEmbed.Footer.Value.Text);

            var guild = ((SocketGuildChannel)context.Channel).Guild;
            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            // TODO: make this general for any boss signup
            var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
            aodDb.RemoveRowFromHistory(historyId);

            await context.Channel.ModifyMessageAsync(context.Message.Id, msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex);
            });

            await context.DeferAsync();
        }
    }
}
