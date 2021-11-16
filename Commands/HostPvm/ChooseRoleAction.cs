using Discord;
using WinstonBot.Data;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;

namespace WinstonBot.Commands.HostPvm
{
    [Action("pvm-event-choose-role")]
    public class ChooseRoleAction : ActionBase
    {
        public static readonly string Name = "pvm-event-choose-role";

        [ActionParam]
        public long BossIndex { get; set; }

        [ActionParam]
        public long RoleIndex { get; set; }

        private BossData.Entry Entry => BossData.Entries[BossIndex];

        public ChooseRoleAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            // TODO: user proper config
            var guildUser = (context.User as SocketGuildUser);
            if (guildUser == null || !Utility.DoesUserHaveAnyRequiredRole(guildUser, new ulong[] { 851165967106441256 }))
            {
                await context.RespondAsync($"Insufficient permissions: Please read the raid rules to get the <@&851165967106441256> role.", ephemeral: true);
                return;
            }

            var runtimeRoles = Helpers.GetRuntimeRoles(context.Message.Embeds.FirstOrDefault());
            var role = runtimeRoles[RoleIndex];

            if (Helpers.GetUserCount(runtimeRoles) >= Entry.MaxPlayersOnTeam)
            {
                await context.RespondAsync("Signup is full", ephemeral: true);
                return;
            }

            List<RaidRole> conflictingRoles;
            if (role.HasUser(context.User.Id))
            {
                Logger.LogInformation($"Removing {context.User} from role {role.Definition.Name} on {Entry.PrettyName} signup");
                role.RemoveUser(context.User.Id);
            }
            else if (Helpers.CanAddUserToRole(context.User.Id, role.Definition.RoleType, runtimeRoles, out conflictingRoles))
            {
                if (role.AddUser(context.User.Id))
                {
                    Logger.LogInformation($"Added {context.User} to role {role.Definition.Name} on {Entry.PrettyName} signup");
                }
                else
                {
                    context.RespondAsync($"{role.Definition.Name} role is full.", ephemeral: true).GetAwaiter();
                    return;
                }
            }
            else
            {
                context.RespondAsync($"You cannot signup for {role.Definition.Name} because you're already signed up for {String.Join(", ", conflictingRoles)}.", ephemeral: true).GetAwaiter();
                return;
            }

            var guild = (context.Message.Channel as SocketGuildChannel).Guild;

            await context.DeferAsync();

            var timestamp = context.Message.Embeds.First().Timestamp;

            Embed embed;
            MessageComponent component;
            Helpers.BuildSignup(runtimeRoles, Entry, guild, timestamp, out embed, out component);

            context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Embed = embed;
                msgProps.Components = component;
            }).Wait();
        }
    }
}
