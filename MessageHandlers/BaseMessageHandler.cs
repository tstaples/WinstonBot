using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;
using WinstonBot.Commands;

namespace WinstonBot.MessageHandlers
{
    public abstract class BaseMessageHandler : IMessageHandler
    {
        protected ulong GuildId {  get; private set; }
        protected CommandContext Context { get; private set; }
        protected IServiceProvider ServiceProvider => Context.ServiceProvider;

        protected IUserReader UserReader {  get; private set; }

        public BaseMessageHandler(CommandContext context, IUserReader userReader)
        {
            Context = context;
            UserReader = userReader;
        }

        public virtual Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction) { return Task.FromResult(false); }
        public virtual Task<bool> MessageRepliedTo(SocketUserMessage messageParam) { return Task.FromResult(false); }
    }
}
