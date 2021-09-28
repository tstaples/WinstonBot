using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot
{
    internal class Utility
    {
        public static GuildEmote TryGetEmote(DiscordSocketClient client, string name)
        {
            return client.Guilds.SelectMany(x => x.Emotes)
                .FirstOrDefault(x => x.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1);
        }

        public static ulong GetChannelIdByName(DiscordSocketClient client, string channelName)
        {
            return 0;
        }
    }
}
