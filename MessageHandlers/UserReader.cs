using Discord;
using Discord.WebSocket;

namespace WinstonBot.MessageHandlers
{
    public interface IUserReader
    {
        public IEnumerable<string> GetMentionsFromMessage(IUserMessage message);
        public Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote);
    }

    public class UserReader : IUserReader
    {
        private DiscordSocketClient _client;

        public UserReader(DiscordSocketClient client)
        {
            _client = client;
        }

        public IEnumerable<string> GetMentionsFromMessage(IUserMessage message)
        {
            return message.MentionedUserIds.Select(userId => _client.GetUser(userId).Mention).ToList();
        }

        public async Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote)
        {
            List<IUser> userReactions = new List<IUser>();
            IAsyncEnumerable<IReadOnlyCollection<IUser>> reactionUsers = message.GetReactionUsersAsync(emote, 10);
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

            var names = userReactions.Select(user => user.Mention);
            return names;
        }
    }

    public class MockUserReader : IUserReader
    {
        private string[] _names;

        public MockUserReader(string[] names)
        {
            _names = names;
        }

        public IEnumerable<string> GetMentionsFromMessage(IUserMessage message)
        {
            return message.Content.Split(' ');
        }

        public async Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote)
        {
            return _names;
        }
    }
}
