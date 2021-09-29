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
            _client.MessageReceived += HandleCommandAsync;
            _client.ReactionAdded += HandleReactionAdded;
            //_client.ReactionRemoved += HandleReactionRemoved;
            //_client.ReactionsCleared += HandleAllReactionsRemoved;

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

        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.User.IsSpecified)
            {
                return;
            }

            var userMessage = await message.GetOrDownloadAsync();

            var guild = (channel as SocketGuildChannel)?.Guild;
            if (guild == null)
            {
                Console.WriteLine("Ignoring reaction containing invalid guild: " + userMessage.Content);
                return;
            }

            if (reaction.UserId == this._client.CurrentUser.Id ||
                userMessage.Author.Id != this._client.CurrentUser.Id)
            {
                return;
            }

            var messageDb = _services.GetRequiredService<MessageDatabase>();

            // TODO: if someone tries to signup that doesn't have the necessary role PM them and refer to the rules channel.

            if (messageDb.HasMessage(guild.Id, message.Id))
            {
                var handler = messageDb.GetMessageHandler(guild.Id, message.Id);
                var handled = await handler.ReactionAdded(userMessage, channel, reaction);
                if (handled)
                {
                    messageDb.RemoveMessage(guild.Id, message.Id);
                }
            }
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            var messageDb = _services.GetRequiredService<MessageDatabase>();

            var guild = (message.Channel as SocketGuildChannel)?.Guild;
            if (guild == null)
            {
                Console.WriteLine("Ignoring message containing invalid guild: " + message.Content);
                return;
            }

            if (message.Reference != null &&
                message.Reference.MessageId.IsSpecified &&
                messageDb.HasMessage(guild.Id, message.Reference.MessageId.Value))
            {
                var handler = messageDb.GetMessageHandler(guild.Id, message.Reference.MessageId.Value);
                var handled = await handler.MessageRepliedTo(message);
                if (handled)
                {
                    messageDb.RemoveMessage(guild.Id, message.Id);
                }
            }
            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            else if (message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                // Create a WebSocket-based command context based on the message
                var context = new Commands.CommandContext(_client, message)
                {
                    ServiceProvider = _services,
                };

                // Execute the command with the command context we just
                // created, along with the service provider for precondition checks.
                await _commands.ExecuteAsync(
                    context: context,
                    argPos: argPos,
                    services: _services);
            }
        }
    }
}
