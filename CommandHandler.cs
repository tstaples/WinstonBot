using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WinstonBot.Services;

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

        private async Task HandleInteractionCreated(SocketInteraction arg)
        {
            if (arg is SocketSlashCommand command)
            {
                if (command.Data.Name == "host-pvm-signup")
                {
                    var builder = new ComponentBuilder()
                        .WithButton("Sign Up", "pvm-team-signup")
                        .WithButton(new ButtonBuilder()
                            .WithLabel("Quit")
                            .WithCustomId("pvm-quit-signup")
                            .WithStyle(ButtonStyle.Danger))
                        .WithButton(new ButtonBuilder()
                            .WithLabel("Complete Team")
                            .WithCustomId("pvm-complete-team")
                            .WithStyle(ButtonStyle.Success));

                    var bossIndex = command.Data.Options.First().Value;
                    string bossName = "AoD"; // TODO: get from index

                    var embed = new EmbedBuilder()
                        .WithTitle($"{bossName} Sign Ups")
                        //TEMP
                        .WithDescription(String.Join(Environment.NewLine, DictToList(testNames)));

                    await arg.RespondAsync($"Click to signup for {bossName}.", embed:embed.Build(), component: builder.Build());
                }
            }
        }

        private async Task HandlePvmSignup(SocketMessageComponent component)
        {
            var currentEmbed = component.Message.Embeds.First();

            //List<string> names = new();
            Dictionary<string, string> names = new();
            if (currentEmbed.Description != null)
            {
                var lines = component.Message.Embeds.First().Description.Split(Environment.NewLine).ToList();
                lines.ForEach(line => names.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }

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

        private async Task HandleQuitSignup(SocketMessageComponent component)
        {
            //await RemoveUserFromTeam(component, component.User.Mention);
            var currentEmbed = component.Message.Embeds.First();

            //List<string> names = new();
            Dictionary<string, string> names = new();
            if (currentEmbed.Description != null)
            {
                var lines = component.Message.Embeds.First().Description.Split(Environment.NewLine).ToList();
                lines.ForEach(line => names.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }

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

        private async Task HandleTeamCompleted(SocketMessageComponent component)
        {
            // TODO: check perms
            var currentEmbed = component.Message.Embeds.First();
            string bossName = currentEmbed.Title.Split(' ')[0];
            Console.WriteLine("Pressed complete for " + bossName);

            Dictionary<string, string> names = new();
            if (currentEmbed.Description != null)
            {
                var lines = component.Message.Embeds.First().Description.Split(Environment.NewLine).ToList();
                lines.ForEach(line => names.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }

            if (names.Count == 0)
            {
                await component.RespondAsync("Not enough people signed up.", ephemeral: true);
                return;
            }

            // TODO: calculate who should go.
            //List<string> selectedNames = names;
            Dictionary<string, string> selectedNames = new();
            Dictionary<string, string> unselectedNames = new();
            int i = 0;
            foreach (var pair in names)
            {
                if (i++ < 7) selectedNames.Add(pair.Key, pair.Value);
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
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId("pvm-confirm-team")
                    .WithStyle(ButtonStyle.Primary));

            await component.RespondAsync("Confirm or edit the team", embed: pendingTeamEmbed.Build(), component: builder.Build(), ephemeral:true);
        }

        private async Task HandleTeamConfirmed(SocketMessageComponent component)
        {
            
        }
        
        private async Task AddUserToTeam(SocketMessageComponent component, string mention, string username)
        {
            var currentEmbed = component.Message.Embeds.First();

            Dictionary<string, string> selectedNames = new();
            Dictionary<string, string> unselectedNames = new();
            if (currentEmbed.Fields.Length == 2)
            {
                var selectedlines = currentEmbed.Fields[0].Value.Split(Environment.NewLine).ToList();
                selectedlines.ForEach(line => selectedNames.Add(line.Split(" - ")[0], line.Split(" - ")[1]));

                var unselectedlines = currentEmbed.Fields[1].Value.Split(Environment.NewLine).ToList();
                unselectedlines.ForEach(line => unselectedNames.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }

            if (selectedNames.ContainsKey(mention))
            {
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
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId("pvm-confirm-team")
                    .WithStyle(ButtonStyle.Primary));

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = pendingTeamEmbed.Build();
                msgProps.Components = builder.Build();
            });
        }

        private async Task RemoveUserFromTeam(SocketMessageComponent component, string mention, string username)
        {
            var currentEmbed = component.Message.Embeds.First();

            Dictionary<string, string> selectedNames = new();
            Dictionary<string, string> unselectedNames = new();
            if (currentEmbed.Fields.Length == 2)
            {
                var selectedlines = currentEmbed.Fields[0].Value.Split(Environment.NewLine).ToList();
                selectedlines.ForEach(line => selectedNames.Add(line.Split(" - ")[0], line.Split(" - ")[1]));

                var unselectedlines = currentEmbed.Fields[1].Value.Split(Environment.NewLine).ToList();
                unselectedlines.ForEach(line => unselectedNames.Add(line.Split(" - ")[0], line.Split(" - ")[1]));
            }

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
                    .WithCustomId($"remove-user-from-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Success));
            }

            foreach (var namePair in unselectedNames)
            {
                builder.WithButton(new ButtonBuilder()
                    .WithLabel(namePair.Value)
                    .WithCustomId($"add-user-to-team_{namePair.Key}_{namePair.Value}")
                    .WithStyle(ButtonStyle.Secondary));
            }

            builder.WithButton(new ButtonBuilder()
                    .WithLabel("Confirm Team")
                    .WithCustomId("pvm-confirm-team")
                    .WithStyle(ButtonStyle.Primary));

            await component.UpdateAsync(msgProps =>
            {
                msgProps.Embed = pendingTeamEmbed.Build();
                msgProps.Components = builder.Build();
            });
        }

        private async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            switch (component.Data.CustomId)
            {
                case "pvm-team-signup":
                    await HandlePvmSignup(component);
                    break;

                case "pvm-quit-signup":
                    await HandleQuitSignup(component);
                    break;

                case "pvm-complete-team":
                    await HandleTeamCompleted(component);
                    break;

                case "pvm-confirm-team":
                    await HandleTeamConfirmed(component);
                    break;
            }

            if (component.Data.CustomId.StartsWith("remove-user-from-team_"))
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                await RemoveUserFromTeam(component, mention, username);
            }
            else if (component.Data.CustomId.StartsWith("add-user-to-team_"))
            {
                string mention = component.Data.CustomId.Split('_')[1];
                string username = component.Data.CustomId.Split('_')[2];
                await AddUserToTeam(component, mention, username);
            }
        }

        //private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        //{
        //    if (!reaction.User.IsSpecified)
        //    {
        //        return;
        //    }

        //    var userMessage = await message.GetOrDownloadAsync();

        //    var guild = (channel as SocketGuildChannel)?.Guild;
        //    if (guild == null)
        //    {
        //        Console.WriteLine("Ignoring reaction containing invalid guild: " + userMessage.Content);
        //        return;
        //    }

        //    if (reaction.UserId == this._client.CurrentUser.Id ||
        //        userMessage.Author.Id != this._client.CurrentUser.Id)
        //    {
        //        return;
        //    }

        //    var messageDb = _services.GetRequiredService<MessageDatabase>();

        //    // TODO: if someone tries to signup that doesn't have the necessary role PM them and refer to the rules channel.

        //    if (messageDb.HasMessage(guild.Id, message.Id))
        //    {
        //        var handler = messageDb.GetMessageHandler(guild.Id, message.Id);
        //        var handled = await handler.ReactionAdded(userMessage, channel, reaction);
        //        if (handled)
        //        {
        //            messageDb.RemoveMessage(guild.Id, message.Id);
        //        }
        //    }
        //}

        //private async Task HandleCommandAsync(SocketMessage messageParam)
        //{
        //    // Don't process the command if it was a system message
        //    var message = messageParam as SocketUserMessage;
        //    if (message == null || message.Author.IsBot) return;

        //    // Create a number to track where the prefix ends and the command begins
        //    int argPos = 0;

        //    var messageDb = _services.GetRequiredService<MessageDatabase>();

        //    var guild = (message.Channel as SocketGuildChannel)?.Guild;
        //    if (guild == null)
        //    {
        //        Console.WriteLine("Ignoring message containing invalid guild: " + message.Content);
        //        return;
        //    }

        //    if (message.Reference != null &&
        //        message.Reference.MessageId.IsSpecified &&
        //        messageDb.HasMessage(guild.Id, message.Reference.MessageId.Value))
        //    {
        //        var handler = messageDb.GetMessageHandler(guild.Id, message.Reference.MessageId.Value);
        //        var handled = await handler.MessageRepliedTo(message);
        //        if (handled)
        //        {
        //            messageDb.RemoveMessage(guild.Id, message.Id);
        //        }
        //    }
        //    // Determine if the message is a command based on the prefix and make sure no bots trigger commands
        //    else if (message.HasCharPrefix('!', ref argPos) ||
        //        message.HasMentionPrefix(_client.CurrentUser, ref argPos))
        //    {
        //        // Create a WebSocket-based command context based on the message
        //        var context = new Commands.CommandContext(_client, message)
        //        {
        //            ServiceProvider = _services,
        //            GuildId = guild.Id
        //        };

        //        // Execute the command with the command context we just
        //        // created, along with the service provider for precondition checks.
        //        await _commands.ExecuteAsync(
        //            context: context,
        //            argPos: argPos,
        //            services: _services);
        //    }
        //}
    }
}
