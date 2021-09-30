using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WinstonBot.Services;
using WinstonBot.Data;
using System.Diagnostics;

namespace WinstonBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private IServiceProvider _services;

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(IServiceProvider services, DiscordSocketClient client)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = client;
            _services = services;
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.ButtonExecuted += HandleButtonExecuted;
            _client.InteractionCreated += HandleInteractionCreated;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: _services);
        }

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

        private List<string> DictToList(Dictionary<string, string> names)
        {
            List<string> nameList = new();
            foreach (var pair in names)
            {
                nameList.Add($"{pair.Key} - {pair.Value}");
            }
            return nameList;
        }

        private Dictionary<string, string> ParseNamesToDict(string text)
        {
            Dictionary<string, string> names = new();
            if (text != null)
            {
                var lines = text.Split(Environment.NewLine).ToList();
                lines.ForEach(line => names.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }
            return names;
        }

        private async Task HandleInteractionCreated(SocketInteraction arg)
        {
            if (arg is SocketSlashCommand command)
            {
                if (command.Data.Name == "host-pvm-signup")
                {
                    var bossIndex = (long)command.Data.Options.First().Value;
                    if (!BossData.ValidBossIndex(bossIndex))
                    {
                        await arg.RespondAsync($"Invalid boss index {bossIndex}. Max Index is {(long)BossData.Boss.Count - 1}", ephemeral: true);
                        return;
                    }

                    var bossPrettyName = BossData.Entries[bossIndex].PrettyName;
                    string message = $"Sign up for {bossPrettyName}"; // default message
                    if (command.Data.Options.Count > 1)
                    {
                        message = (string)command.Data.Options.ElementAt(1).Value;
                    }

                    var builder = new ComponentBuilder()
                        .WithButton("Sign Up", $"pvm-team-signup_{bossIndex}")
                        .WithButton(new ButtonBuilder()
                            .WithLabel("Quit")
                            .WithCustomId($"pvm-quit-signup_{bossIndex}")
                            .WithStyle(ButtonStyle.Danger))
                        .WithButton(new ButtonBuilder()
                            .WithLabel("Complete Team")
                            .WithCustomId($"pvm-complete-team_{bossIndex}")
                            .WithStyle(ButtonStyle.Success));

                    var embed = new EmbedBuilder()
                        .WithTitle($"{bossPrettyName} Sign Ups")
                        //TEMP
                        .WithDescription(String.Join(Environment.NewLine, DictToList(testNames)));

                    await arg.RespondAsync(message, embed:embed.Build(), component: builder.Build());
                }
            }
        }

        private async Task HandlePvmSignup(SocketMessageComponent component, long bossIndex)
        {
            var currentEmbed = component.Message.Embeds.First();

            Dictionary<string, string> names = ParseNamesToDict(currentEmbed.Description);
            if (names.ContainsKey(component.User.Mention))
            {
                Console.WriteLine($"{component.User.Mention} is already signed up: ignoring.");
                await component.RespondAsync("You're already signed up.", ephemeral:true);
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

        private async Task HandleQuitSignup(SocketMessageComponent component, long bossIndex)
        {
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

        private async Task HandleTeamCompleted(SocketMessageComponent component, long bossIndex)
        {
            // TODO: check perms
            var currentEmbed = component.Message.Embeds.First();
            string bossName = currentEmbed.Title.Split(' ')[0];
            Console.WriteLine("Pressed complete for " + bossName);

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

            // send ephermeral message to confirm signup
            // once ephermeral is confirmed send final group to channel.
            var pendingTeamEmbed = new EmbedBuilder()
                        .WithTitle("Pending Team")
                        .AddField("Selected Users", String.Join(Environment.NewLine, DictToList(selectedNames)), inline: true)
                        .AddField("Unselected Users", String.Join(Environment.NewLine, DictToList(unselectedNames)), inline: true);

            var builder = new ComponentBuilder();
            foreach (var namePair in selectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"pvm-confirm-team_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            await component.RespondAsync("Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                "\nOnce you're finished making changes you can dismiss this message.",
                embed: pendingTeamEmbed.Build(), component: builder.Build(), ephemeral:true);
        }

        private async Task HandleTeamConfirmed(SocketMessageComponent component, long bossIndex)
        {
            var currentEmbed = component.Message.Embeds.First();
            Debug.Assert(currentEmbed.Fields.Length == 2);
            Dictionary<string, string> selectedNames = ParseNamesToDict(currentEmbed.Fields[0].Value);

            var embed = new EmbedBuilder()
                        .WithTitle("Selected Team")
                        .WithDescription(String.Join(Environment.NewLine, DictToList(selectedNames)));

            var builder = new ComponentBuilder();
            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Edit Team")
                    .WithCustomId($"pvm-edit-team_{bossIndex}")
                    .WithStyle(ButtonStyle.Danger));


            await component.Channel.ModifyMessageAsync(component.Message.Reference.MessageId.Value, msgProps =>
            {
                // TODO: change this from "signup for x".
                //msgProps.Content = "";
                msgProps.Embed = embed.Build();
                msgProps.Components = builder.Build();
            });

            // Ack the interaction so they don't see "interaction failed" after hitting complete team.
            await component.DeferAsync();
        }

        private async Task AddUserToTeam(SocketMessageComponent component, string mention, string username, long bossIndex)
        {
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

            var pendingTeamEmbed = new EmbedBuilder()
                        .WithTitle("Pending Team")
                        .AddField("Selected Users", String.Join(Environment.NewLine, DictToList(selectedNames)), inline: true)
                        .AddField("Unselected Users", String.Join(Environment.NewLine, DictToList(unselectedNames)), inline: true);

            var builder = new ComponentBuilder();
            foreach (var namePair in selectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"pvm-confirm-team_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = pendingTeamEmbed.Build();
                msgProps.Components = builder.Build();
            });
        }

        private async Task RemoveUserFromTeam(SocketMessageComponent component, string mention, string username, long bossIndex)
        {
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

            var pendingTeamEmbed = new EmbedBuilder()
                        .WithTitle("Pending Team")
                        .AddField("Selected Users", String.Join(Environment.NewLine, DictToList(selectedNames)), inline: true)
                        .AddField("Unselected Users", String.Join(Environment.NewLine, DictToList(unselectedNames)), inline: true);

            var builder = new ComponentBuilder();
            foreach (var namePair in selectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}_{bossIndex}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId($"pvm-confirm-team_{bossIndex}")
                    .WithStyle(ButtonStyle.Primary));

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = pendingTeamEmbed.Build();
                msgProps.Components = builder.Build();
            });
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component.Data.CustomId.StartsWith("pvm-team-signup_"))
            {
                long boss = long.Parse(component.Data.CustomId.Split('_')[1]);
                await HandlePvmSignup(component, boss);
            }
            else if (component.Data.CustomId.StartsWith("pvm-quit-signup_"))
            {
                long boss = long.Parse(component.Data.CustomId.Split('_')[1]);
                await HandleQuitSignup(component, boss);
            }
            else if (component.Data.CustomId.StartsWith("pvm-complete-team_"))
            {
                long boss = long.Parse(component.Data.CustomId.Split('_')[1]);
                await HandleTeamCompleted(component, boss);
            }
            else if (component.Data.CustomId.StartsWith("pvm-confirm-team_"))
            {
                long boss = long.Parse(component.Data.CustomId.Split('_')[1]);
                await HandleTeamConfirmed(component, boss);
            }
            else if (component.Data.CustomId.StartsWith("remove-user-from-team_"))
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                long boss = long.Parse(component.Data.CustomId.Split('_')[3]);
                await RemoveUserFromTeam(component, mention, username, boss);
            }
            else if (component.Data.CustomId.StartsWith("add-user-to-team_"))
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                long boss = long.Parse(component.Data.CustomId.Split('_')[3]);
                await AddUserToTeam(component, mention, username, boss);
            }
        }
    }
}
