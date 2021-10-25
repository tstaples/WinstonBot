using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    public interface IAction
    {
        public Task HandleAction(ActionContext context);
    }

    public interface ICommand
    {
        public Task HandleCommand(CommandContext context);
    }

    public abstract class CommandBase : ICommand
    {
        public const string BuildCommandName = "BuildCommand";
        public const string BuildCommandOptionName = "BuildCommandOption";
        public const string CreateContextName = "CreateContext";
        public const string CreateActionContextName = "CreateActionContext";

        public virtual bool WantsToHandleSubCommands => false;
        protected ILogger Logger { get; }

        public CommandBase(ILogger logger)
        {
            Logger = logger;
        }

        public virtual Task HandleCommand(CommandContext context) => Task.CompletedTask;
        public virtual Task HandleSubCommand(CommandContext context, CommandInfo subCommandInfo, IEnumerable<CommandDataOption>? options) => Task.CompletedTask;

        public static SlashCommandBuilder BuildCommand(SlashCommandBuilder defaultBuider, ILogger logger) => null;
        // This is for subcommands. TODO: find a better place to do ths.
        public static SlashCommandOptionBuilder BuildCommandOption(ILogger logger) => null;

        public static CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new CommandContext(client, arg, services);
        }

        public static ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services, string owningCommand)
        {
            return new ActionContext(client, arg, services, owningCommand);
        }
    }
}
