using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Action("pvm-quit-signup")]
    internal class QuitAction : IAction
    {
        public static string ActionName = "pvm-quit-signup";

        [ActionParam]
        public long BossIndex { get; set; }

        public async Task HandleAction(ActionContext context)
        {
            if (!context.Message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var currentEmbed = context.Message.Embeds.First();
            var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
            var ids = HostHelpers.ParseNamesToIdList(names);
            if (!ids.Contains(context.User.Id))
            {
                Console.WriteLine($"{context.User.Mention} isn't signed up: ignoring.");
                await context.RespondAsync("You're not signed up.", ephemeral: true);
                return;
            }

            Console.WriteLine($"{context.User.Mention} has quit!");
            var index = ids.IndexOf(context.User.Id);
            names.RemoveAt(index);

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
            });
        }
    }
}
