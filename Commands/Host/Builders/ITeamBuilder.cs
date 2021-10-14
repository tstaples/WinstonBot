using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinstonBot.Data;

namespace WinstonBot.Commands
{
    internal interface ITeamBuilder
    {
        public Dictionary<string, ulong> SelectTeam(List<ulong> inputNames);
    }
}
