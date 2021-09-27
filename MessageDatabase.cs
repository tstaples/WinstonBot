using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot
{
    // TODO: this should store message ids of host messages in the DB so if the bot restarts the old messages still work.
    // We will need to remove the messages once the message gets deleted.
    public class MessageDatabase
    {
        private ConcurrentDictionary<ulong, bool> _hostMessages = new ConcurrentDictionary<ulong, bool>();

        public MessageDatabase()
        {

        }

        public void AddMessage(ulong messageId)
        {
            _hostMessages.TryAdd(messageId, true);
        }

        public bool HasMessage(ulong messageId)
        {
            return _hostMessages.ContainsKey(messageId);
        }
    }
}
