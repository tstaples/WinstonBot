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
        public long RoleId {  get; }

        public Task HandleAction(ActionContext context);
    }

    public interface ICommand
    {
        public enum Permission
        { 
            Everyone,
            AdminOnly
        }

        public string Name { get; }
        public Permission DefaultPermission { get; }
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions { get; }

        public SlashCommandProperties BuildCommand();
        public Task HandleCommand(Commands.CommandContext context);
        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services);
    }
}
