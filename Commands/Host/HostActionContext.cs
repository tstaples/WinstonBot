using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    public class HostActionContext : ActionContext
    {
        public HostMessageMetadata? OriginalMessageData => _originalMessageData;
        public SocketGuild? Guild => OriginalMessageData != null ? Client.GetGuild(OriginalMessageData.GuildId) : null;
        public SocketTextChannel? OriginalChannel => OriginalMessageData != null ? Guild?.GetTextChannel(OriginalMessageData.ChannelId) : null;
        public bool IsMessageDataValid => OriginalMessageData != null && Guild != null && OriginalChannel != null;
        public ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> OriginalSignupsForMessage => ServiceProvider.GetRequiredService<MessageDatabase>().OriginalSignupsForMessage;
        public ConcurrentDictionary<ulong, bool> MessagesBeingEdited => ServiceProvider.GetRequiredService<MessageDatabase>().MessagesBeingEdited;

        private HostMessageMetadata? _originalMessageData;

        public HostActionContext(
            DiscordSocketClient client,
            SocketMessageComponent arg,
            IServiceProvider services,
            string owningCommand)
            : base(client, arg, services, owningCommand)
        {
            _originalMessageData = GetOriginalMessageData();
        }

        public HostMessageMetadata? GetOriginalMessageData()
        {
            return HostMessageMetadata.ParseMetadata(Message.Embeds);
        }

        public async Task<IMessage?> GetOriginalMessage()
        {
            if (OriginalMessageData != null && OriginalMessageData.MessageId != 0 && OriginalChannel != null)
            {
                return await OriginalChannel.GetMessageAsync(OriginalMessageData.MessageId);
            }
            return null;
        }

        public void EditFinishedForMessage(ulong messageId)
        {
            bool outVal;
            MessagesBeingEdited.TryRemove(messageId, out outVal);
        }

        public bool TryMarkMessageForEdit(ulong messageId, List<ulong>? names = null)
        {
            if (MessagesBeingEdited.TryAdd(messageId, true))
            {
                if (names != null)
                {
                    var collection = new ReadOnlyCollection<ulong>(names);
                    if (!OriginalSignupsForMessage.TryAdd(messageId, collection))
                    {
                        // Update the collection
                        OriginalSignupsForMessage[messageId] = collection;
                    }
                }
                return true;
            }
            return false;
        }
    }

    public class HostMessageMetadata
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public bool TeamConfirmedBefore { get; set; }
        public Guid?[] HistoryIds { get; set; }

        public static HostMessageMetadata? ParseMetadata(IReadOnlyCollection<IEmbed> embeds)
        {
            if (!embeds.Any() || !embeds.First().Footer.HasValue)
            {
                return null;
            }

            // all the data except the history ids is the same.
            string text = embeds.First().Footer.Value.Text;

            var footerParts = text.Split(',');
            if (footerParts.Length < 4)
            {
                return null;
            }

            try
            {
                var guildId = ulong.Parse(footerParts[0]);
                var channelId = ulong.Parse(footerParts[1]);
                var originalMessageId = ulong.Parse(footerParts[2]);
                var confirmedBefore = bool.Parse(footerParts[3]);

                return new HostMessageMetadata()
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    MessageId = originalMessageId,
                    TeamConfirmedBefore = confirmedBefore,
                    HistoryIds = ParseHistoryIds(embeds)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Guid?[] ParseHistoryIds(IReadOnlyCollection<IEmbed> embeds)
        {
            Guid?[] historyIds = new Guid?[embeds.Count];
            for (int i = 0; i < embeds.Count; ++i)
            {
                var embed = embeds.ElementAt(i);

                string footerText = embed.Footer.Value.Text;
                var footerParts = footerText.Split(',');

                Guid? parsedId = null;
                if (footerParts.Length > 4)
                {
                    parsedId = Guid.Parse(footerParts[4]);
                }

                historyIds[i] = parsedId;
            }
            return historyIds;
        }
    }
}
