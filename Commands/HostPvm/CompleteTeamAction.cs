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
            // TODO: user proper config
            var guildUser = (context.User as SocketGuildUser);
            if (guildUser == null || !Utility.DoesUserHaveAnyRequiredRole(guildUser, new ulong[] { 826982246866223124, 773757083904114689 }))
            {
                await context.RespondAsync("Insufficient permissions", ephemeral: true);
                return;
            }

            var runtimeRoles = Helpers.GetRuntimeRoles(context.Message.Embeds.FirstOrDefault());
            var guild = (context.Message.Channel as SocketGuildChannel).Guild;

            var timestamp = context.Message.Embeds.First().Timestamp;

            Embed embed;
            MessageComponent component;
            Helpers.BuildSignup(runtimeRoles, Entry, guild, timestamp, out embed, out component);

            component = new ComponentBuilder()
                .WithButton("Edit", $"{EditTeamAction.Name}_{BossIndex}", ButtonStyle.Danger)
                .Build();

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = embed;
                msgProps.Components = component;
            });
        }
    }
}
