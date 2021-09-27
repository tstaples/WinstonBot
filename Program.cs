using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Concurrent;
using WinstonBot;

public class Program
{
	private DiscordSocketClient _client;
	private CommandHandler _commandHandler;
	private MessageDatabase _messageDB;

	public static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();

	public async Task MainAsync()
	{
		_messageDB = new MessageDatabase();

		_client = new DiscordSocketClient();
		_client.Log += this.Log;

		var token = File.ReadAllText(Path.Combine("Config", "token.txt"));
		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();

		_commandHandler = new CommandHandler(_client, new CommandService(), _messageDB);
		await _commandHandler.InstallCommandsAsync();

		await Task.Delay(-1);
	}

	private Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}
}