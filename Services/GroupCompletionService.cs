using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Services
{
    public class GroupCompletionService
    {
        private readonly EmoteDatabase _emoteDatabase;

        public GroupCompletionService(EmoteDatabase emoteDatabase)
        {
            _emoteDatabase = emoteDatabase;
        }

        public async void CompleteGroup(DiscordSocketClient client, IUserMessage message, ISocketMessageChannel channel)
        {
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
        }
    }
}
