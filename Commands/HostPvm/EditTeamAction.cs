using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;

namespace WinstonBot.Commands.HostPvm
{
    [Action("pvm-event-edit")]
    public class EditTeamAction : ActionBase
    {
        public static readonly string Name = "pvm-event-edit";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry Entry => BossData.Entries[BossIndex];

        public EditTeamAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            // TODO: user proper config
            var guildUser = (context.User as SocketGuildUser);
            if (guildUser == null || !Utility.DoesUserHaveAnyRequiredRole(guildUser, new ulong[] { 826982246866223124, 773757083904114689 }))
            {
                await context.RespondAsync("Insufficient permissions", ephemeral: true);
                return;
            }

            var runtimeRoles = Helpers.GetRuntimeRoles(context.Message.Embeds.FirstOrDefault());
            var guild = (context.Message.Channel as SocketGuildChannel).Guild;

            Embed embed;
            MessageComponent component;
            Helpers.BuildSignup(runtimeRoles, Entry, guild, out embed, out component);

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = embed;
                msgProps.Components = component;
            });
        }
    }
}
