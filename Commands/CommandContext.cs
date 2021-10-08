using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    public class CommandContext
    {
        public DiscordSocketClient Client { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public ISocketMessageChannel Channel => SlashCommand.Channel;

        private SocketSlashCommand SlashCommand { get; set; }
        private string _commandName;

        public CommandContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            Client = client;
            SlashCommand = arg;
            ServiceProvider = services;
            _commandName = SlashCommand.CommandName;
        }

        // TODO: need to expose way to delete the message which deletes the interaction
        public async Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
        {
            // if there's a component then this message could contain an interaction
            if (component != null)
            {
                await SlashCommand.DeferAsync();
                var message = await SlashCommand.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);

                var interactionService = ServiceProvider.GetRequiredService<InteractionService>();
                interactionService.AddInteraction(_commandName, message.Id);
            }
            else
            {
                await SlashCommand.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);
            }
        }
    }

    public class ActionContext
    {
        public DiscordSocketClient Client { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public SocketUserMessage Message => Component.Message;
        public SocketMessageComponentData Data => Component.Data;
        public SocketUser User => Component.User;
        public ISocketMessageChannel Channel => Component.Channel;

        private SocketMessageComponent Component { get; set; }
        private string _commandName; // TODO: set this

        public ActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services, string owningCommand)
        {
            Client = client;
            Component = arg;
            ServiceProvider = services;
            _commandName = owningCommand;
        }

        public async Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
        {
            // if there's a component then this message could contain an interaction
            if (component != null)
            {
                await Component.DeferAsync();
                var message = await Component.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);

                var interactionService = ServiceProvider.GetRequiredService<InteractionService>();
                interactionService.AddInteraction(_commandName, message.Id);
            }
            else
            {
                await Component.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);
            }
        }

        public Task UpdateAsync(Action<MessageProperties> func, RequestOptions options = null)
        {
            // TODO: this could potentially add a component to a message that didn't have one before which we'd want to track.
            return Component.UpdateAsync(func, options);
        }

        public Task DeferAsync(bool ephemeral = false, RequestOptions options = null)
        {
            return Component.DeferAsync(ephemeral, options);
        }
    }
}
