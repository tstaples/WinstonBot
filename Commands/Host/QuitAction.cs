namespace WinstonBot.Commands
{
    internal class QuitAction : IAction
    {
        public static string ActionName = "pvm-quit-signup";
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

            var currentEmbed = component.Message.Embeds.First();
            var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
            var ids = HostHelpers.ParseNamesToIdList(names);
            if (!ids.Contains(component.User.Id))
            {
                Console.WriteLine($"{component.User.Mention} isn't signed up: ignoring.");
                await component.RespondAsync("You're not signed up.", ephemeral: true);
                return;
            }

            Console.WriteLine($"{component.User.Mention} has quit!");
            var index = ids.IndexOf(component.User.Id);
            names.RemoveAt(index);

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildSignupEmbed(context.BossIndex, names);
            });
        }
    }
}
