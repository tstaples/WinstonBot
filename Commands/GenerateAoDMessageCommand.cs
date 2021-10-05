using Discord;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    public class GenerateAoDMessageCommand : ICommand
    {
        public string Name => "generate-aod-message";

        public ICommand.Permission DefaultPermission => ICommand.Permission.Everyone;

        public ulong AppCommandId { get; set; }

        public IEnumerable<IAction> Actions => new List<IAction>();

        public SlashCommandProperties BuildCommand()
        {
            return new SlashCommandBuilder()
                .WithName(Name)
                .WithDescription("Post the daily aod signup message.")
                .Build();
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new ActionContext(client, arg, services);
        }

        public async Task HandleCommand(CommandContext context)
        {
            var reset = new DateTimeOffset(GetReset().AddDays(1));
            var startTime = new DateTimeOffset(GetReset().AddDays(1).AddMinutes(30));
            
            string startTimestamp = startTime.ToUnixTimeSeconds().ToString();
            string resetTimestamp = reset.ToUnixTimeSeconds().ToString();
            string message = $"<@&792538753762590790> React with <:AOD:774551619844046868> to be added to the queue for AOD at <t:{startTimestamp}> in <t:{startTimestamp}:R>.\n\n" +
                $"People will be selected by the bot based on roles they can do and how often they've come in the last 5 days (people who have come less will be chosen over those who have gone more)\n\n" +
                $"The team will be announced at<t:{resetTimestamp}>, so you have until then to add a reaction.";

            await context.SlashCommand.RespondAsync(message, allowedMentions:new AllowedMentions(AllowedMentionTypes.Roles));
        }

        private DateTime GetReset()
        {
            var date = DateTime.UtcNow;
            date = date.AddHours(-date.Hour)
                .AddMinutes(-date.Minute)
                .AddSeconds(-date.Second)
                .AddMilliseconds(-date.Millisecond);
            return date;
        }
    }
}
