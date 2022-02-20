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

    public static class Data
    {
        public static readonly RoleDefinition[] Roles = new RoleDefinition[]
        {
            new RoleDefinition(RaidRole.Base, "🛡", "Base"),
            new RoleDefinition(RaidRole.MainStun, "💥", "Main Stun"),
            new RoleDefinition(RaidRole.BackupStun, "⚡", "Backup Stun"),
            new RoleDefinition(RaidRole.Backup, "🇧", "Backup"),
            new RoleDefinition(RaidRole.Shark10, "🦈", "Shark 10"),
            new RoleDefinition(RaidRole.JellyWrangler, "🐡", "Jelly Wrangler"),
            new RoleDefinition(RaidRole.PT13, "1️⃣", "PT 1/3"),
            new RoleDefinition(RaidRole.NorthTank, "🐍", "North Tank"),
            new RoleDefinition(RaidRole.PoisonTank, "🤢", "Poison Tank"),
            new RoleDefinition(RaidRole.PT2, "2️⃣", "PT 2"),
            new RoleDefinition(RaidRole.Double, "🇩", "Double"),
            new RoleDefinition(RaidRole.CPR, "❤️", "CPR"),
            new RoleDefinition(RaidRole.NC, "🐕", "NC"),
            new RoleDefinition(RaidRole.Stun5, "5️⃣", "Stun 5", max:2),
            new RoleDefinition(RaidRole.Stun0, "0️⃣", "Stun 0"),
            new RoleDefinition(RaidRole.DPS, "⚔️", "DPS", 5), // TODO: confirm max
            new RoleDefinition(RaidRole.Fill, "🆓", "Fill", max:10),
            new RoleDefinition(RaidRole.Reserve, "💭", "Reserve", max:10),
        };

        public static readonly bool[,] RoleCompatibilityMatrix = new bool[,]
        {
					        //Base	Main Stun	Backup Stun	Backup	Shark 10	JW		PT13	North Tank	Poison Tank	PT2		Double	CPR		NC		Stun5	Stun0	DPS		Fill	Reserve
	        /*Base*/		{true,  true,       true,       false,  false,      false,  false,  false,      false,      false,  false,  false,  false,  false,  false,  false,  false,  false},
	        /*Main Stun*/	{true,  true,       false,      true,   true,       true,   true,   true,       true,       true,   true,   true,   true,   true,   true,   true,   false,  false},
	        /*Backup Stun*/	{true,  false,      true,       true,   true,       true,   true,   true,       true,       true,   true,   true,   true,   true,   true,   true,   false,  false},
	        /*Backup*/		{false, true,       true,       true,   true,       true,   false,  true,       true,       false,  true,   true,   false,  true,   true,   false,  false,  false},
	        /*Shark 10*/	{false, true,       true,       true,   true,       false,  true,   false,      true,       true,   true,   true,   true,   false,  false,  false,  false,  false},
	        /*JW*/			{false, true,       true,       true,   false,      true,   true,   false,      true,       true,   true,   true,   true,   false,  false,  false,  false,  false},
	        /*PT13*/		{false, true,       true,       false,  true,       true,   true,   true,       true,       false,  true,   true,   false,  true,   true,   false,  false,  false},
	        /*North Tank*/	{false, true,       true,       true,   false,      false,  true,   true,       true,       true,   true,   true,   true,   false,  false,  false,  false,  false},
	        /*Poison Tank*/	{false, true,       true,       true,   true,       true,   true,   true,       true,       true,   false,  false,  true,   true,   true,   false,  false,  false},
	        /*PT2*/			{false, true,       true,       false,  true,       true,   false,  true,       true,       true,   true,   true,   false,  true,   true,   false,  false,  false},
	        /*Double*/		{false, true,       true,       true,   true,       true,   true,   true,       false,      true,   true,   false,  true,   true,   true,   false,  false,  false},
	        /*CPR*/			{false, true,       true,       true,   true,       true,   true,   true,       false,      true,   false,  true,   true,   true,   true,   false,  false,  false},
	        /*NC*/			{false, true,       true,       false,  true,       true,   false,  true,       true,       false,  true,   true,   false,  true,   true,   false,  false,  false},
	        /*Stun5*/		{false, true,       true,       true,   false,      false,  true,   false,      true,       true,   true,   true,   true,   true,   false,  false,  false,  false},
	        /*Stun0*/		{false, true,       true,       true,   false,      false,  true,   false,      true,       true,   true,   true,   true,   false,  true,   false,  false,  false},
	        /*DPS*/			{false, true,       true,       false,  false,      false,  false,  false,      false,      false,  false,  false,  false,  false,  false,  true,   false,  false},
	        /*Fill*/		{false, false,      false,      false,  false,      false,  false,  false,      false,      false,  false,  false,  false,  false,  false,  false,  true,   false},
	        /*Reserve*/		{false, false,      false,      false,  false,      false,  false,  false,      false,      false,  false,  false,  false,  false,  false,  false,  false,  true},
        };
    }
}
