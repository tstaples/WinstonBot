using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    internal class SignupAction : IAction
    {
        public static string ActionName = "pvm-team-signup";
        public string Name => ActionName;

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            var component = context.Component;
            if (!component.Message.Embeds.Any())
            {
                await component.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var configService = context.ServiceProvider.GetRequiredService<ConfigService>();
            List<ulong> rolesForBoss = new();
            if (configService.Configuration.GuildEntries[context.Guild.Id].RolesNeededForBoss.TryGetValue(context.BossEntry.CommandName, out rolesForBoss))
            {
                if (!Utility.DoesUserHaveAnyRequiredRole((SocketGuildUser)component.User, rolesForBoss))
                {
                    await component.RespondAsync(
                        $"You must have one of the following roles to sign up:\n{Utility.JoinRoleMentions(context.Guild, rolesForBoss)}\n" +
                        $"Please see #pvm-rules.", ephemeral: true);
                    return;
                }
            }

            var currentEmbed = component.Message.Embeds.First();

            var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
            var ids = HostHelpers.ParseNamesToIdList(names);
            if (ids.Contains(component.User.Id))
            {
                Console.WriteLine($"{component.User.Mention} is already signed up: ignoring.");
                await component.RespondAsync("You're already signed up.", ephemeral: true);
                return;
            }

            // TODO: handle checking they have the correct role.
            Console.WriteLine($"{component.User.Mention} has signed up!");
            names.Add(component.User.Mention);

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(context.BossIndex, names);
            });
        }
    }
}
