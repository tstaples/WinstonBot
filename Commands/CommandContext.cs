using Discord.Commands;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    public class CommandContext : SocketCommandContext
    {
        public IServiceProvider ServiceProvider {  get; set; }

        public ulong GuildId { get; set; }

        public CommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }
    }
}
