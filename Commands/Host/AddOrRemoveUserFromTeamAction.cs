using Discord;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    internal abstract class AddOrRemoveUserFromTeamBase : ActionBase
    {
        [ActionParam]
        public long BossIndex { get; set; }

        [ActionParam]
        public ulong MentionToAdd { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public AddOrRemoveUserFromTeamBase(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;
            if (context.OriginalMessageData == null || !context.IsMessageDataValid)
            {
                await context.RespondAsync("The original message this interaction was created from could not be found.", ephemeral: true);
                return;
            }

            int teamIndex = 0;
            HashSet<ulong> allSelectedIds = new();
            List<Embed> embeds = new();
            foreach (var currentEmbed in context.Message.Embeds)
            {
                Dictionary<string, ulong> selectedIds = HostHelpers.ParseNamesToRoleIdMap(currentEmbed);
                if (CanRunActionForUser(MentionToAdd, currentEmbed.Fields, allSelectedIds.Concat(selectedIds.Values)))
                {
                    if (!context.OriginalSignupsForMessage.ContainsKey(context.OriginalMessageData.MessageId))
                    {
                        await context.RespondAsync($"No user data could be found for message {context.OriginalMessageData.MessageId}.\n" +
                            $"This may be because the bot restarted while you were editing.\n" +
                            $"Please click 'Confirm Team' on the original message to try again.",
                            ephemeral: true);
                        return;
                    }

                    RunActionForUser(MentionToAdd, currentEmbed.Fields, selectedIds);
                }

                allSelectedIds = allSelectedIds.Concat(selectedIds.Values).ToHashSet();

                embeds.Add(HostHelpers.BuildTeamSelectionEmbed(
                        context.Guild,
                        context.OriginalMessageData.ChannelId,
                        context.OriginalMessageData.MessageId,
                        context.OriginalMessageData.HistoryIds[teamIndex],
                        context.OriginalMessageData.TeamConfirmedBefore,
                        teamIndex,
                        BossEntry,
                        selectedIds));

                ++teamIndex;
            }

            var unselectedUserIds = context.OriginalSignupsForMessage[context.OriginalMessageData.MessageId]
                        .Where(id => !allSelectedIds.Contains(id));

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embeds = embeds.ToArray();
                msgProps.Components = HostHelpers.BuildTeamSelectionComponent(context.Guild, BossIndex, allSelectedIds, unselectedUserIds);
            });
        }

        protected abstract bool CanRunActionForUser(ulong userId, IEnumerable<EmbedField> fields, IEnumerable<ulong> users);
        protected abstract void RunActionForUser(ulong userId, IEnumerable<EmbedField> fields, Dictionary<string, ulong> users);
    }

    [Action("remove-user-from-team")]
    internal class RemoveUserFromTeamAction : AddOrRemoveUserFromTeamBase
    {
        public static string ActionName = "remove-user-from-team";

        public RemoveUserFromTeamAction(ILogger logger) : base(logger)
        {
        }

        protected override bool CanRunActionForUser(ulong userId, IEnumerable<EmbedField> fields, IEnumerable<ulong> users)
        {
            return users.Contains(userId);
        }

        protected override void RunActionForUser(ulong userId, IEnumerable<EmbedField> fields, Dictionary<string, ulong> users)
        {
            Logger.LogDebug($"Removing {userId} from the team");
            var field = fields.Where(field => Utility.GetUserIdFromMention(field.Value) == userId).First();
            users[field.Name] = 0;
        }
    }

    [Action("add-user-to-team")]
    internal class AddUserToTeamAction : AddOrRemoveUserFromTeamBase
    {
        public static string ActionName = "add-user-to-team";

        public AddUserToTeamAction(ILogger logger) : base(logger)
        {
        }

        protected override bool CanRunActionForUser(ulong userId, IEnumerable<EmbedField> fields, IEnumerable<ulong> users)
        {
            var emptyFields = fields.Where(field => field.Value == "None");

            return emptyFields.Any() && !users.Contains(userId);
        }

        protected override void RunActionForUser(ulong userId, IEnumerable<EmbedField> fields, Dictionary<string, ulong> users)
        {
            Logger.LogDebug($"Adding {userId} to the team");
            var firstEmpty = fields.Where(field => field.Value == "None").First();
            users[firstEmpty.Name] = userId;
        }
    }
}
