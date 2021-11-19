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
                if (role.Definition.RoleType != RaidRole.Reserve)
                {
                    users = users.Concat(role.Users).ToHashSet();
                }
            }

            var guild = (context.Message.Channel as SocketGuildChannel).Guild;

            var sorted = users
                .Where(id => guild.GetUser(id) != null)
                .ToList();

            sorted.Sort((a, b) =>
            {
                var userA = guild.GetUser(a);
                var userB = guild.GetUser(b);
                var nameA = userA.Nickname ?? userA.Username;
                var nameB = userB.Nickname ?? userB.Username;
                return nameA.CompareTo(nameB);
            });

            var mentions = Utility.ConvertUserIdListToMentions(guild, sorted);
            await context.RespondAsync($"Invite:\n{String.Join("\n", mentions)}", ephemeral: true);
        }
    }
}
