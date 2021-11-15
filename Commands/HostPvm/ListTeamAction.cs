using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;
using System.Text;

namespace WinstonBot.Commands.HostPvm
{
    [Action("pvm-event-list-team")]
    public class ListTeamAction : ActionBase
    {
        public static readonly string Name = "pvm-event-list-team";

        public ListTeamAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            var runtimeRoles = Helpers.GetRuntimeRoles(context.Message.Embeds.FirstOrDefault());
            var users = new HashSet<ulong>();
            foreach (RuntimeRole role in runtimeRoles)
            {
                users = users.Concat(role.Users).ToHashSet();
            }

            var sorted = users.ToList();
            sorted.Sort();

            var guild = (context.Message.Channel as SocketGuildChannel).Guild;
            var mentions = Utility.ConvertUserIdListToMentions(guild, sorted);
            await context.RespondAsync($"Invite:\n{String.Join("\n", mentions)}", ephemeral: true);
        }
    }
}
