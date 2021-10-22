using WinstonBot.Commands;

namespace WinstonBot.Attributes
{
    /// <summary>
    /// Defines a top level command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// The command name that appears in the slash command menu. Must be lower case and contain no special characters except -.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The command description that appears in the slash command context menu.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// If this command is admin only be default or available to everyone.
        /// Specific role permissions can be set on your command with the /configure command if it's marked [Configurable].
        /// </summary>
        public DefaultPermission DefaultPermission { get; set; }

        /// <summary>
        /// The actions your command defines.
        /// Actions are responses to interactions such as a button press.
        /// </summary>
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
