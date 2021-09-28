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

	public static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();

	public IServiceProvider BuildServiceProvider() => new ServiceCollection()
		.AddSingleton(_client)
		.AddSingleton(new CommandService(new CommandServiceConfig()
        {
			DefaultRunMode = RunMode.Async,
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
		_messageDB = new MessageDatabase();
		_configService = new ConfigService(Path.Combine("Config", "config.json"));

		_commandHandler = new CommandHandler(BuildServiceProvider(), _client);
		await _commandHandler.InstallCommandsAsync();

		await Task.Delay(-1);
	}

	private Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}
}