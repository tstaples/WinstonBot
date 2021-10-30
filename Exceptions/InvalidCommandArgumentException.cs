namespace WinstonBot
{
    public class InvalidCommandArgumentException : Exception
    {
        public InvalidCommandArgumentException(string message) : base(message) { }
    }
}
