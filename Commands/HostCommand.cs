using Discord;
using Discord.Commands;
using WinstonBot.Services;
using WinstonBot.MessageHandlers;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Data;
using Discord.WebSocket;
using System.Diagnostics;

namespace WinstonBot.Commands
{
    public class HostPvmSignup : ICommand
    {
        public string Name => "host-pvm-signup";
        public int Id => 1;
        public IEnumerable<IAction> Actions => _actions;

        private List<IAction> _actions = new List<IAction>()
        {
            new SignupAction(),
            new QuitAction(),
            new CompleteTeamAction(),
            new ConfirmTeamAction(),
            new AddUserToTeamAction(),
            new RemoveUserFromTeamAction()
        };

        private static int CurrentActionId = 0;

        private Dictionary<string, string> testNames = new Dictionary<string, string>()
        {
            { "1vinnie", "vinnie" },
            { "1bob", "bob" },
            { "1joe", "joe" },
            { "1aaron", "aaron" },
            { "1kadeem", "kadeem" },
            { "1mike", "mike" },
            { "1fuccboi", "fuccboi" },
            { "1feerip", "feerip" },
            { "1gob", "gob" },
            { "1bog", "bog" },
        };

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

        public async Task HandleCommand(SocketSlashCommand slashCommand)
        {
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

            var builder = new ComponentBuilder()
                .WithButton("Sign Up", $"{SignupAction.ActionName}_{bossIndex}")
                .WithButton(new ButtonBuilder()
                    .WithLabel("Quit")
                    .WithCustomId($"{QuitAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger))
                .WithButton(new ButtonBuilder()
                    .WithLabel("Complete Team")
                    .WithCustomId($"{CompleteTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));

            var embed = new EmbedBuilder()
                .WithTitle($"{bossPrettyName} Sign Ups")
                //TEMP
                .WithDescription(String.Join(Environment.NewLine, DictToList(testNames)));

            await slashCommand.RespondAsync(message, embed: embed.Build(), component: builder.Build());
        }

        #region Helpers
        private static List<string> DictToList(Dictionary<string, string> names)
        {
            List<string> nameList = new();
            foreach (var pair in names)
            {
                nameList.Add($"{pair.Key} - {pair.Value}");
            }
            return nameList;
        }

        private static Dictionary<string, string> ParseNamesToDict(string text)
        {
            Dictionary<string, string> names = new();
            if (text != null)
            {
                var lines = text.Split(Environment.NewLine).ToList();
                lines.ForEach(line => names.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }
            return names;
        }

        private static Embed BuildTeamSelectionEmbed(
            Dictionary<string, string> selectedNames,
            Dictionary<string, string> unselectedNames)
        {
            return new EmbedBuilder()
                .WithTitle("Pending Team")
                .AddField("Selected Users", String.Join(Environment.NewLine, DictToList(selectedNames)), inline: true)
                .AddField("Unselected Users", String.Join(Environment.NewLine, DictToList(unselectedNames)), inline: true)
                .Build();
        }

        private static MessageComponent BuildTeamSelectionComponent(
            long bossIndex,
            Dictionary<string, string> selectedNames,
            Dictionary<string, string> unselectedNames)
        {
            var builder = new ComponentBuilder();
            foreach (var namePair in selectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"{RemoveUserFromTeamAction.ActionName}_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"{AddUserToTeamAction.ActionName}_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"{ConfirmTeamAction.ActionName}_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            return builder.Build();
        }
        #endregion // helpers

        #region actions
        private class SignupAction : IAction
        {
            public static string ActionName = "pvm-team-signup";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(SocketMessageComponent component)
            {
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                Dictionary<string, string> names = ParseNamesToDict(currentEmbed.Description);
                if (names.ContainsKey(component.User.Mention))
                {
                    Console.WriteLine($"{component.User.Mention} is already signed up: ignoring.");
                    await component.RespondAsync("You're already signed up.", ephemeral: true);
                    return;
                }

                // TODO: handle checking they have the correct role.
                Console.WriteLine($"{component.User.Mention} has signed up!");
                names.Add(component.User.Mention, component.User.Username);

                var embed = new EmbedBuilder()
                            .WithTitle("Sign Ups")
                            .WithDescription(String.Join(Environment.NewLine, DictToList(names)));

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = embed.Build();
                });
            }
        }

        private class QuitAction : IAction
        {
            public static string ActionName = "pvm-quit-signup";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(SocketMessageComponent component)
            {
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                Dictionary<string, string> names = ParseNamesToDict(currentEmbed.Description);
                if (!names.ContainsKey(component.User.Mention))
                {
                    Console.WriteLine($"{component.User.Mention} isn't signed up: ignoring.");
                    await component.RespondAsync("You're not signed up.", ephemeral: true);
                    return;
                }

                Console.WriteLine($"{component.User.Mention} has quit!");
                names.Remove(component.User.Mention);

                var embed = new EmbedBuilder()
                            .WithTitle("Sign Ups")
                            .WithDescription(String.Join(Environment.NewLine, DictToList(names)));

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = embed.Build();
                });
            }
        }

        private class CompleteTeamAction : IAction
        {
            public static string ActionName = "pvm-complete-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(SocketMessageComponent component)
            {
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);
                var currentEmbed = component.Message.Embeds.First();

                Dictionary<string, string> names = ParseNamesToDict(currentEmbed.Description);
                if (names.Count == 0)
                {
                    await component.RespondAsync("Not enough people signed up.", ephemeral: true);
                    return;
                }

                BossData.Entry bossData = BossData.Entries[bossIndex];

                // TODO: calculate who should go.
                Dictionary<string, string> selectedNames = new();
                Dictionary<string, string> unselectedNames = new();
                int i = 0;
                foreach (var pair in names)
                {
                    if (i++ < bossData.MaxPlayersOnTeam) selectedNames.Add(pair.Key, pair.Value);
                    else unselectedNames.Add(pair.Key, pair.Value);
                }

                await component.RespondAsync("Confirm or edit the team." +
                    "\nClick the buttons to change who is selected to go." +
                    "\nOnce you're done click Confirm Team." +
                    "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                    "\nOnce you're finished making changes you can dismiss this message.",
                    embed: BuildTeamSelectionEmbed(selectedNames, unselectedNames),
                    component: BuildTeamSelectionComponent(bossIndex, selectedNames, unselectedNames),
                    ephemeral: true);
            }
        }

        private class ConfirmTeamAction : IAction
        {
            public static string ActionName = "pvm-confirm-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(SocketMessageComponent component)
            {
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[1]);

                var currentEmbed = component.Message.Embeds.First();
                Debug.Assert(currentEmbed.Fields.Length == 2);
                Dictionary<string, string> selectedNames = ParseNamesToDict(currentEmbed.Fields[0].Value);

                var embed = new EmbedBuilder()
                            .WithTitle("Selected Team")
                            .WithDescription(String.Join(Environment.NewLine, DictToList(selectedNames)));

                // We no longer have the full list of people signed up so we can't edit.
                // We'd need to include the people who weren't selected as well, but we wouldn't want to ping them. Only add it if really needed.

                var bossData = BossData.Entries[bossIndex];
                await component.Channel.ModifyMessageAsync(component.Message.Reference.MessageId.Value, msgProps =>
                {
                    msgProps.Content = $"Final team for {bossData.PrettyName}";
                    msgProps.Embed = embed.Build();
                    msgProps.Components = new ComponentBuilder().Build();
                });

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

            public async Task HandleAction(SocketMessageComponent component)
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[3]);

                var currentEmbed = component.Message.Embeds.First();

                Debug.Assert(currentEmbed.Fields.Length == 2);
                Dictionary<string, string> selectedNames = ParseNamesToDict(currentEmbed.Fields[0].Value);
                Dictionary<string, string> unselectedNames = ParseNamesToDict(currentEmbed.Fields[1].Value);

                if (!selectedNames.ContainsKey(mention))
                {
                    return;
                }

                Console.WriteLine($"Removing {username} from the team");
                selectedNames.Remove(mention);
                unselectedNames.Add(mention, username);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildTeamSelectionEmbed(selectedNames, unselectedNames);
                    msgProps.Components = BuildTeamSelectionComponent(bossIndex, selectedNames, unselectedNames);
                });
            }
        }

        private class AddUserToTeamAction : IAction
        {
            public static string ActionName = "add-user-to-team";
            public string Name => ActionName;
            public int Id { get; } = CurrentActionId++;
            public long RoleId => throw new NotImplementedException();

            public async Task HandleAction(SocketMessageComponent component)
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                long bossIndex = long.Parse(component.Data.CustomId.Split('_')[3]);

                var currentEmbed = component.Message.Embeds.First();

                Debug.Assert(currentEmbed.Fields.Length == 2);
                Dictionary<string, string> selectedNames = ParseNamesToDict(currentEmbed.Fields[0].Value);
                Dictionary<string, string> unselectedNames = ParseNamesToDict(currentEmbed.Fields[1].Value);

                if (selectedNames.ContainsKey(mention))
                {
                    return;
                }

                BossData.Entry bossData = BossData.Entries[bossIndex];
                if (selectedNames.Count == bossData.MaxPlayersOnTeam)
                {
                    await component.RespondAsync("Cannot add user to team as the team is full. Please remove someone first.", ephemeral: true);
                    return;
                }

                Console.WriteLine($"Adding {username} to the team");
                selectedNames.Add(mention, username);
                unselectedNames.Remove(mention);

                await component.UpdateAsync(msgProps =>
                {
                    msgProps.Embed = BuildTeamSelectionEmbed(selectedNames, unselectedNames);
                    msgProps.Components = BuildTeamSelectionComponent(bossIndex, selectedNames, unselectedNames);
                });
            }
        }
        #endregion //actions
    }
}
