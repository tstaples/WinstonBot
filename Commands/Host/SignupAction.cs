namespace WinstonBot.Commands
{
    internal class SignupAction : IAction
    {
        public static string ActionName = "pvm-team-signup";
        public string Name => ActionName;
        public long RoleId => throw new NotImplementedException();

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
