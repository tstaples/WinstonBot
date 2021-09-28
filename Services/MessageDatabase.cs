using System.Collections.Concurrent;
using WinstonBot.MessageHandlers;

namespace WinstonBot.Services
{
    // TODO: this should store message ids of host messages in the DB so if the bot restarts the old messages still work.
    // We will need to remove the messages once the message gets deleted.
    public class MessageDatabase
    {
        private ConcurrentDictionary<ulong, IMessageHandler> _hostMessages = new ConcurrentDictionary<ulong, IMessageHandler>();

        public void AddMessage(ulong messageId, IMessageHandler handler)
        {
            _hostMessages.TryAdd(messageId, handler);
        }

        public bool HasMessage(ulong messageId)
        {
            return _hostMessages.ContainsKey(messageId);
        }

        public IMessageHandler GetMessageHandler(ulong messageId)
        {
            return _hostMessages[messageId];
        }
    }
}
