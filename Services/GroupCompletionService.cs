using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.GroupHandlers;

namespace WinstonBot.Services
{
    public class GroupCompletionService
    {
        private readonly EmoteDatabase _emoteDatabase;

        private Dictionary<MessageDatabase.MessageType, IGroupHandler> _groupHandlers = new Dictionary<MessageDatabase.MessageType, IGroupHandler>();

        public GroupCompletionService(EmoteDatabase emoteDatabase)
        {
            _emoteDatabase = emoteDatabase;

            _groupHandlers = new Dictionary<MessageDatabase.MessageType, IGroupHandler>()
            {
                { MessageDatabase.MessageType.AoD, new AoDGroupHandler(_emoteDatabase) }
            };
        }

        public async void CompleteGroup(DiscordSocketClient client, IUserMessage message, ISocketMessageChannel channel, MessageDatabase.MessageType messageType, MessageDatabase.GroupType groupType)
        {
            IGroupHandler handler;
            if (_groupHandlers.TryGetValue(messageType, out handler))
            {
                await handler.CompleteGroup(client, message, channel, groupType);
            }
        }
    }
}
