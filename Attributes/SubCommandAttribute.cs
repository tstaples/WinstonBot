namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SubCommandAttribute : Attribute
    {
        /// <summary>
        /// The subcommand name that appears in the slash command menu. Must be lower case and contain no special characters except -.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The subcommand description that appears in the slash command context menu.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The type of the parent command class this is a subcommand of.
        /// </summary>
        public Type ParentCommand { get; set; }
        
        /// <summary>
        /// This determines if we mark this as a subcommand or subcommand group. If you're trying to define a subcommand group
        /// but all your subcommands are created dynamically, set this to true.
        /// </summary>
        public bool HasDynamicSubCommands { get; set; }

        /// <summary>
        /// The actions your command defines.
        /// Actions are responses to interactions such as a button press.
        /// </summary>
        public Type[]? Actions { get; set; }

        public SubCommandAttribute()
        {
            Name = null;
            Description = "PLEASE FILL ME OUT";
            ParentCommand = null;
            HasDynamicSubCommands = false;
            Actions = null;
        }

        public SubCommandAttribute(string name, string description, Type parentCommand, Type[]? actions = null, bool dynamicSubcommands = false)
        {
            Name = name;
            Description = description;
            ParentCommand = parentCommand;
            Actions = actions;
            HasDynamicSubCommands = dynamicSubcommands;
        }
    }
}
