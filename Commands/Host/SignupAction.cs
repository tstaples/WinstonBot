using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Services;
using WinstonBot.Attributes;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    [Action("pvm-team-signup")]
    internal class SignupAction : IAction
    {
        public static string ActionName = "pvm-team-signup";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public async Task HandleAction(ActionContext context)
        {
            if (!context.Message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var guild = ((SocketGuildChannel)context.Message.Channel).Guild;
            var configService = context.ServiceProvider.GetRequiredService<ConfigService>();
            List<ulong> rolesForBoss = new();
            if (configService.Configuration.GuildEntries[guild.Id].RolesNeededForBoss.TryGetValue(BossEntry.CommandName, out rolesForBoss))
            {
                if (!Utility.DoesUserHaveAnyRequiredRole((SocketGuildUser)context.User, rolesForBoss))
                {
                    await context.RespondAsync(
                        $"You must have one of the following roles to sign up:\n{Utility.JoinRoleMentions(guild, rolesForBoss)}\n" +
                        $"Please see #pvm-rules.", ephemeral: true);
                    return;
                }
            }

            var currentEmbed = context.Message.Embeds.First();

            var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
            var ids = HostHelpers.ParseNamesToIdList(names);
            if (ids.Contains(context.User.Id))
            {
                Console.WriteLine($"{context.User.Mention} is already signed up: ignoring.");
                await context.RespondAsync("You're already signed up.", ephemeral: true);
                return;
            }

            Console.WriteLine($"{context.User.Mention} has signed up!");
            names.Add(context.User.Mention);

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
            });
        }
    }
}
