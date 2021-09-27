using Discord.Commands;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    public class CommandContext : SocketCommandContext
    {
        public MessageDatabase MessageDatabase {  get; set; }

        public CommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }
    }
}
