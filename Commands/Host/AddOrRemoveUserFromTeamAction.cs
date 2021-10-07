namespace WinstonBot.Commands
{
    internal abstract class AddOrRemoveUserFromTeamBase : IAction
    {
        public abstract string Name { get; }

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;
            if (context.OriginalMessageData == null || !context.IsMessageDataValid)
            {
                await context.RespondAsync("The original message this interaction was created from could not be found.", ephemeral: true);
                return;
            }

            var currentEmbed = context.Message.Embeds.First();
            string mention = context.Data.CustomId.Split('_')[2];

            ulong userId = Utility.GetUserIdFromMention(mention);
            var ids = HostHelpers.ParseNamesToIdList(currentEmbed.Description);
            if (!CanRunActionForUser(userId, ids))
            {
                return;
            }

            if (!context.OriginalSignupsForMessage.ContainsKey(context.OriginalMessageData.MessageId))
            {
                await context.RespondAsync($"No user data could be found for message {context.OriginalMessageData.MessageId}.\n" +
                    $"This may be because the bot restarted while you were editing.\n" +
                    $"Please click 'Confirm Team' on the original message to try again.",
                    ephemeral: true);
                return;
            }

            ids = RunActionForUser(userId, mention, ids);
            var selectedNames = Utility.ConvertUserIdListToMentions(context.Guild, ids);

            var unselectedUserIds = context.OriginalSignupsForMessage[context.OriginalMessageData.MessageId]
                .Where(id => !ids.Contains(id));
            var unselectedNames = Utility.ConvertUserIdListToMentions(context.Guild, unselectedUserIds);

            await context.UpdateAsync(msgProps =>
            {
                msgProps.Embed = HostHelpers.BuildTeamSelectionEmbed(
                    context.OriginalMessageData.GuildId,
                    context.OriginalMessageData.ChannelId,
                    context.OriginalMessageData.MessageId,
                    context.OriginalMessageData.TeamConfirmedBefore,
                    context.BossEntry,
                    selectedNames);
                msgProps.Components = HostHelpers.BuildTeamSelectionComponent(context.Guild, context.BossIndex, selectedNames, unselectedNames);
            });
        }

        protected abstract bool CanRunActionForUser(ulong userId, IReadOnlyCollection<ulong> users);
        protected abstract List<ulong> RunActionForUser(ulong userId, string mention, List<ulong> users);
    }

    internal class RemoveUserFromTeamAction : AddOrRemoveUserFromTeamBase
    {
        public static string ActionName = "remove-user-from-team";
        public override string Name => ActionName;

        protected override bool CanRunActionForUser(ulong userId, IReadOnlyCollection<ulong> users)
        {
            return users.Contains(userId);
        }

        protected override List<ulong> RunActionForUser(ulong userId, string mention, List<ulong> users)
        {
            Console.WriteLine($"Removing {mention} to the team");
            users.Remove(userId);
            return users;
        }
    }

    internal class AddUserToTeamAction : AddOrRemoveUserFromTeamBase
    {
        public static string ActionName = "add-user-to-team";
        public override string Name => ActionName;

        protected override bool CanRunActionForUser(ulong userId, IReadOnlyCollection<ulong> users)
        {
            return !users.Contains(userId);
        }

        protected override List<ulong> RunActionForUser(ulong userId, string mention, List<ulong> users)
        {
            Console.WriteLine($"Adding {mention} to the team");
            users.Add(userId);
            return users;
        }
    }
}
