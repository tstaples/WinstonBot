using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;

namespace WinstonBot.Commands.HostPvm
{
    [Action("pvm-event-complete")]
    public class CompleteTeamAction : ActionBase
    {
        public static readonly string Name = "pvm-event-complete";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry Entry => BossData.Entries[BossIndex];

        public CompleteTeamAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            var runtimeRoles = Helpers.GetRuntimeRoles(context.Message.Embeds.FirstOrDefault());
            var guild = (context.Message.Channel as SocketGuildChannel).Guild;

            Embed embed;
            MessageComponent component;
            Helpers.BuildSignup(runtimeRoles, Entry, guild, out embed, out component);

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = embed;
                msgProps.Components = null;
            });
        }
    }
}
