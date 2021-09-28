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
        public Task ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction);
        public Task MessageRepliedTo(SocketUserMessage message);
    }
}
