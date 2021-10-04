using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    internal class HostHelpers
    {
        public static List<string> ParseNamesToList(string text)
        {
            if (text != null)
            {
                return text.Split(Environment.NewLine).ToList();
            }
            return new List<string>();
        }

        public static List<ulong> ParseNamesToIdList(string text)
        {
            return ParseNamesToList(text)
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .ToList();
        }

        public static List<ulong> ParseNamesToIdList(IEnumerable<string> nameList)
        {
            return nameList
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .ToList();
        }

        public static string BuildMessageLink(ulong guildId, ulong channelId, ulong messageId)
        {
            return $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";
        }

        public static Embed BuildTeamSelectionEmbed(
            ulong guildId,
            ulong channelId,
            ulong messageId,
            bool confirmedBefore,
            BossData.Entry bossEntry,
            List<string> selectedNames)
        {
            return new EmbedBuilder()
                .WithTitle("Pending Team")
                .WithDescription(String.Join(Environment.NewLine, selectedNames))
                .WithFooter($"{guildId},{channelId},{messageId},{confirmedBefore}")
                .WithThumbnailUrl(bossEntry.IconUrl)
                .WithUrl(BuildMessageLink(guildId, channelId, messageId))
                .Build();
        }

        public static MessageComponent BuildTeamSelectionComponent(
            SocketGuild guild,
            long bossIndex,
            List<string> selectedNames,
            List<string> unselectedNames)
        {
            var builder = new ComponentBuilder();
            foreach (var mention in selectedNames)
            {
                ulong userid = Utility.GetUserIdFromMention(mention);
                var username = guild.GetUser(userid).Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"❌ {username}")
                    .WithCustomId($"{RemoveUserFromTeamAction.ActionName}_{bossIndex}_{mention}")
                    .WithStyle(ButtonStyle.Danger));
            }

            foreach (var mention in unselectedNames)
            {
                var username = guild.GetUser(Utility.GetUserIdFromMention(mention)).Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"{username}")
                    .WithEmote(new Emoji("➕"))
                    .WithCustomId($"{AddUserToTeamAction.ActionName}_{bossIndex}_{mention}")
                    .WithStyle(ButtonStyle.Success));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"{ConfirmTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Cancel")
                    .WithCustomId($"{CancelTeamConfirmationAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger));

            return builder.Build();
        }

        public static MessageComponent BuildSignupButtons(long bossIndex, bool disabled = false)
        {
            var builder = new ComponentBuilder()
                .WithButton("Sign Up", $"{SignupAction.ActionName}_{bossIndex}", disabled: disabled)
                .WithButton(new ButtonBuilder()
                    .WithLabel("Unsign")
                    .WithDisabled(disabled)
                    .WithCustomId($"{QuitAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger))
                .WithButton(new ButtonBuilder()
                    .WithDisabled(disabled)
                    .WithLabel("Complete Team")
                    .WithCustomId($"{CompleteTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            return builder.Build();
        }

        public static MessageComponent BuildEditButton(long bossIndex, bool disabled = false)
        {
            return new ComponentBuilder()
                .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{bossIndex}", ButtonStyle.Danger, disabled: disabled)
                .Build();
        }

        public static Embed BuildSignupEmbed(long bossIndex, IEnumerable<string> names, string? editedByMention = null)
        {
            var bossEntry = BossData.Entries[bossIndex];
            var builder = new EmbedBuilder()
                .WithTitle($"{bossEntry.PrettyName}")
                .WithDescription(String.Join(Environment.NewLine, names))
                .WithThumbnailUrl(bossEntry.IconUrl)
                .WithCurrentTimestamp(); // TODO: include event start timestamp
            if (editedByMention != null)
            {
                builder.WithFooter($"Being edited by {editedByMention}");
            }
            return builder.Build();
        }
    }
}
