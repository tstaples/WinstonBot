namespace WinstonBot.Attributes
{
    /// <summary>
    /// Add this to your command to have it appear under /configure command.
    /// This will allow you to set role permissions for who can use your command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal class ConfigurableCommandAttribute : Attribute
    {
    }
}
