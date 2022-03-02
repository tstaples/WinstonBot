using Discord;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("say", "Speak Cat", DefaultPermission.AdminOnly)]
    [ScheduableCommand]
    [ConfigurableCommand]
    public class Say : CommandBase
    {
        [CommandOption("message", "What to say.", required: true)]
        public string Message { get; set; }

        public Say(ILogger logger) : base(logger) { }

        public async override Task HandleCommand(CommandContext context)
        {
            await context.SendMessageAsync(Message, allowedMentions: new AllowedMentions(AllowedMentionTypes.Roles | AllowedMentionTypes.Users));
        }
    }

    [Command("meow", "Annoy Catman", DefaultPermission.Everyone)]
    [ScheduableCommand]
    [ConfigurableCommand]
    public class meow : CommandBase
    {
        public meow(ILogger logger) : base(logger) { }

        public async override Task HandleCommand(CommandContext context)
        {
            await context.SendMessageAsync($"<@141439679890325504>", allowedMentions: AllowedMentions.All);
            await context.SendMessageAsync($"<a:MEOW:881462772636995595>");
        }
    }
}
