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
    public class ActionContext
    {
        public DiscordSocketClient Client { get; set; }
        public SocketMessageComponent Component { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public ActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            Client = client;
            Component = arg;
            ServiceProvider = services;
        }
    }
}
