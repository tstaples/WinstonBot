using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using WinstonBot.Attributes;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Action("pvm-revert-to-signup")]
    internal class RevertToSignupAction : IAction
    {
        public static string ActionName = "pvm-revert-to-signup";

        [ActionParam]
        public long BossIndex { get; set; }

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;
            ReadOnlyCollection<ulong> ids;
            if (!context.OriginalSignupsForMessage.TryGetValue(context.Message.Id, out ids))
            {
                await context.RespondAsync($"Failed to get original signups for message.");
                return;
            }

            var guild = ((SocketGuildChannel)context.Channel).Guild;
            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            // TODO: make this general for any boss signup
            var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
            aodDb.RemoveLastRowFromHistory();

            await context.Channel.ModifyMessageAsync(context.Message.Id, msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex);
            });

            await context.DeferAsync();
        }
    }
}
