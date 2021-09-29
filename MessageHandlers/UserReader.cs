using Discord;
using Discord.WebSocket;

namespace WinstonBot.MessageHandlers
{
    public interface IUserReader
    {
        public enum ReaderType
        { 
            Default,
            Debug
        }

        public ReaderType MyReaderType { get; }

        public IEnumerable<string> GetMentionsFromMessage(IUserMessage message);
        public Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote);
    }

    [Serializable]
    public abstract class BaseUserReader : IUserReader
    {
        private IUserReader.ReaderType _readerType;

        public IUserReader.ReaderType MyReaderType => _readerType;

        public BaseUserReader(IUserReader.ReaderType readerType)
        {
            _readerType = readerType;
        }

        public abstract IEnumerable<string> GetMentionsFromMessage(IUserMessage message);

        public abstract Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote);
    }

    public class UserReader : BaseUserReader
    {
        [NonSerialized]
        private DiscordSocketClient _client;

        public UserReader(DiscordSocketClient client) : base(IUserReader.ReaderType.Default)
        {
            _client = client;
        }

        public override IEnumerable<string> GetMentionsFromMessage(IUserMessage message)
        {
            return message.MentionedUserIds.Select(userId => _client.GetUser(userId).Mention).ToList();
        }

        public async override Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote)
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

    [Serializable]
    public class MockUserReader : BaseUserReader
    {
        private string[] _names;

        public MockUserReader(string[] names) : base(IUserReader.ReaderType.Debug)
        {
            _names = names;
        }

        public override IEnumerable<string> GetMentionsFromMessage(IUserMessage message)
        {
            // TODO: actually parse from the message.
            //return message.Content.Split(' ');
            return _names;
        }

        public async override Task<IEnumerable<string>> GetMentionsFromReaction(IUserMessage message, IEmote emote)
        {
            return _names;
        }
    }
}
