namespace WinstonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CommandOptionAttribute : Attribute
    {
        /// <summary>
        /// The name (lower case, no special characters) for this option that appears in the slash command.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The description for this option.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this option is required or optional.
        /// </summary>
        public bool Required {  get; set; }

        /// <summary>
        /// If your option has choices you can specify the class that populates the choices when building the command.
        /// This class must have a function with the signature: public static void PopulateChoices(SlashCommandOptionBuilder builder)
        /// </summary>
        public Type? ChoiceDataProvider { get; set; }

        public CommandOptionAttribute()
        {
            Name = null;
            Description = "PLEASE FILL ME OUT";
            Required = true;
            ChoiceDataProvider = null;
        }

        public CommandOptionAttribute(string name, string description, bool required = true, Type? dataProvider = null)
        {
            Name = name;
            Description = description;
            Required = required;
            ChoiceDataProvider = dataProvider;
        }
    }
}
