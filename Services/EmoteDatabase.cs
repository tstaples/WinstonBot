using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Services
{
    public class EmoteDatabase
    {
        public interface IEmoteDefinition
        {
            public string Name {  get; set; }
            public bool IsEmoji { get; }
        }

        public class CustomEmoteDefinition : IEmoteDefinition
        {
            public string Name {  get; set; }
            public bool IsEmoji => false;
        }

        public class EmojiDefinition : IEmoteDefinition
        {
            public string Name {  get; set; }
            public bool IsEmoji => true;
        }

        public static readonly IEmoteDefinition AoDEmote = new CustomEmoteDefinition() { Name = "winstonface" };
        public static readonly IEmoteDefinition CompleteEmoji = new EmojiDefinition() { Name = "\u2705" };

        private readonly Dictionary<IEmoteDefinition, IEmote> _emotes = new Dictionary<IEmoteDefinition, IEmote>();

        //public void Populate(DiscordSocketClient client)
        //{
        //    string[] emoteNames = new string[] { AoDEmoteName };
        //    foreach (string name in emoteNames)
        //    {
        //        var emote = Utility.TryGetEmote(client, name);
        //        if (emote == null)
        //        {
        //            Console.WriteLine("Failed to lookup emote: " + name);
        //        }
        //        else
        //        {
        //            _emotes.Add(name, emote);
        //        }
        //    }

        //    string[] emojiNames = new string[] { CompleteEmojiName };
        //    foreach (string name in emojiNames)
        //    {
        //        var emote = new Emoji(name);
        //        if (emote == null)
        //        {
        //            Console.WriteLine("Failed to lookup emoji: " + name);
        //        }
        //        else
        //        {
        //            _emotes.Add(name, emote);
        //        }
        //    }
        //}

        public IEmote Get(DiscordSocketClient client, IEmoteDefinition emoteDefinition)
        {
            IEmote cachedEmote;
            if (_emotes.TryGetValue(emoteDefinition, out cachedEmote))
            {
                return cachedEmote; 
            }

            if (emoteDefinition.IsEmoji)
            {
                var emoji = new Emoji(emoteDefinition.Name);
                _emotes.Add(emoteDefinition, emoji);
                return emoji;
            }
            else
            {
                var emote = Utility.TryGetEmote(client, emoteDefinition.Name);
                if (emote != null)
                {
                    _emotes.Add(emoteDefinition, emote);
                    return emote;
                }
            }
            return null;
        }
    }
}
