using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Services;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Commands;

namespace WinstonBot.MessageHandlers
{
    public class AoDMessageHandlers
    {
        public static readonly EmoteDatabase.IEmoteDefinition AoDEmote = new EmoteDatabase.CustomEmoteDefinition() { Name = "winstonface" };
        public static readonly EmoteDatabase.IEmoteDefinition CompleteEmoji = new EmoteDatabase.EmojiDefinition() { Name = "\u2705" };
        public static readonly EmoteDatabase.IEmoteDefinition CancelEmoji = new EmoteDatabase.EmojiDefinition() { Name = "❌" };
        
        // TODO: if we want to be able to leave off from where we were if the bot restarts we probably need to serialize the state we were in.
        private enum State
        {
            WaitForQueueCompletion,
            ConfirmTeamSelection,
            WaitForCancelation
        }

        public class QueueCompleted : BaseMessageHandler
        {
            public QueueCompleted(Commands.CommandContext context, IUserReader userReader) : base(context, userReader)
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

                var emoteDB = ServiceProvider.GetRequiredService<EmoteDatabase>();
                var client = ServiceProvider.GetRequiredService<DiscordSocketClient>();

                var aodEmote = emoteDB.Get(client, AoDEmote);

                var names = await UserReader.GetMentionsFromReaction(message, aodEmote);
                if (names.Count() == 0)
                {
                    Console.WriteLine("No one signed up, cannot complete the group.");
                    return false;
                }

                var configService = ServiceProvider.GetRequiredService<ConfigService>();
                // NOTE: we can get the guild from the guild ID which we can store.
                SocketTextChannel teamConfirmationChannel = Context.Guild.GetTextChannel(configService.Configuration.TeamConfirmationChannelId);
                if (teamConfirmationChannel == null)
                {
                    await channel.SendMessageAsync("Failed to find team confirmation channel. Please use config set teamconfirmationchannel <channel> to set it.");
                    return false;
                }

                Console.WriteLine("[QueuedCompleted] sending pending team message to confirmation channel");
                var newMessage = await teamConfirmationChannel.SendMessageAsync(
                    $"Pending Team is:\n {String.Join('\n', names)}\n\nPress {CompleteEmoji.Name} To confirm team and post to main channel.\n\n" +
                    $"To revise team reply to this message with the full list of mentions for who should go.");

                ServiceProvider.GetRequiredService<MessageDatabase>().AddMessage(Context.Guild.Id, newMessage.Id, new TeamConfirmation(Context, UserReader, channel));

                var completeEmote = emoteDB.Get(client, CompleteEmoji);
                await newMessage.AddReactionAsync(emoteDB.Get(client, CompleteEmoji));
                return true;
            }
        }

        public class TeamConfirmation : BaseMessageHandler
        {
            private ISocketMessageChannel _channelToSendFinalTeamTo;

            public TeamConfirmation(CommandContext context, IUserReader userReader, ISocketMessageChannel channelToSendFinalTeamTo) : base(context, userReader)
            {
                _channelToSendFinalTeamTo = channelToSendFinalTeamTo;
            }

            public override async Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction)
            {
                if (reaction.Emote.Name == CompleteEmoji.Name)
                {
                    var client = ServiceProvider.GetRequiredService<DiscordSocketClient>();
                    IEnumerable<string> names = UserReader.GetMentionsFromMessage(message);

                    Console.WriteLine("[TeamConfirmation] Team confirmed");

                    // Send team to main channel
                    var newMessage = await _channelToSendFinalTeamTo.SendMessageAsync(
                        $"Team confirmed:\n {String.Join('\n', names)}\n\nPress {CancelEmoji.Name} to edit the team.");

                    ServiceProvider.GetRequiredService<MessageDatabase>().AddMessage(Context.Guild.Id, newMessage.Id, new CancelTeam(Context, UserReader));

                    // Create cancelation/deletion handler (remove team from DB and send a team confirmation again so they can edit).
                    var emoteDB = ServiceProvider.GetRequiredService<EmoteDatabase>();
                    await newMessage.AddReactionAsync(emoteDB.Get(client, CancelEmoji));

                    // Log team in DB
                    return true;
                }
                else if (reaction.Emote.Name == CancelEmoji.Name)
                {
                    Console.WriteLine("[TeamConfirmation] Team canceled, deleting message");
                    await channel.DeleteMessageAsync(message);
                    return true;
                }
                return false;
            }

            public override async Task<bool> MessageRepliedTo(SocketUserMessage messageParam)
            {
                // parse out new team and set this handler as the handler for the new message
                IEnumerable<string> newNames = UserReader.GetMentionsFromMessage(messageParam);

                var emoteDB = ServiceProvider.GetRequiredService<EmoteDatabase>();
                var client = ServiceProvider.GetRequiredService<DiscordSocketClient>();

                var configService = ServiceProvider.GetRequiredService<ConfigService>();
                SocketTextChannel teamConfirmationChannel = Context.Guild.GetTextChannel(configService.Configuration.TeamConfirmationChannelId);
                if (teamConfirmationChannel == null)
                {
                    await messageParam.Channel.SendMessageAsync("Failed to find team confirmation channel. Please use config set teamconfirmationchannel <channel> to set it.");
                    return false;
                }

                Console.WriteLine("[TeamConfirmation] Team was revised. Sending to confirmation channel.");

                var newMessage = await teamConfirmationChannel.SendMessageAsync(
                    $"Revised Team is:\n {String.Join('\n', newNames)}\n\nPress {CompleteEmoji.Name} To confirm team and post to main channel.\n\n" +
                    $"To revise team reply to this message with the full list of mentions for who should go.");

                ServiceProvider.GetRequiredService<MessageDatabase>().AddMessage(Context.Guild.Id, newMessage.Id, new TeamConfirmation(Context, UserReader, _channelToSendFinalTeamTo));

                await newMessage.AddReactionAsync(emoteDB.Get(client, CompleteEmoji));
                return true;
            }
        }

        public class CancelTeam : BaseMessageHandler
        {
            public CancelTeam(CommandContext context, IUserReader userReader) : base(context, userReader)
            {
            }

            public override async Task<bool> ReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction)
            {
                if (reaction.Emote.Name != CancelEmoji.Name)
                {
                    return false;
                }

                var client = ServiceProvider.GetRequiredService<DiscordSocketClient>();
                IEnumerable<string> names = UserReader.GetMentionsFromMessage(message);

                var configService = ServiceProvider.GetRequiredService<ConfigService>();
                SocketTextChannel teamConfirmationChannel = Context.Guild.GetTextChannel(configService.Configuration.TeamConfirmationChannelId);
                if (teamConfirmationChannel == null)
                {
                    await channel.SendMessageAsync("Failed to find team confirmation channel. Please use config set teamconfirmationchannel <channel> to set it.");
                    return false;
                }

                Console.WriteLine("[CancelTeam] Team was canceled, sending edit request to confirmation channel.");

                // Delete the original message
                await channel.DeleteMessageAsync(message);

                var newMessage = await teamConfirmationChannel.SendMessageAsync($"Canceled team:\n {String.Join('\n', names)}.\n\nPlease make any edits then confirm again.");

                ServiceProvider.GetRequiredService<MessageDatabase>().AddMessage(Context.Guild.Id, newMessage.Id, new TeamConfirmation(Context, UserReader, channel));

                var emoteDB = ServiceProvider.GetRequiredService<EmoteDatabase>();
                await newMessage.AddReactionAsync(emoteDB.Get(client, CompleteEmoji));
                return true;
            }
        }
    }
}
