using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot;
using WinstonBot.Services;
using Newtonsoft.Json;
using Discord.Net;
using WinstonBot.Commands;

public class Program
{
    private DiscordSocketClient _client;
    private CommandHandler _commandHandler;
    private MessageDatabase _messageDB;
    private EmoteDatabase _emoteDatabase;
    private ConfigService _configService;
    private IServiceProvider _services;
    List<ICommand> _commands; // TODO: maybe make this a service?

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

        await Task.Delay(-1);
    }

    private async Task ClientReady()
    {
        Console.WriteLine("Client ready");

        _commands = new List<ICommand>()
        {
            new HostPvmSignup()
        };

        // this is gross.
        // Could pass this into BuildCommand
        var configureCommand = new ConfigCommand(_commands);
        _commands.Add(configureCommand);

        // Register the commands in all the guilds
        foreach (SocketGuild guild in _client.Guilds)
        {
            var registeredGuildCommands = await guild.GetApplicationCommandsAsync();
            bool anyNeedToBeRegisterd = _commands.Count != registeredGuildCommands.Count ||
                _commands
                .Where(cmd => registeredGuildCommands
                    .Where(regCmg => regCmg.Name != cmd.Name)
                    .Any())
                .Any();

            if (anyNeedToBeRegisterd)
            {
                try
                {
                    await guild.DeleteApplicationCommandsAsync();

                    foreach (ICommand command in _commands)
                    {
                        // Check if this command is already registered.
                        if (!registeredGuildCommands.Where(cmd => cmd.Name == command.Name).Any())
                        {
                            await guild.CreateApplicationCommandAsync(command.BuildCommand());
                        }
                    }
                }
                catch (ApplicationCommandException ex)
                {
                    // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                    var json = JsonConvert.SerializeObject(ex.Error, Formatting.Indented);

                    // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                    Console.WriteLine(json);
                }
            }
        }

        _commandHandler = new CommandHandler(_services, _client, _commands);
        await _commandHandler.InstallCommandsAsync();
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}