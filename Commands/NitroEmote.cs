using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("do-emote", "Posts an animated emote for plebs without nitro.")]
    internal class NitroEmote : CommandBase
    {
        [CommandOption("emote", "The name of the emote")]
        public string EmoteName { get; set; }

        public override async Task HandleCommand(CommandContext context)
        {
            var emote = Utility.TryGetEmote(context.Client, EmoteName);
            if (emote != null)
            {
                string animatedSymbol = emote.Animated ? "a" : string.Empty;
                string emoteString = $"<{animatedSymbol}:{emote.Name}:{emote.Id}>";
                await context.RespondAsync(emoteString);
            }
        }
    }
}
