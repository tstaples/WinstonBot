using Discord;
using Discord.WebSocket;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    internal class HostHelpers
    {
        public static ITeamBuilder GetTeamBuilder(BossData.Entry entry)
        {
            if (entry.BuilderClass == null)
            {
                throw new ArgumentNullException($"No builder class set for {entry.CommandName}");
            }
            return (ITeamBuilder)Activator.CreateInstance(entry.BuilderClass);
        }

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

        public static Dictionary<string, ulong> ParseNamesToRoleIdMap(Embed embed)
        {
            Dictionary<string, ulong> roleToUsers = new();
            foreach (EmbedField field in embed.Fields)
            {
                roleToUsers.Add(field.Name, Utility.GetUserIdFromMention(field.Value));
            }
            return roleToUsers;
        }

        public static List<ulong> ParseNamesToIdListWithValidation(SocketGuild guild, string text)
        {
            return ParseNamesToList(text)
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .Where(id => guild.GetUser(id) != null)
                .ToList();
        }

        public static List<ulong> ParseNamesToIdList(IEnumerable<string> nameList)
        {
            return nameList
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .ToList();
        }

        public static List<ulong> ParseNamesToIdListWithValidation(SocketGuild guild, IEnumerable<string> nameList)
        {
            return nameList
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .Where(id => guild.GetUser(id) != null)
                .ToList();
        }

        public static string BuildMessageLink(ulong guildId, ulong channelId, ulong messageId)
        {
            return $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";
        }

        public static Embed BuildTeamSelectionEmbed(
            SocketGuild guild,
            ulong channelId,
            ulong messageId,
            bool confirmedBefore,
            BossData.Entry bossEntry,
            Dictionary<string, ulong> selectedNames)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"Pending Team for {bossEntry.PrettyName}")
                .WithDescription("Suggested Roles based on fancy math and attendance.")
                // We use spaces as separators as commas cause it to be treated as a long string that can't be broken.
                // This causes weird issues where the fields get super squished.
                .WithFooter($"{guild.Id} {channelId} {messageId} {confirmedBefore}")
                .WithThumbnailUrl(bossEntry.IconUrl)
                .WithColor(bossEntry.EmbedColor)
                .WithUrl(BuildMessageLink(guild.Id, channelId, messageId));

            foreach ((string role, ulong id) in selectedNames)
            {
                var user = guild.GetUser(id);
                string mention = user != null ? user.Mention : "None";
                var fieldBuilder = new EmbedFieldBuilder()
                    .WithName(role.ToString())
                    .WithValue(mention)
                    .WithIsInline(true);
                builder.AddField(fieldBuilder);
            }
            return builder.Build();
        }

        public static Embed BuildFinalTeamEmbed(
            SocketGuild guild,
            string finalizedByMention,
            BossData.Entry bossEntry,
            Dictionary<string, ulong> selectedNames)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"Selected Team for {bossEntry.PrettyName}")
                .WithDescription("__Suggested__ Roles based on fancy math and attendance.")
                .WithFooter($"Team Finalized by {finalizedByMention}")
                .WithColor(bossEntry.EmbedColor)
                .WithThumbnailUrl(bossEntry.IconUrl);

            foreach ((string role, ulong id) in selectedNames)
            {
                var user = guild.GetUser(id);
                string mention = user != null ? user.Mention : "None";
                var fieldBuilder = new EmbedFieldBuilder()
                    .WithName(role.ToString())
                    .WithValue(mention)
                    .WithIsInline(true);
                builder.AddField(fieldBuilder);
            }
            return builder.Build();
        }

        public static MessageComponent BuildTeamSelectionComponent(
            SocketGuild guild,
            long bossIndex,
            Dictionary<string, ulong> selectedNames,
            IEnumerable<ulong> unselectedNames)
        {
            var builder = new ComponentBuilder();
            foreach ((string role, ulong id) in selectedNames)
            {
                var user = guild.GetUser(id);
                if (user == null)
                {
                    // This user is likely no long in the discord
                    continue;
                }

                var username = user.Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"❌ {username}")
                    .WithCustomId($"{RemoveUserFromTeamAction.ActionName}_{bossIndex}_{id}")
                    .WithStyle(ButtonStyle.Danger));
            }

            // Ensure the add buttons are never on the same row as the remove buttons.
            int numSelectedNames = selectedNames.Where(pair => pair.Value != 0).Count();
            int currentRow = (int)Math.Ceiling((float)numSelectedNames / 5);

            foreach (var id in unselectedNames)
            {
                var user = guild.GetUser(id);
                if (user == null)
                {
                    // This user is likely no long in the discord
                    continue;
                }

                var username = user.Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"{username}")
                    .WithEmote(new Emoji("➕"))
                    .WithCustomId($"{AddUserToTeamAction.ActionName}_{bossIndex}_{id}")
                    .WithStyle(ButtonStyle.Success), row: currentRow);
            }

            // Ensure the confirm/cancel buttons are never on the same row as the other buttons.
            currentRow += (int)Math.Ceiling((float)unselectedNames.Count() / 5);

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"{ConfirmTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary),
                    row: currentRow);

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Cancel")
                    .WithCustomId($"{CancelTeamConfirmationAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger),
                    row: currentRow);

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
                .WithColor(bossEntry.EmbedColor)
                .WithCurrentTimestamp(); // TODO: include event start timestamp
            if (editedByMention != null)
            {
                builder.WithFooter($"Being edited by {editedByMention}");
            }
            return builder.Build();
        }
    }
}
