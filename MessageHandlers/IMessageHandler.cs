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
    public interface IMessageHandler
    {
        public Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction);
        public Task<bool> MessageRepliedTo(SocketUserMessage message);

        public void ConstructContext(IServiceProvider serviceProvider);
    }
}
