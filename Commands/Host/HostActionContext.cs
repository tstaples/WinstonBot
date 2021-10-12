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
            if (Message.Embeds.Any() && Message.Embeds.First().Footer.HasValue)
            {
                return HostMessageMetadata.ParseMetadata(Client, Message.Embeds.First().Footer.Value.Text);
            }
            return null;
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
                    OriginalSignupsForMessage.TryAdd(messageId, new ReadOnlyCollection<ulong>(names));
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

        public static HostMessageMetadata? ParseMetadata(DiscordSocketClient client, string text)
        {
            var footerParts = text.Split(' ');
            if (footerParts.Length != 4)
            {
                return null;
            }

            var guildId = ulong.Parse(footerParts[0]);
            var channelId = ulong.Parse(footerParts[1]);
            var originalMessageId = ulong.Parse(footerParts[2]);
            var confirmedBefore = bool.Parse(footerParts[3]);

            return new HostMessageMetadata()
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = originalMessageId,
                TeamConfirmedBefore = confirmedBefore
            };
        }
    }
}
