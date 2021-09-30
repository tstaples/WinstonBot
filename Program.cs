using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot;
using WinstonBot.Services;
using Newtonsoft.Json;
using Discord.Net;

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

        string dbPath = Path.Combine("Database", "MessageData.json");
		_messageDB = new MessageDatabase(dbPath);
		_configService = new ConfigService(Path.Combine("Config", "config.json"));

        _client.Ready += ClientReady;

		_services = BuildServiceProvider();
		_commandHandler = new CommandHandler(_services, _client);
		await _commandHandler.InstallCommandsAsync();

		await Task.Delay(-1);
	}

    private async Task ClientReady()
    {
		Console.WriteLine("Client ready");

		var guild = _client.Guilds.First();

		var hostCommand = new SlashCommandBuilder()
			.WithName("host-pvm")
			.WithDescription("Hosts a pvm event.")
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("boss")
				.WithDescription("The boss to host")
				.WithRequired(true)
				.AddChoice("aod", 1)
				.AddChoice("raids", 2)
				.WithType(ApplicationCommandOptionType.Integer))
			.AddOption("message", ApplicationCommandOptionType.String, "Additional info about the event to be added to the message body.", required:false)
			.Build();

		var hostQueuedCommand = new SlashCommandBuilder()
			.WithName("host-pvm-signup")
			.WithDescription("Create a signup for a pvm event")
			.AddOption(new SlashCommandOptionBuilder()
				.WithName("boss")
				.WithDescription("The boss to host")
				.WithRequired(true)
				.AddChoice("aod", 0)
				.AddChoice("raids", 1)
				.WithType(ApplicationCommandOptionType.Integer))
			.AddOption("message", ApplicationCommandOptionType.String, "Additional info about the event to be added to the message body.", required: false)
			.Build();

		try
        {
			await guild.DeleteApplicationCommandsAsync();
			await guild.CreateApplicationCommandAsync(hostCommand);
			await guild.CreateApplicationCommandAsync(hostQueuedCommand);
		}
		catch (ApplicationCommandException ex)
        {
			// If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
			var json = JsonConvert.SerializeObject(ex.Error, Formatting.Indented);

			// You can send this error somewhere or just print it to the console, for this example we're just going to print it.
			Console.WriteLine(json);
		}

		// read in the db
		// populate the message dict
		_messageDB.Load(_services);
    }

    private Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}
}