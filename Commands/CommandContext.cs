using Discord.Commands;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    public class CommandContext
    {
        public DiscordSocketClient Client {  get; set; }
        public SocketSlashCommand SlashCommand {  get; set; }
        public IServiceProvider ServiceProvider {  get; set; }
        public CommandContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            Client = client;
            SlashCommand = arg;
            ServiceProvider = services;
        }
    }
}
