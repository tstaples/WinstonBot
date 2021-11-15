using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Commands.HostPvm
{
    // TODO: move this to raid specific data
    public enum RaidRole
    {
        Base,
        MainStun,
        BackupStun,
        Backup,
        Shark10,
        JellyWrangler,
        PT13,
        NorthTank,
        PoisonTank,
        PT2,
        Double,
        CPR,
        NC,
        Stun5,
        Stun0,
        DPS,
        Fill,
        Reserve,
        None,
    }

    public class RoleDefinition
    {
        public RaidRole RoleType { get; }
        public string Emoji { get; }
        public string Name { get; }
        public int MaxPlayers { get; }

        public RoleDefinition(RaidRole role, string emoji, string name, int max = 1)
        {
            RoleType = role;
            Emoji = emoji;
            Name = name;
            MaxPlayers = max;
        }
    }

    public class RuntimeRole
    {
        public RoleDefinition Definition { get; }
        public List<ulong> Users { get; }

        public RuntimeRole(RoleDefinition definition, List<ulong>? users = null)
        {
            Definition = definition;
            Users = users ?? new List<ulong>();
        }

        public bool AddUser(ulong id)
        {
            if (Users.Count < Definition.MaxPlayers)
            {
                return Utility.AddUnique(Users, id);
            }
            return false;
        }

        public bool HasUser(ulong id) => Users.Contains(id);
        public void RemoveUser(ulong id) => Users.Remove(id);
    }
}
