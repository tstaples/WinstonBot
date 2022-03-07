using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Services;
using WinstonBot.Attributes;
using WinstonBot.Data;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    [Action("fix-mentions-glitch")]
    internal class FixMentionsGlitchAction : ActionBase
    {
        public static string ActionName = "fix-mentions-glitch";


        public FixMentionsGlitchAction(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext context)
        {
            // Re-grab the message as it may have been modified by a concurrent action.
            var message = await context.Channel.GetMessageAsync(context.Message.Id);
            if (!message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }

            var currentEmbed = message.Embeds.First();
            
            Logger.LogInformation($"{context.User.Mention} used {ActionName} on {context.Message.Id}!");

            string fixMessage = "Hi, I am fulfilling your request to force-cache the Discord user IDs in the signup above, to compensate for the Discord client's visual glitch (don't worry, bot functionality is unaffected). " +
                "To do so, I just need to show your Discord client the user mentions in the message content field rather than an embed, so here they are:\n" +
                currentEmbed.Description + 
                "\n" + 
                "Now that your Discord client has seen these, the weird numbers in the signup itself should be replaced with an actual user.\n" +
                ">>> **NOTE: You might need to go to another channel and come back for your client to render the now-cached user mentions.**\n" +
                "**If it's still not working, try clicking on the user mentions in this message to REALLY force the cache download, and then refresh the channel again.**";

            await context.RespondAsync(fixMessage, ephemeral: true);
        }
    }
}
