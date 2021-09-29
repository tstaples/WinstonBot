using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;

namespace WinstonBot.MessageHandlers
{
    public interface IMessageHandlerContext
    {
        public IServiceProvider ServiceProvider { get; set; }
        public IUserReader UserReader { get; set; }
    }

    public interface IMessageHandler
    {
        public Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction);
        public Task<bool> MessageRepliedTo(SocketUserMessage message);

        public void ConstructContext(IServiceProvider serviceProvider);
    }
}
