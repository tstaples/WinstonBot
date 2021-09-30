using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Data
{
    public class BossData
    {
        public enum Boss
        {
            AoD,
            Raids,
            Count
        }

        public class Entry
        {
            public Boss Id { get; set; }
            public string CommandName { get; set; }
            public string PrettyName { get; set; }
            public uint MaxPlayersOnTeam { get; set; }
        }

        public static readonly Entry[] Entries = new Entry[]
        {
            new Entry()
            {
                Id = Boss.AoD,
                CommandName = "aod",
                PrettyName = "AoD",
                MaxPlayersOnTeam = 7 // TODO: allow optionally passing this in through the command for different team sizes.
            },
            new Entry()
            {
                Id = Boss.Raids,
                CommandName = "raids",
                PrettyName = "Liberation of Mazcab",
                MaxPlayersOnTeam = 10
            },
        };

        public static bool ValidBossIndex(long index)
        {
            return index >= 0 && index < (long)Boss.Count;
        }
    }
}
