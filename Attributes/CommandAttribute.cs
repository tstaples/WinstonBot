using WinstonBot.Commands;

namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class CommandAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DefaultPermission DefaultPermission { get; set; }
        // Exclude this command from being shown in other commands that list commands.
        public Type[]? Actions {  get; set; }

        public CommandAttribute()
        {
            Name = null;
            Description = null;
            DefaultPermission = DefaultPermission.Everyone;
        }

        public CommandAttribute(
            string name,
            string description,
            DefaultPermission defaultPermission = DefaultPermission.Everyone,
            Type[]? actions = null)
        {
            Name = name;
            Description = description;
            DefaultPermission = defaultPermission;
            Actions = actions;
        }
    }
}
