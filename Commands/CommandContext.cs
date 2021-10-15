using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    public class CommandContext
    {
        public DiscordSocketClient Client { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public virtual ulong ChannelId => _slashCommand.Channel.Id;
        public virtual SocketGuild Guild => ((SocketGuildChannel)_slashCommand.Channel).Guild;
        public virtual IUser User => _slashCommand.User;

        protected virtual ISocketMessageChannel Channel => _slashCommand.Channel;
        private SocketSlashCommand? _slashCommand;
        protected string _commandName;

        public CommandContext(DiscordSocketClient client, SocketSlashCommand? arg, IServiceProvider services)
        {
            Client = client;
            _slashCommand = arg;
            ServiceProvider = services;
            _commandName = arg != null ? arg.CommandName : String.Empty;
        }

        // TODO: need to expose way to delete the message which deletes the interaction
        public virtual async Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
        {
            if (_slashCommand == null)
            {
                throw new Exception("_slashCommand is null.");
            }

            // if there's a component then this message could contain an interaction
            if (component != null)
            {
                await _slashCommand.DeferAsync();
                var message = await _slashCommand.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);

                var interactionService = ServiceProvider.GetRequiredService<InteractionService>();
                interactionService.AddInteraction(_commandName, message.Id);
            }
            else
            {
                await _slashCommand.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);
            }
        }

        public virtual async Task<RestUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent component = null, ISticker[] stickers = null)
        {
            return await Channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, component, stickers);
        }

        public virtual Task DeleteMessageAsync(ulong messageId, RequestOptions options = null)
        {
            return Channel.DeleteMessageAsync(messageId, options);
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
        public string OwningCommand => _commandName;

        private SocketMessageComponent Component { get; set; }
        private string _commandName;

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
