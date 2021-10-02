using Discord;
using Discord.Commands;
using WinstonBot.Services;
using WinstonBot.MessageHandlers;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Data;
using Discord.WebSocket;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace WinstonBot.Commands
{
    public class HostPvmSignup : ICommand
    {
        public string Name => "host-pvm-signup";
        public int Id => 1;
        public ICommand.Permission DefaultPermission => ICommand.Permission.Everyone;
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
            new SignupAction(),
            new QuitAction(),
            new CompleteTeamAction(),
            new ConfirmTeamAction(),
            new CancelTeamConfirmationAction(),
            new AddUserToTeamAction(),
            new RemoveUserFromTeamAction()
        };

        private static int CurrentActionId = 0;

        // we only have the mention string in the desc.
        private List<string> testNames = new List<string>()
        {
            { "<@141439679890325504>" },
            { "<@204793753691619330>" },
            { "<@889961722314637342>" },
            { "<@879404492922167346>" },
            { "<@856679611899576360>" }
        };

        private static ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> _messagesBeingEdited = new();

        public SlashCommandProperties BuildCommand()
        {
            var choices = new SlashCommandOptionBuilder()
                    .WithName("boss")
                    .WithDescription("The boss to host")
                    .WithRequired(true)
                    .WithType(ApplicationCommandOptionType.Integer);

            foreach (var entry in BossData.Entries)
            {
                choices.AddChoice(entry.CommandName, (int)entry.Id);
            }

            var hostQueuedCommand = new SlashCommandBuilder()
                .WithName(Name)
                .WithDescription("Create a signup for a pvm event")
                .AddOption(choices)
                .AddOption("message", ApplicationCommandOptionType.String, "Additional info about the event to be added to the message body.", required: false);

            return hostQueuedCommand.Build();
        }

        public async Task HandleCommand(Commands.CommandContext context)
        {
            var slashCommand = context.SlashCommand;

            var bossIndex = (long)slashCommand.Data.Options.First().Value;
            if (!BossData.ValidBossIndex(bossIndex))
            {
                await slashCommand.RespondAsync($"Invalid boss index {bossIndex}. Max Index is {(long)BossData.Boss.Count - 1}", ephemeral: true);
                return;
            }

            var bossPrettyName = BossData.Entries[bossIndex].PrettyName;
            string message = $"Sign up for {bossPrettyName}"; // default message
            if (slashCommand.Data.Options.Count > 1)
            {
                message = (string)slashCommand.Data.Options.ElementAt(1).Value;
            }

            var buttons = BuildSignupButtons(bossIndex);
            var embed = new EmbedBuilder()
                .WithTitle($"{bossPrettyName} Sign Ups")
                //TEMP
                .WithDescription(String.Join(Environment.NewLine, testNames));

            await slashCommand.RespondAsync(message, embed: embed.Build(), component: buttons, allowedMentions: AllowedMentions.All);
        }

        #region Helpers
        private static void RemoveMessageFromEditedList(ulong messageId)
        {
            ReadOnlyCollection<ulong> outList;
            _messagesBeingEdited.TryRemove(messageId, out outList);
        }

        private static bool TryAddMessageToEditedList(ulong messageId, List<ulong> names)
        {
            return _messagesBeingEdited.TryAdd(messageId, new ReadOnlyCollection<ulong>(names));
        }

        private static ulong GetUserIdFromMention(string mention)
        {
            var resultString = Regex.Match(mention, @"\d+").Value;
            ulong value = 0;
            if (ulong.TryParse(resultString, out value))
            {
                return value;
            }
            else
            {
                Console.WriteLine($"Failed to parse user id from string {mention}");
                return 0;
            }
        }

        private static List<string> ParseNamesToList(string text)
        {
            if (text != null)
            {
                return text.Split(Environment.NewLine).ToList();
            }
            return new List<string>();
        }

        private static List<ulong> ParseNamesToIdList(string text)
        {
            return ParseNamesToList(text)
                .Select(mention => GetUserIdFromMention(mention))
                .ToList();
        }

        private static List<ulong> ParseNamesToIdList(IEnumerable<string> nameList)
        {
            return nameList
                .Select(mention => GetUserIdFromMention(mention))
                .ToList();
        }

        private static List<string> ConvertUserIdListToMentions(SocketGuild guild, IEnumerable<ulong> ids)
        {
            return ids.Select(id => guild.GetUser(id).Mention).ToList();
        }

        private static Embed BuildTeamSelectionEmbed(
            ulong guildId,
            ulong channelId,
            ulong messageId,
            List<string> selectedNames)
        {
            return new EmbedBuilder()
                .WithTitle("Pending Team")
                .WithDescription(String.Join(Environment.NewLine, selectedNames))
                .WithFooter($"{guildId},{channelId},{messageId}")
                .Build();
        }

        private static MessageComponent BuildTeamSelectionComponent(
            SocketGuild guild,
            long bossIndex,
            List<string> selectedNames,
            List<string> unselectedNames,
            bool includeCancelButton = true)
        {
            var builder = new ComponentBuilder();
            foreach (var mention in selectedNames)
            {
                ulong userid = GetUserIdFromMention(mention);
                var username = guild.GetUser(userid).Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"❌ {username}")
                    .WithCustomId($"{RemoveUserFromTeamAction.ActionName}_{mention}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger));
            }

            foreach (var mention in unselectedNames)
            {
                var username = guild.GetUser(GetUserIdFromMention(mention)).Username;
                builder.WithButton(new ButtonBuilder()
                    .WithLabel($"{username}")
                    .WithEmote(new Emoji("➕"))
                    .WithCustomId($"{AddUserToTeamAction.ActionName}_{mention}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"{ConfirmTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            if (includeCancelButton)
            {
                builder.WithButton(new ButtonBuilder()
                        .WithLabel("Cancel")
                        .WithCustomId($"{CancelTeamConfirmationAction.ActionName}_{bossIndex}")
                        .WithStyle(ButtonStyle.Danger));
            }

            return builder.Build();
        }

        private static MessageComponent BuildSignupButtons(long bossIndex, bool disabled = false)
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

        private static Embed BuildSignupEmbed(long bossIndex, IEnumerable<string> names, string? editedByMention = null)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"{BossData.Entries[bossIndex].PrettyName} Sign Ups")
                .WithDescription(String.Join(Environment.NewLine, names));
            if (editedByMention != null)
            {
                builder.WithFooter($"Being edited by {editedByMention}");
            }
            return builder.Build();
        }

        private class MessageMetadata
        {
            public ulong GuildId { get; set; }
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
        }

        private static MessageMetadata ParseMetadata(DiscordSocketClient client, string text)
        {
            var footerParts = text.Split(',');
            var guildId = ulong.Parse(footerParts[0]);
            var channelId = ulong.Parse(footerParts[1]);
            var originalMessageId = ulong.Parse(footerParts[2]);

            return new MessageMetadata()
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = originalMessageId
            };
        }
        #endregion // helpers

        #region actions
        private class SignupAction : IAction
        {
            public static string ActionName = "pvm-team-signup";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;

                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                var names = ParseNamesToList(currentEmbed.Description);
                var ids = ParseNamesToIdList(names);
                if (ids.Contains(component.User.Id))
                {
                    Console.WriteLine($"{component.User.Mention} is already signed up: ignoring.");
                    await component.RespondAsync("You're already signed up.", ephemeral: true);
                    return;
                }

                // TODO: handle checking they have the correct role.
                Console.WriteLine($"{component.User.Mention} has signed up!");
                names.Add(component.User.Mention);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildSignupEmbed(bossIndex, names);
                });
            }
        }

        private class QuitAction : IAction
        {
            public static string ActionName = "pvm-quit-signup";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                var names = ParseNamesToList(currentEmbed.Description);
                var ids = ParseNamesToIdList(names);
                if (!ids.Contains(component.User.Id))
                {
                    Console.WriteLine($"{component.User.Mention} isn't signed up: ignoring.");
                    await component.RespondAsync("You're not signed up.", ephemeral: true);
                    return;
                }

                Console.WriteLine($"{component.User.Mention} has quit!");
                var index = ids.IndexOf(component.User.Id);
                names.RemoveAt(index);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildSignupEmbed(bossIndex, names);
                });
            }
        }

        private class CompleteTeamAction : IAction
        {
            public static string ActionName = "pvm-complete-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                var names = ParseNamesToList(currentEmbed.Description);
                if (names.Count == 0)
                {
                    await component.RespondAsync("Not enough people signed up.", ephemeral: true);
                    return;
                }

                if (!TryAddMessageToEditedList(component.Message.Id, ParseNamesToIdList(names)))
                {
                    await component.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                    return;
                }

                BossData.Entry bossData = BossData.Entries[bossIndex];

                // TODO: calculate who should go.
                List<string> selectedNames = new();
                List<string> unselectedNames = new();
                int i = 0;
                foreach (var mention in names)
                {
                    if (i++ < bossData.MaxPlayersOnTeam) selectedNames.Add(mention);
                    else unselectedNames.Add(mention);
                }

                await component.Message.ModifyAsync(msgProps =>
                {
                    msgProps.Components = BuildSignupButtons(bossIndex, true);
                    msgProps.Content = "Host is finalizing the team, fuck off."; // todo
                    // footers can't show mentions, so use the username.
                    msgProps.Embed = BuildSignupEmbed(bossIndex, names, component.User.Username);
                });

                var guild = ((SocketGuildChannel)component.Channel).Guild;
                await component.User.SendMessageAsync("Confirm or edit the team." +
                    "\nClick the buttons to change who is selected to go." +
                    "\nOnce you're done click Confirm Team." +
                    "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                    "\nOnce you're finished making changes you can dismiss this message.",
                    embed: BuildTeamSelectionEmbed(guild.Id, component.Channel.Id, component.Message.Id, selectedNames),
                    component: BuildTeamSelectionComponent(guild, bossIndex, selectedNames, unselectedNames));

                await component.DeferAsync();
            }
        }

        private class ConfirmTeamAction : IAction
        {
            public static string ActionName = "pvm-confirm-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;

                var currentEmbed = component.Message.Embeds.First();
                if (!currentEmbed.Footer.HasValue)
                {
                    throw new NullReferenceException($"Footer for message {component.Message.Id} is null. Failed to get metadata");
                }

                var selectedNames = ParseNamesToList(currentEmbed.Description);

                var embed = new EmbedBuilder()
                            .WithTitle("Selected Team")
                            .WithDescription(String.Join(Environment.NewLine, selectedNames));

                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);

                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var bossData = BossData.Entries[bossIndex];

                await channel.ModifyMessageAsync(metadata.MessageId, msgProps =>
                {
                    msgProps.Content = $"Final team for {bossData.PrettyName}";
                    msgProps.Embed = embed.Build();
                    msgProps.Components = new ComponentBuilder().Build();
                });

                var builder = ComponentBuilder.FromComponents(component.Message.Components);

                // Remove the cancel button from the edit message.
                await component.Message.ModifyAsync(msgProps =>
                {
                    foreach (var row in builder.ActionRows)
                    {
                        foreach (var component in row.Components)
                        {
                            if (component.CustomId.StartsWith(CancelTeamConfirmationAction.ActionName))
                            {
                                row.Components.Remove(component);
                                break;
                            }
                        }
                    }
                    msgProps.Components = builder.Build();
                });

                // Even though this is a DM, make it ephemeral so they can dismiss it as they can't delete the messages in DM.
                await component.RespondAsync("Team updated in original message.\n\n" +
                    "Feel free to continue making edits and click 'Confirm Team' again to update.",
                    ephemeral:true);
            }
        }

        private class CancelTeamConfirmationAction : IAction
        {
            public static string ActionName = "pvm-cancel-team-confirmation";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;

                var currentEmbed = component.Message.Embeds.First();
                if (!currentEmbed.Footer.HasValue)
                {
                    throw new NullReferenceException($"Footer for message {component.Message.Id} is null. Failed to get metadata");
                }

                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);

                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var bossData = BossData.Entries[bossIndex];

                var names = new List<string>();
                ReadOnlyCollection<ulong> ids;
                if (_messagesBeingEdited.TryRemove(metadata.MessageId, out ids))
                {
                    names = ConvertUserIdListToMentions(guild, ids);
                }
                else
                {
                    Console.WriteLine($"[Cancel Team Confirmation] Failed to find message data for {metadata.MessageId}. Cannot remove 'edited by' footer.");
                }

                await channel.ModifyMessageAsync(metadata.MessageId, msgProps =>
                {
                    msgProps.Components = BuildSignupButtons(bossIndex);
                    msgProps.Content = $"Sign up for {bossData.PrettyName}";
                    // Only update the embed if we have the names otherwise we'll end up clearing them.
                    if (names.Count > 0)
                    {
                        msgProps.Embed = BuildSignupEmbed(bossIndex, names);
                    }
                });

                // Delete the edit team message from the DM
                await component.Message.DeleteAsync();

                // Ack the interaction so they don't see "interaction failed" after hitting complete team.
                await component.DeferAsync();
            }
        }

        private class RemoveUserFromTeamAction : IAction
        {
            public static string ActionName = "remove-user-from-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;
                string mention = component.Data.CustomId.Split('_')[1];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[2]);

                var currentEmbed = component.Message.Embeds.First();
                if (!currentEmbed.Footer.HasValue)
                {
                    throw new NullReferenceException($"Footer for message {component.Message.Id} is null. Failed to get metadata");
                }

                ulong userId = GetUserIdFromMention(mention);
                var ids = ParseNamesToIdList(currentEmbed.Description);
                if (!ids.Contains(userId))
                {
                    return;
                }

                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);

                Console.WriteLine($"Removing {mention} from the team");
                ids.Remove(userId);
                var selectedNames = ConvertUserIdListToMentions(guild, ids);

                if (!_messagesBeingEdited.ContainsKey(metadata.MessageId))
                {
                    await component.RespondAsync($"No user data could be found for message {metadata.MessageId}.\n" +
                        $"This may be because the bot restarted while you were editing.\n" +
                        $"Please click 'Confirm Team' on the original message to try again.",
                        ephemeral: true);
                    return;
                }

                var unselectedUserIds = _messagesBeingEdited[metadata.MessageId]
                    .Where(id => !ids.Contains(id));
                var unselectedNames = ConvertUserIdListToMentions(guild, unselectedUserIds);

                await component.UpdateAsync(msgProps =>
                {
                    // TODO: hide cancel button if we've confirmed before
                    // TODO: add revert all changes button since we can do that now.
                    msgProps.Embed = BuildTeamSelectionEmbed(metadata.GuildId, metadata.ChannelId, metadata.MessageId, selectedNames);
                    msgProps.Components = BuildTeamSelectionComponent(guild, bossIndex, selectedNames, unselectedNames);
                });
            }
        }

        private class AddUserToTeamAction : IAction
        {
            public static string ActionName = "add-user-to-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;
                string mention = component.Data.CustomId.Split('_')[1];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[2]);

                var currentEmbed = component.Message.Embeds.First();
                if (!currentEmbed.Footer.HasValue)
                {
                    throw new NullReferenceException($"Footer for message {component.Message.Id} is null. Failed to get metadata");
                }

                ulong userId = GetUserIdFromMention(mention);
                var selectedNames = ParseNamesToList(currentEmbed.Description);
                var ids = ParseNamesToIdList(selectedNames);
                if (ids.Contains(userId))
                {
                    return;
                }

                BossData.Entry bossData = BossData.Entries[bossIndex];
                if (selectedNames.Count == bossData.MaxPlayersOnTeam)
                {
                    await component.RespondAsync("Cannot add user to team as the team is full. Please remove someone first.", ephemeral: true);
                    return;
                }

                Console.WriteLine($"Adding {mention} to the team");
                selectedNames.Add(mention);
                ids.Add(userId);

                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);

                if (!_messagesBeingEdited.ContainsKey(metadata.MessageId))
                {
                    await component.RespondAsync($"No user data could be found for message {metadata.MessageId}.\n" +
                        $"This may be because the bot restarted while you were editing.\n" +
                        $"Please click 'Confirm Team' on the original message to try again.",
                        ephemeral:true);
                    return;
                }

                var unselectedUserIds = _messagesBeingEdited[metadata.MessageId]
                    .Where(id => !ids.Contains(id));
                var unselectedNames = ConvertUserIdListToMentions(guild, unselectedUserIds);

                await component.UpdateAsync(msgProps =>
                {
                    // TODO: hide cancel button if we've confirmed before
                    msgProps.Embed = BuildTeamSelectionEmbed(metadata.GuildId, metadata.ChannelId, metadata.MessageId, selectedNames);
                    msgProps.Components = BuildTeamSelectionComponent(guild, bossIndex, selectedNames, unselectedNames);
                });
            }
        }
        #endregion //actions
    }
}
