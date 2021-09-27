using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;

namespace WinstonBot.GroupHandlers
{
    public interface IGroupHandler
    {
        public Task CompleteGroup(DiscordSocketClient client, IUserMessage message, ISocketMessageChannel channel, MessageDatabase.GroupType groupType);
    }
}
