using Discord;
using Discord.Commands;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WinstonBot;
using WinstonBot.Services;
using Microsoft.Extensions.Configuration;

public class Program
{
    private DiscordSocketClient _client;
    private CommandHandler _commandHandler;
    private EmoteDatabase _emoteDatabase;
    private ConfigService _configService;
    private MessageDatabase _messageDatabase;
    private InteractionService _interactionService;
    private ScheduledCommandService _timerService;
    private AoDDatabase _aoDDatabase;
    private ILogger _logger;
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
        .AddSingleton(_aoDDatabase)
        .AddSingleton(_logger)
        .BuildServiceProvider();

    public async Task MainAsync()
    {
        IHostBuilder builder = Host.CreateDefaultBuilder()
            .ConfigureHostConfiguration(config => config.AddEnvironmentVariables())
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddEnvironmentVariables()
                    .AddJsonFile(Path.Combine("Config", "appsettings.json"), false, true)
                    .AddJsonFile(Path.Combine("Config", $"appsettings.{context.HostingEnvironment.EnvironmentName}.json"), false, true)
                    .Build();
            })
            .ConfigureLogging(logging =>
            {
                logging
                .AddConsole()
                .AddDebug()
                .SetMinimumLevel(LogLevel.Trace);
            })
            .ConfigureDiscordHost((context, config) =>
            {
                config.SocketConfig = new DiscordSocketConfig()
                {
                    MessageCacheSize = 1000,
                    LargeThreshold = 250,
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
                    LogLevel = LogSeverity.Debug,
                    AlwaysDownloadUsers = true,
                    MaxWaitBetweenGuildAvailablesBeforeReady = 2000
                };

                config.Token = context.Configuration["token"];
            })
            .ConfigureServices((context, services) =>
            {
                services
                //.AddSingleton<InteractionService>() // Not used for now
                .AddHostedService<MessageDatabase>()
                //.AddSingleton<EmoteDatabase>() // Deprecated for now
                .AddHostedService<ConfigService>()
                .AddHostedService<ScheduledCommandService>()
                .AddHostedService<AoDDatabase>()
                .AddHostedService<CommandHandler>();

            })
            .UseConsoleLifetime();

        Console.WriteLine($"Running WinstonBot Version: {Assembly.GetEntryAssembly().GetName().Version}");

        //_client = new DiscordSocketClient(new DiscordSocketConfig()
        //{
        //    MessageCacheSize = 1000,
        //    LargeThreshold = 250,
        //    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
        //    LogLevel = LogSeverity.Info,
        //    AlwaysDownloadUsers = true
        //});
        //_client.Log += this.Log;

        //#if DEBUG
        //        var token = File.ReadAllText(Path.Combine("Config", "test_token.txt"));
        //#else
        //        var token = File.ReadAllText(Path.Combine("Config", "token.txt"));
        //#endif
        //        await _client.LoginAsync(TokenType.Bot, token);
        //        await _client.StartAsync();

        //_interactionService = new InteractionService();
        //_messageDatabase = new MessageDatabase();
        //_emoteDatabase = new EmoteDatabase();
        //_configService = new ConfigService(Path.Combine("Config", "config.json"));
        //_timerService = new ScheduledCommandService(Path.Combine("Config", "ScheduledEvents.json"), _client);
        //_aoDDatabase = new AoDDatabase(Path.Combine("Config", "google_credentials.json"));
        //_aoDDatabase.Initialize();

        //_services = BuildServiceProvider();

        //_commandHandler = new CommandHandler(_services, _client);

        //_client.Ready += ClientReady;


        using var host = builder.Build();
        await host.RunAsync();

        Environment.ExitCode = 1;
    }

    //private async Task ClientReady()
    //{
    //    Console.WriteLine("Client ready");

    //    await _commandHandler.InstallCommandsAsync();

    //    _timerService.StartEvents(_services);

    //    await _client.SetGameAsync($"Version {Assembly.GetEntryAssembly().GetName().Version}");
    //}

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}