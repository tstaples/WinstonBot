namespace WinstonBot.Commands
{
    internal interface ITeamBuilder
    {
        public IServiceProvider ServiceProvider { get; set; }

        public Dictionary<string, ulong> SelectTeam(IEnumerable<ulong> inputNames);

        public Dictionary<string, ulong>[] SelectTeams(IEnumerable<ulong> inputNames, int numTeams);

    }
}
