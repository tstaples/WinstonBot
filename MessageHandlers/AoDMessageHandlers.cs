using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace WinstonBot.MessageHandlers
{
    public class AoDMessageHandlers
    {
        public static readonly EmoteDatabase.IEmoteDefinition AoDEmote = new EmoteDatabase.CustomEmoteDefinition() { Name = "winstonface" };
        public static readonly EmoteDatabase.IEmoteDefinition CompleteEmoji = new EmoteDatabase.EmojiDefinition() { Name = "\u2705" };

        // TODO: if we want to be able to leave off from where we were if the bot restarts we probably need to serialize the state we were in.
        private enum State
        {
            WaitForQueueCompletion,
            ConfirmTeamSelection,
        }

        public class QueueCompleted : BaseMessageHandler
        {
            public QueueCompleted(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }

            public override async Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction)
            {
                // 1. for queued, determine who should go based on our rules
                // 2. send result to the specific channel
                // 3. complete reaction will finalize the team

                if (reaction.Emote.Name != CompleteEmoji.Name)
                {
                    return false;
                }

                var emoteDB = ServiceProvider.GetService<EmoteDatabase>();
                var client = ServiceProvider.GetService<DiscordSocketClient>();

                var aodEmote = emoteDB.Get(client, AoDEmote);

                List<IUser> userReactions = new List<IUser>();
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

                if (names.Count() == 0)
                {
                    Console.WriteLine("No one signed up, cannot complete the group.");
                    return false;
                }

                // TODO: send this to the secret channel
                var newMessage = await channel.SendMessageAsync("Team is: " + String.Join(' ', names));

                ServiceProvider.GetService<MessageDatabase>().AddMessage(newMessage.Id, new TeamConfirmation(ServiceProvider));

                var completeEmote = emoteDB.Get(client, CompleteEmoji);
                await newMessage.AddReactionAsync(completeEmote);
                return true;
            }
        }

        public class TeamConfirmation : BaseMessageHandler
        {
            public TeamConfirmation(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }

            public override async Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction)
            {
                if (reaction.Emote.Name != CompleteEmoji.Name)
                {
                    return false;
                }

                var client = ServiceProvider.GetService<DiscordSocketClient>();
                List<string> names = message.MentionedUserIds.Select(userId => client.GetUser(userId).Mention).ToList();

                // Send team to main channel
                await channel.SendMessageAsync("Team confirmed: " + String.Join(' ', names));

                // Log team in DB
                // Create cancelation/deletion handler (remove team from DB and send a team confirmation again so they can edit).
                return true;
            }

            public override async Task<bool> MessageRepliedTo(SocketUserMessage messageParam)
            {
                // parse out new team and set this handler as the handler for the new message
                List<string> newNames = messageParam.MentionedUsers.Select(user => user.Mention).ToList();

                var emoteDB = ServiceProvider.GetService<EmoteDatabase>();
                var client = ServiceProvider.GetService<DiscordSocketClient>();

                var newMessage = await messageParam.Channel.SendMessageAsync("Revised Team is: " + String.Join(' ', newNames));

                ServiceProvider.GetService<MessageDatabase>().AddMessage(newMessage.Id, new TeamConfirmation(ServiceProvider));

                await newMessage.AddReactionAsync(emoteDB.Get(client, CompleteEmoji));
                return true;
            }
        }
    }
}
