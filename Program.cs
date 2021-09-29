using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot;
using WinstonBot.Services;
using Newtonsoft.Json;

public class Program
{
	private DiscordSocketClient _client;
	private CommandHandler _commandHandler;
	private MessageDatabase _messageDB;
	private EmoteDatabase _emoteDatabase;
	private ConfigService _configService;
	private IServiceProvider _services;

	public static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();

	public IServiceProvider BuildServiceProvider() => new ServiceCollection()
		.AddSingleton(_client)
		.AddSingleton(new CommandService(new CommandServiceConfig()
        {
			DefaultRunMode = RunMode.Sync,
			CaseSensitiveCommands = false,
			LogLevel = LogSeverity.Verbose
        }))
		.AddSingleton(_messageDB)
		.AddSingleton(_emoteDatabase)
		.AddSingleton(_configService)
		.BuildServiceProvider();

	public async Task MainAsync()
	{
		_client = new DiscordSocketClient(new DiscordSocketConfig()
        {
			MessageCacheSize = 1000
        });
		_client.Log += this.Log;

		var token = File.ReadAllText(Path.Combine("Config", "token.txt"));
		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();

		Console.WriteLine("after start");
		_emoteDatabase = new EmoteDatabase();

        string dbPath = Path.Combine("Database", "MessageData.json");
		_messageDB = new MessageDatabase(dbPath);
		_configService = new ConfigService(Path.Combine("Config", "config.json"));

        _client.Ready += ClientReady;

		_services = BuildServiceProvider();
		_commandHandler = new CommandHandler(_services, _client);
		await _commandHandler.InstallCommandsAsync();

		await Task.Delay(-1);
	}

    private Task ClientReady()
    {
		Console.WriteLine("Client ready");

		// read in the db
		// populate the message dict
		_messageDB.Load(_services);

		return Task.CompletedTask;
    }

    private Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}
}