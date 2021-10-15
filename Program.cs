using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot;
using WinstonBot.Services;

public class Program
{
    private DiscordSocketClient _client;
    private CommandHandler _commandHandler;
    private EmoteDatabase _emoteDatabase;
    private ConfigService _configService;
    private MessageDatabase _messageDatabase;
    private InteractionService _interactionService;
    private ScheduledCommandService _timerService;
    private IServiceProvider _services;

    public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

    public IServiceProvider BuildServiceProvider() => new ServiceCollection()
        .AddSingleton(_client)
        .AddSingleton(_emoteDatabase)
        .AddSingleton(_configService)
        .AddSingleton(_messageDatabase)
        .AddSingleton(_interactionService)
        .AddSingleton(_timerService)
        .BuildServiceProvider();

    public async Task MainAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig()
        {
            MessageCacheSize = 1000,
            LargeThreshold = 250,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
            LogLevel = LogSeverity.Info,
            AlwaysDownloadUsers = true
        });
        _client.Log += this.Log;

#if DEBUG
        var token = File.ReadAllText(Path.Combine("Config", "test_token.txt"));
#else
        var token = File.ReadAllText(Path.Combine("Config", "token.txt"));
#endif
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _interactionService = new InteractionService();
        _messageDatabase = new MessageDatabase();
        _emoteDatabase = new EmoteDatabase();
        _configService = new ConfigService(Path.Combine("Config", "config.json"));
        _timerService = new ScheduledCommandService(Path.Combine("Config", "ScheduledEvents.json"), _client);

        _services = BuildServiceProvider();

        _commandHandler = new CommandHandler(_services, _client);

        _client.Ready += ClientReady;

        await Task.Delay(-1);
    }

    private async Task ClientReady()
    {
        Console.WriteLine("Client ready");

        await _commandHandler.InstallCommandsAsync();

        _timerService.StartEvents(_services);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}