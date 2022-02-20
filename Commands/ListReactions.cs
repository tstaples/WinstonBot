using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using System.Text;


namespace WinstonBot.Commands
{
    public enum ListMode
    {
        Mention,
        Username,
        FullUsername,
    }

    public class ListModeProvider
    {
        public static void PopulateChoices(SlashCommandOptionBuilder builder)
        {
            foreach (var mode in Enum.GetValues(typeof(ListMode)))
            {
                builder.AddChoice(mode.ToString(), (int)mode);
            }
        }
    }

    [Command("list-reactions", "Lists the people who reacted with a certain emote to a message.")]
    public class ListReactions : CommandBase
    {
        [CommandOption("message-id", "The id of the message. Developer mode must be enabled to get the id.")]
        public string TargetMessageId { get; set; }

        [CommandOption("emoji", "The emoji to list reactions for")]
        public string Emoji { get; set; }

        [CommandOption("mode", "How to list the names.", dataProvider:typeof(ListModeProvider))]
        public long Mode { get; set; }

        public ListReactions(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleCommand(CommandContext context)
        {
            ulong messageId = 0;
            if (!ulong.TryParse(TargetMessageId, out messageId))
            {
                throw new InvalidCommandArgumentException("Invalid message id");
            }

            var channel = context.Client.GetChannel(context.ChannelId) as SocketTextChannel;
            if (channel == null)
            {
                await context.RespondAsync("Failed to find channel for message.", ephemeral: true);
                return;
            }

            var message = await channel.GetMessageAsync(messageId);
            if (message == null)
            {
                await context.RespondAsync("Failed to find message.", ephemeral: true);
                return;
            }

            IEmote? emote = null;
            {
                Discord.Emoji emoji;
                Discord.Emote tempEmote;
                if (Discord.Emoji.TryParse(Emoji, out emoji))
                {
                    emote = emoji;
                }
                else if (Discord.Emote.TryParse(Emoji, out tempEmote))
                {
                    emote = tempEmote;
                }
            }

            var mode = (ListMode)Mode;

            if (emote != null)
            {
                var reactions = message.GetReactionUsersAsync(emote, 100);
                var users = await reactions.FlattenAsync();
                var builder = new StringBuilder();
                foreach (var user in users)
                {
                    string name = string.Empty;
                    switch (mode)
                    {
                        case ListMode.FullUsername:
                            name = $"{user.Username}#{user.Discriminator}";
                            break;

                        case ListMode.Username:
                            name = user.Username;
                            break;

                        case ListMode.Mention:
                            name = user.Mention;
                            break;
                    }

                    builder.Append(name).AppendLine();
                }

                await context.RespondAsync(builder.ToString(), ephemeral: true);
            }
            else
            {
                await context.RespondAsync($"Failed to parse emote {Emoji}", ephemeral: true);
            }
        }
    }
}
