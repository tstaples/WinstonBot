using Microsoft.Extensions.Hosting;

namespace WinstonBot.Services
{
    internal class DBService : BackgroundService
    {
        private AoDDatabase _aoDDatabase;

        public DBService(AoDDatabase aoDDatabase)
        {
            _aoDDatabase = aoDDatabase;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _aoDDatabase.RefreshDB();
            return Task.CompletedTask;
        }
    }
}
