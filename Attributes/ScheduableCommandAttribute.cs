namespace WinstonBot.Attributes
{
    /// <summary>
    /// Add this to your command to allow it to be scheduled via /schedule command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal class ScheduableCommandAttribute : Attribute
    {
        // TODO: can add things like min time it can run etc
    }
}
