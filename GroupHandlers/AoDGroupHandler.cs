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
    public class AoDGroupHandler : IGroupHandler
    {
        // TODO: figure out how we pass this in. Should the handler be persistent? Probably not.
        private readonly EmoteDatabase _emoteDatabase;
        private MessageDatabase _messageDatabase;

        public AoDGroupHandler(EmoteDatabase emoteDB, MessageDatabase messageDatabase)
        {
            _emoteDatabase = emoteDB;
            _messageDatabase = messageDatabase;
        }

        public async Task CompleteGroup(DiscordSocketClient client, IUserMessage message, ISocketMessageChannel channel, MessageDatabase.GroupType groupType)
        {
            // 1. for queued, determine who should go based on our rules
            // 2. send result to the specific channel
            // 3. complete reaction will finalize the team

            List<IUser> userReactions = new List<IUser>();
            var aodEmote = _emoteDatabase.Get(client, EmoteDatabase.AoDEmote);
            IAsyncEnumerable<IReadOnlyCollection<IUser>> reactionUsers = message.GetReactionUsersAsync(aodEmote, 10);
            await foreach (var users in reactionUsers)
            {
                foreach (IUser user in users)
                {
                    if (!user.IsBot)
                    {
                        userReactions.Add(user);
                    }
                }
            }

            foreach (IUser user in userReactions)
            {
                Console.WriteLine(user.Username);
            }

            var names = userReactions.Select(user => user.Mention);

            var newMessage = await channel.SendMessageAsync(String.Join(' ', names));
            var completeEmote = _emoteDatabase.Get(client, EmoteDatabase.CompleteEmoji);
            await newMessage.AddReactionAsync(completeEmote);
        }
    }
}
