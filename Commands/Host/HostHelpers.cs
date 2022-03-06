using Discord;
using Discord.WebSocket;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    internal class HostHelpers
    {
        // We can only have 25 buttons and we need 2 for confirm/cancel.
        public const int MaxSignupsAllowed = ComponentBuilder.MaxActionRowCount * ActionRowBuilder.MaxChildCount;

        public static ITeamBuilder GetTeamBuilder(IServiceProvider serviceProvider, BossData.Entry entry)
        {
            if (entry.BuilderClass == null)
            {
                throw new ArgumentNullException($"No builder class set for {entry.CommandName}");
            }

            var builder = Activator.CreateInstance(entry.BuilderClass) as ITeamBuilder;
            if (builder == null)
            {
                throw new ArgumentException($"Failed construct builder for {entry.CommandName}");
            }

            builder.ServiceProvider = serviceProvider;
            return builder;
        }

        public static List<string> ParseMentionsStringToMentionList(string text)
        {
            if (text != null)
            {
                return text.Split(Environment.NewLine).ToList();
            }
            return new List<string>();
        }

        public static List<ulong> ParseNamesToIdList(string text)
        {
            return ParseMentionsStringToMentionList(text)
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .ToList();
        }

        public static Dictionary<string, ulong> ParseNamesToRoleIdMap(IEmbed embed)
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
            return ParseMentionsStringToMentionList(text)
                .Select(mention => Utility.GetUserIdFromMention(mention))
                .Where(id => guild.GetUser(id) != null)
                .ToList();
        }

        public static List<ulong> ParseMentionListToIdList(IEnumerable<string> nameList)
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
            Guid? historyId,
            bool confirmedBefore,
            int teamIndex,
            BossData.Entry bossEntry,
            Dictionary<string, ulong> selectedNames)
        {
            // We use spaces here too so the line can break properly and not cause the embed to get messed up
            string footerText = $"{guild.Id}, {channelId}, {messageId}, {confirmedBefore}";
            if (historyId != null)
            {
                footerText += $", {historyId}";
            }

            var builder = new EmbedBuilder()
                .WithTitle($"Pending Team ({teamIndex + 1}) for {bossEntry.PrettyName}")
                .WithDescription("Suggested Roles based on fancy math and attendance.")
                .WithFooter(footerText)
                .WithThumbnailUrl(bossEntry.IconUrl)
                .WithColor(bossEntry.EmbedColor);
                // TODO: url is currently bugged and causes only a single embed to appear.
                //.WithUrl(BuildMessageLink(guild.Id, channelId, messageId));

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
            int teamIndex,
            Dictionary<string, ulong> selectedNames,
            Guid historyId)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"Team {teamIndex + 1} for {bossEntry.PrettyName}")
                .WithDescription("__Suggested__ Roles based on \"fancy\" math and attendance. Feel free to swap roles within your team.")
                .WithFooter($"Finalized by {finalizedByMention} | {historyId}")
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
            IEnumerable<ulong> selectedNames,
            IEnumerable<ulong> unselectedNames)
        {
            var builder = new ComponentBuilder();
            foreach (ulong id in selectedNames)
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
            int numSelectedNames = selectedNames.Where(id => id != 0).Count();
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
            // Can't exceed max rows
            currentRow = Math.Min(currentRow, ComponentBuilder.MaxActionRowCount - 1);

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

        public static MessageComponent BuildSignupButtons(long bossIndex, int numTeams, bool disabled = false)
        {
            var builder = new ComponentBuilder()
                .WithButton("Sign Up", $"{SignupAction.ActionName}_{bossIndex}", disabled: disabled)
                .WithButton(new ButtonBuilder()
                    .WithLabel("Unsign")
                    .WithDisabled(disabled)
                    .WithCustomId($"{QuitAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger));
                
            for (int i = 0; i < numTeams; i++)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithDisabled(disabled)
                    .WithLabel($"Create {i + 1} Team{(i == 0 ? string.Empty : 's')}")
                    .WithCustomId($"{CompleteTeamAction.ActionName}_{bossIndex}_{i + 1}")
                    .WithStyle(ButtonStyle.Success));
            }
            return builder.Build();
        }

        public static MessageComponent BuildFinalTeamComponents(long bossIndex, bool disabled = false)
        {
            return new ComponentBuilder()
                .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{bossIndex}", ButtonStyle.Danger, disabled: disabled)
                .WithButton("Revert To Signup", $"{RevertToSignupAction.ActionName}_{bossIndex}", ButtonStyle.Danger, disabled: disabled)
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
                .WithFooter($"{names.Count()} Signed up.");
                //.WithCurrentTimestamp(); // TODO: include event start timestamp
            if (editedByMention != null)
            {
                builder.WithFooter($"Being edited by {editedByMention}");
            }
            return builder.Build();
        }

        public static Guid ParseHistoryIdFromFooter(string text)
        {
            var parts = text.Split('|');
            if (parts.Length == 2)
            {
                Guid outGuid;
                Guid.TryParse(parts[1], out outGuid);
                return outGuid;
            }
            return Guid.Empty;
        }

        public static string UpdateHistoryIdInFooter(string text, Guid historyId)
        {
            var footerTextParts = text.Split('|');
            if (footerTextParts.Length > 0)
            {
                string footerText = $"{footerTextParts[0]} | {historyId}";
                return footerText;
            }
            return $" | {historyId}";
        }

        public static int CalculateNumTeams(long bossIndex, int numUsers)
        {
            var entry = BossData.Entries[bossIndex];
            int maxPlayers = (int)entry.MaxPlayersOnTeam;
            int minPlayers = maxPlayers / 2;
            int numTeams = numUsers / maxPlayers;
            int remainder = numUsers % maxPlayers;
            if (remainder >= minPlayers)
            {
                ++numTeams;
            }
            return Math.Max(numTeams, 1);
        }

        // The Discord API returns two different forms of mentions for some reason,
        // <@ID> and <@!ID>, the latter being when the user has a nickname set in the guild.
        // As of the time of this writing (3/06/2022), the second format seems to have no
        // observable benefit while only being an occassional hinderance (other users looking
        // at the mention without the mentioned user downloaded into their Discord cache see
        // the raw mention, rather than the username or nickname of the user the mention
        // refers to, and thus have no idea who it is). Ultimately this doesn't affect 
        // bot functionality, but it is a significant annoyance to not know who it is that
        // is being mentioned in bot posts, which are usually supposed to be informational.
        // Until Discord fixes this (client-side) bug, having bots use only the first (universal)
        // format for user mentions seems to be a reasonable workaround.
        public static string GetUniversalUserMention(IUser user)
        {
                return "<@" + user.Id + ">";
        }
    }
}
