using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Commands.Config
{
    internal interface ISubCommand
    {
        public string Name { get; }

        public SlashCommandOptionBuilder Build();
        public Task HandleCommand(ConfigCommandContext context, IReadOnlyCollection<SocketSlashCommandDataOption>? options);
    }
}
