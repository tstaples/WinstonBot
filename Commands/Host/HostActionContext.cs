using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    public class HostActionContext : ActionContext
    {
        public long BossIndex { get; set; }
        public BossData.Entry BossEntry => BossData.Entries[BossIndex];
        public HostMessageMetadata? OriginalMessageData => _originalMessageData;
        public SocketGuild? Guild => OriginalMessageData != null ? Client.GetGuild(OriginalMessageData.GuildId) : null;
        public SocketTextChannel? Channel => OriginalMessageData != null ? Guild?.GetTextChannel(OriginalMessageData.ChannelId) : null;
        public bool IsMessageDataValid => OriginalMessageData != null && Guild != null && Channel != null;
        public ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> OriginalSignupsForMessage { get; }
        public ConcurrentDictionary<ulong, bool> MessagesBeingEdited { get; }

        private HostMessageMetadata? _originalMessageData;

        public HostActionContext(
            DiscordSocketClient client,
            SocketMessageComponent arg,
            IServiceProvider services,
            ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> originalSignups,
            ConcurrentDictionary<ulong, bool> messagesBeingEdited)
            : base(client, arg, services)
        {
            OriginalSignupsForMessage = originalSignups;
            MessagesBeingEdited = messagesBeingEdited;
            _originalMessageData = GetOriginalMessageData();
        }

        public HostMessageMetadata? GetOriginalMessageData()
        {
            if (Component.Message.Embeds.Any() && Component.Message.Embeds.First().Footer.HasValue)
            {
                return HostMessageMetadata.ParseMetadata(Client, Component.Message.Embeds.First().Footer.Value.Text);
            }
            return null;
        }

        public async Task<IMessage?> GetOriginalMessage()
        {
            if (OriginalMessageData != null && OriginalMessageData.MessageId != 0 && Channel != null)
            {
                return await Channel.GetMessageAsync(OriginalMessageData.MessageId);
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
            var footerParts = text.Split(',');
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
