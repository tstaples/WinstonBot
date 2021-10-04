using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public static ulong GetUserIdFromMention(string mention)
        {
            var resultString = Regex.Match(mention, @"\d+").Value;
            ulong value = 0;
            if (ulong.TryParse(resultString, out value))
            {
                return value;
            }
            else
            {
                Console.WriteLine($"Failed to parse user id from string {mention}");
                return 0;
            }
        }
    }
}
