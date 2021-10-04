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
            new EditCompletedTeamAction(),
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

        private static ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> _originalSignupsForMessage = new();
        private static ConcurrentDictionary<ulong, bool> _messagesBeingEdited = new();

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
            var embed = BuildSignupEmbed(bossIndex, testNames);

            await slashCommand.RespondAsync(message, embed: embed, component: buttons, allowedMentions: AllowedMentions.All);
        }

        #region Helpers
        private static void EditFinishedForMessage(ulong messageId)
        {
            //ReadOnlyCollection<ulong> outList;
            bool outVal;
            _messagesBeingEdited.TryRemove(messageId, out outVal);
        }

        private static bool TryMarkMessageForEdit(ulong messageId, List<ulong>? names = null)
        {
            if (_messagesBeingEdited.TryAdd(messageId, true))
            {
                if (names != null)
                {
                    _originalSignupsForMessage.TryAdd(messageId, new ReadOnlyCollection<ulong>(names));
                }
                return true;
            }
            return false;
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
            bool confirmedBefore,
            List<string> selectedNames)
        {
            return new EmbedBuilder()
                .WithTitle("Pending Team")
                .WithDescription(String.Join(Environment.NewLine, selectedNames))
                .WithFooter($"{guildId},{channelId},{messageId},{confirmedBefore}")
                .Build();
        }

        private static MessageComponent BuildTeamSelectionComponent(
            SocketGuild guild,
            long bossIndex,
            List<string> selectedNames,
            List<string> unselectedNames)
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

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Cancel")
                    .WithCustomId($"{CancelTeamConfirmationAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger));

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

        private static MessageComponent BuildEditButton(long bossIndex, bool disabled = false)
        {
            return new ComponentBuilder()
                .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{bossIndex}", ButtonStyle.Danger, disabled:disabled)
                .Build();
        }

        private static EmbedBuilder CreateBuilderForEmbed(IEmbed source)
        {
            var builder = new EmbedBuilder()
                .WithTitle(source.Title)
                .WithDescription(source.Description)
                .WithUrl(source.Url);
            if (source.Timestamp != null) builder.WithTimestamp(source.Timestamp.Value);
            if (source.Color != null) builder.WithColor(source.Color.Value);
            if (source.Image != null) builder.WithImageUrl(source.Image.Value.Url);
            if (source.Thumbnail != null) builder.WithThumbnailUrl(source.Thumbnail.Value.Url);
            var fields = source.Fields.Select(field =>
            {
                return new EmbedFieldBuilder()
                    .WithName(field.Name)
                    .WithValue(field.Value)
                    .WithIsInline(field.Inline);

            });
            builder.WithFields(fields.ToArray());

            if (source.Author != null)
            {
                builder.WithAuthor(source.Author.Value.Name, source.Author.Value.IconUrl, source.Author.Value.Url);
            }
            if (source.Footer != null)
            {
                builder.WithFooter(source.Footer.Value.Text, source.Footer.Value.IconUrl);
            }
            return builder;
        }

        private static Embed BuildSignupEmbed(long bossIndex, IEnumerable<string> names, string? editedByMention = null)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"{BossData.Entries[bossIndex].PrettyName} Sign Ups")
                .WithDescription(String.Join(Environment.NewLine, names))
                .WithCurrentTimestamp(); // TODO: include event start timestamp
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
            public bool TeamConfirmedBefore { get; set; }
        }

        private static MessageMetadata ParseMetadata(DiscordSocketClient client, string text)
        {
            var footerParts = text.Split(',');
            var guildId = ulong.Parse(footerParts[0]);
            var channelId = ulong.Parse(footerParts[1]);
            var originalMessageId = ulong.Parse(footerParts[2]);
            var confirmedBefore = bool.Parse(footerParts[3]);

            return new MessageMetadata()
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = originalMessageId,
                TeamConfirmedBefore = confirmedBefore
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
                if (!component.Message.Embeds.Any())
                {
                    await component.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                    return;
                }

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

                if (!component.Message.Embeds.Any())
                {
                    await component.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                    return;
                }
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

                if (!component.Message.Embeds.Any())
                {
                    await component.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                    return;
                }
                var currentEmbed = component.Message.Embeds.First();

                var names = ParseNamesToList(currentEmbed.Description);
                if (names.Count == 0)
                {
                    await component.RespondAsync("Not enough people signed up.", ephemeral: true);
                    return;
                }

                bool hasBeenConfirmedBefore = currentEmbed.Footer.HasValue;

                if (!TryMarkMessageForEdit(component.Message.Id, ParseNamesToIdList(names)))
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
                    embed: BuildTeamSelectionEmbed(guild.Id, component.Channel.Id, component.Message.Id, hasBeenConfirmedBefore, selectedNames),
                    component: BuildTeamSelectionComponent(guild, bossIndex, selectedNames, unselectedNames));

                await component.DeferAsync();
            }
        }

        private class EditCompletedTeamAction : IAction
        {
            public static string ActionName = "pvm-edit-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(ActionContext context)
            {
                var component = context.Component;
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);

                if (!component.Message.Embeds.Any())
                {
                    await component.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral:true);
                    return;
                }
                var currentEmbed = component.Message.Embeds.First();

                if (!TryMarkMessageForEdit(component.Message.Id))
                {
                    await component.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                    return;
                }

                var guild = ((SocketGuildChannel)component.Channel).Guild;
                var selectedNames = ParseNamesToList(currentEmbed.Description);
                if (selectedNames.Count == 0)
                {
                    await component.RespondAsync("Not enough people signed up.", ephemeral: true);
                    return;
                }

                var allNames = new List<string>();
                if (_originalSignupsForMessage.ContainsKey(component.Message.Id))
                {
                    allNames = ConvertUserIdListToMentions(guild, _originalSignupsForMessage[component.Message.Id]);
                }
                else
                {
                    Console.WriteLine($"[EditCompletedTeamAction] Failed to find message data for {component.Message.Id}. Cannot retrieve original names.");
                }

                BossData.Entry bossData = BossData.Entries[bossIndex];

                List<string> unselectedNames = allNames
                    .Where(name => !selectedNames.Contains(name))
                    .ToList();

                await component.Message.ModifyAsync(msgProps =>
                {
                    msgProps.Content = "Host is finalizing the team, fuck off."; // todo
                    //msgProps.Embed = BuildSignupEmbed(bossIndex, selectedNames, component.User.Username);
                    msgProps.Embed = CreateBuilderForEmbed(currentEmbed)
                    .WithFooter($"Being edited by {component.User.Username}")
                    .Build();
                    msgProps.Components = new ComponentBuilder()
                        .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{bossIndex}", ButtonStyle.Danger, disabled:true)
                        .Build();
                });

                await component.User.SendMessageAsync("Confirm or edit the team." +
                    "\nClick the buttons to change who is selected to go." +
                    "\nOnce you're done click Confirm Team." +
                    "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                    "\nOnce you're finished making changes you can dismiss this message.",
                    embed: BuildTeamSelectionEmbed(guild.Id, component.Channel.Id, component.Message.Id, true, selectedNames),
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
                            .WithFooter($"Finalized by {component.User.Username}")
                            .WithDescription(String.Join(Environment.NewLine, selectedNames));

                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);
                if (originalMessage == null)
                {
                    // This can happen if the original message is deleted but the edit window is still open.
                    await component.RespondAsync("Failed to find the original message this interaction was created from.", ephemeral: true);
                    return;
                }

                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var bossData = BossData.Entries[bossIndex];

                // TODO: ping the people that are going.
                // Should that be a separate message or should we just not use an embed for this?
                await channel.ModifyMessageAsync(metadata.MessageId, msgProps =>
                {
                    msgProps.Content = $"Final team for {bossData.PrettyName}";
                    msgProps.Embed = embed.Build();
                    msgProps.Components = new ComponentBuilder()
                        .WithButton("Edit", $"{EditCompletedTeamAction.ActionName}_{bossIndex}", ButtonStyle.Danger)
                        .Build();
                });

                var builder = ComponentBuilder.FromComponents(component.Message.Components);

                EditFinishedForMessage(metadata.MessageId);

                // Delete the edit team message from the DM
                await component.Message.DeleteAsync();

                // Even though this is a DM, make it ephemeral so they can dismiss it as they can't delete the messages in DM.
                await component.RespondAsync("Team updated in original message.", ephemeral: true);
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
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);
                if (originalMessage == null)
                {
                    // This can happen if the original message is deleted but the edit window is still open.
                    await component.RespondAsync("Failed to find the original message this interaction was created from.", ephemeral: true);
                    return;
                }

                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var bossData = BossData.Entries[bossIndex];
                var names = ParseNamesToList(originalMessage.Embeds.First().Description);

                await channel.ModifyMessageAsync(metadata.MessageId, msgProps =>
                {
                    msgProps.Content = metadata.TeamConfirmedBefore
                    ? $"Final team for {bossData.PrettyName}"
                    : $"Sign up for {bossData.PrettyName}";
                    // TODO: make ICommand function to create the action context so we can include the boss index, metadata etc to reduce duplication.
                    //      can also include helpers here or something.
                    if (!metadata.TeamConfirmedBefore)
                    {
                        msgProps.Embed = BuildSignupEmbed(bossIndex, names);
                    }
                    //msgProps.Embed = BuildSignupEmbed(bossIndex, names);
                    msgProps.Components = metadata.TeamConfirmedBefore
                        ? BuildEditButton(bossIndex, false)
                        : BuildSignupButtons(bossIndex);
                });

                EditFinishedForMessage(metadata.MessageId);

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
                var currentEmbed = component.Message.Embeds.First();
                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);
                if (originalMessage == null)
                {
                    await component.RespondAsync("The original message this interaction was created from could not be found.", ephemeral: true);
                    return;
                }

                string mention = component.Data.CustomId.Split('_')[1];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[2]);

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

                Console.WriteLine($"Removing {mention} from the team");
                ids.Remove(userId);
                var selectedNames = ConvertUserIdListToMentions(guild, ids);

                if (!_originalSignupsForMessage.ContainsKey(metadata.MessageId))
                {
                    await component.RespondAsync($"No user data could be found for message {metadata.MessageId}.\n" +
                        $"This may be because the bot restarted while you were editing.\n" +
                        $"Please click 'Confirm Team' on the original message to try again.",
                        ephemeral: true);
                    return;
                }

                var unselectedUserIds = _originalSignupsForMessage[metadata.MessageId]
                    .Where(id => !ids.Contains(id));
                var unselectedNames = ConvertUserIdListToMentions(guild, unselectedUserIds);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildTeamSelectionEmbed(metadata.GuildId, metadata.ChannelId, metadata.MessageId, metadata.TeamConfirmedBefore, selectedNames);
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

                var currentEmbed = component.Message.Embeds.First();
                var metadata = ParseMetadata(context.Client, currentEmbed.Footer.Value.Text);
                var guild = context.Client.GetGuild(metadata.GuildId);
                var channel = guild.GetTextChannel(metadata.ChannelId);
                var originalMessage = await channel.GetMessageAsync(metadata.MessageId);
                if (originalMessage == null)
                {
                    await component.RespondAsync("The original message this interaction was created from could not be found.", ephemeral: true);
                    return;
                }

                string mention = component.Data.CustomId.Split('_')[1];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[2]);

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

                if (!_originalSignupsForMessage.ContainsKey(metadata.MessageId))
                {
                    await component.RespondAsync($"No user data could be found for message {metadata.MessageId}.\n" +
                        $"This may be because the bot restarted while you were editing.\n" +
                        $"Please click 'Confirm Team' on the original message to try again.",
                        ephemeral:true);
                    return;
                }

                var unselectedUserIds = _originalSignupsForMessage[metadata.MessageId]
                    .Where(id => !ids.Contains(id));
                var unselectedNames = ConvertUserIdListToMentions(guild, unselectedUserIds);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildTeamSelectionEmbed(metadata.GuildId, metadata.ChannelId, metadata.MessageId, metadata.TeamConfirmedBefore, selectedNames);
                    msgProps.Components = BuildTeamSelectionComponent(guild, bossIndex, selectedNames, unselectedNames);
                });
            }
        }
        #endregion //actions
    }
}
