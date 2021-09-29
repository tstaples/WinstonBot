using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;
using WinstonBot.Commands;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace WinstonBot.MessageHandlers
{
    public class MessageHandlerContext : IMessageHandlerContext
    {
        public ulong GuildId {  get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public IUserReader UserReader { get; set; }

        public MessageHandlerContext(ulong guildId, IServiceProvider serviceProvider, IUserReader userReader)
        {
            GuildId = guildId;
            ServiceProvider = serviceProvider;
            UserReader = userReader;
        }
    }

    [Serializable]
    public abstract class BaseMessageHandler : IMessageHandler
    {
        [NonSerialized]
        private MessageHandlerContext _context;
        protected MessageHandlerContext Context => _context;

        // Serialized fields
        public IUserReader.ReaderType UserReaderType { get; set; }
        public ulong GuildId { get; set; }

        protected IServiceProvider ServiceProvider => Context.ServiceProvider;
        protected IUserReader UserReader => Context.UserReader;
        protected DiscordSocketClient Client => ServiceProvider.GetRequiredService<DiscordSocketClient>();
        protected SocketGuild Guild => Client.GetGuild(GuildId);

        public BaseMessageHandler() { }

        public BaseMessageHandler(MessageHandlerContext context)
        {
            _context = context;

            GuildId = context.GuildId;
            UserReaderType = context.UserReader.MyReaderType;
        }

        public virtual Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction) { return Task.FromResult(false); }
        public virtual Task<bool> MessageRepliedTo(SocketUserMessage messageParam) { return Task.FromResult(false); }

        // For re-constructing after being deserialized.
        public void ConstructContext(IServiceProvider serviceProvider)
        {
            IUserReader userReader = null;
            switch (UserReaderType)
            {
                case IUserReader.ReaderType.Default:
                    userReader = new UserReader(serviceProvider.GetRequiredService<DiscordSocketClient>());
                    break;

                case IUserReader.ReaderType.Debug:
                    userReader = new MockUserReader(serviceProvider.GetRequiredService<ConfigService>().Configuration.DebugTestNames);
                    break;
            }

            Debug.Assert(GuildId != 0);
            _context = new MessageHandlerContext(GuildId, serviceProvider, userReader);
        }
    }
}
