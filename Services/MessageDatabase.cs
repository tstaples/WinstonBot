using System.Collections.Concurrent;

namespace WinstonBot.Services
{
    // TODO: this should store message ids of host messages in the DB so if the bot restarts the old messages still work.
    // We will need to remove the messages once the message gets deleted.
    public class MessageDatabase
    {
        public enum MessageType
        {
            AoD
        }

        public enum GroupType
        {
            Default,
            Queued
        }

        public class MessageData
        {
            public MessageType Type { get; set; }
            public GroupType GroupType { get; set; }
        }

        private ConcurrentDictionary<ulong, MessageData> _hostMessages = new ConcurrentDictionary<ulong, MessageData>();

        public void AddMessage(ulong messageId, MessageType messageType, GroupType groupType)
        {
            _hostMessages.TryAdd(messageId, new MessageData { Type = messageType, GroupType = groupType });
        }

        public bool HasMessage(ulong messageId)
        {
            return _hostMessages.ContainsKey(messageId);
        }

        public MessageData GetMessageData(ulong messageId)
        {
            return _hostMessages[messageId];
        }
    }
}
