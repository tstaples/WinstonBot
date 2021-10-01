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
        public int Id { get; }
        public long RoleId {  get; }

        public Task HandleAction(SocketMessageComponent component);
    }

    public interface ICommand
    {
        public string Name { get; }
        public int Id { get; }
        public IEnumerable<IAction> Actions { get; }

        public SlashCommandProperties BuildCommand();
        public Task HandleCommand(SocketSlashCommand slashCommand);
    }
}
