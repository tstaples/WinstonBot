using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Commands
{
    public interface IAction
    {
        public string Name { get; }

        public Task HandleAction(ActionContext context);
    }

    public interface ICommandBase
    {
        public string Name { get; }

        public CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services);

        public Task HandleCommand(CommandContext context);
    }

    public interface ICommand : ICommandBase
    {
        public ulong AppCommandId { get; set; }
        // TODO: sub command can have actions too
        public IEnumerable<IAction> Actions { get; }

        //public SlashCommandProperties BuildCommand();
        // TODO: move this into base
        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services);
    }
}
