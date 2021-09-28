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
    public abstract class BaseMessageHandler : IMessageHandler
    {
        protected IServiceProvider ServiceProvider { get; private set; }

        public BaseMessageHandler(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public virtual Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction) { return Task.FromResult(false); }
        public virtual Task<bool> MessageRepliedTo(SocketUserMessage messageParam) { return Task.FromResult(false); }
    }
}
