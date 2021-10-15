using Discord;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("say", "Speak Cat", DefaultPermission.AdminOnly)]
    [ScheduableCommand]
    public class Say : CommandBase
    {
        [CommandOption("message", "What to say.", required: true)]
        public string Message { get; set; }

        public async override Task HandleCommand(CommandContext context)
        {
            await context.Channel.SendMessageAsync(Message, allowedMentions: new AllowedMentions(AllowedMentionTypes.Roles | AllowedMentionTypes.Users));
        }
    }
}
