using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    internal class BlavikenService : DiscordClientService
    {
        private const ulong Blaviken = 532015945850421260;
        //private const ulong Blaviken = 141439679890325504;

        public BlavikenService(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Client.WaitForReadyAsync(stoppingToken);

            Client.ReactionAdded += OnReactionAdded;
            Client.MessageReceived += OnMessageReceived;
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
            if (arg3.Channel is SocketGuildChannel chan)
            {
                if (arg3.User.IsSpecified && arg3.User.Value.Id == Blaviken)
                {
                    if (arg3.Emote.Name == "ping" || arg3.Emote.Name == "pepeping")
                    {
                        Logger.LogInformation($"Trolling blaviken in {chan.Name}");
                        await arg3.Channel.SendMessageAsync($"<@!{Blaviken}>", allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
                    }
                }
            }
        }

        private async Task OnMessageReceived(SocketMessage arg)
        {
            if (arg.Channel is SocketGuildChannel chan)
            {
                if (arg.Author.Id == Blaviken)
                {
                    if (arg.Content.Contains("<:ping:777725364683538463>") || arg.Content.Contains("<:pepeping:777721327137062923>"))
                    {
                        Logger.LogInformation($"Trolling blaviken in {chan.Name}");
                        var reference = new MessageReference(arg.Id, arg.Channel.Id, chan.Guild.Id);
                        await arg.Channel.SendMessageAsync($"<@!{Blaviken}>", messageReference: reference, allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
                    }
                }
            }
        }
    }
}
