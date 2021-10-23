using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Services
{
    public class MessageDatabase : DiscordClientService
    {
        public ConcurrentDictionary<ulong, ReadOnlyCollection<ulong>> OriginalSignupsForMessage { get; } = new();
        public ConcurrentDictionary<ulong, bool> MessagesBeingEdited { get; } = new();

        public MessageDatabase(DiscordSocketClient client, ILogger<DiscordClientService> logger) : base(client, logger)
        {
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
