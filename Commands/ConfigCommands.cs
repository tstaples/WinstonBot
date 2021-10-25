using WinstonBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using WinstonBot.Attributes;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands
{
    public class ConfigCommandContext : CommandContext
    {
        public SocketGuild Guild { get; set; }
        public ConfigService ConfigService => ServiceProvider.GetRequiredService<ConfigService>();

        public ConfigCommandContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services) : base(client, arg, services)
        {
            Guild = ((SocketGuildChannel)arg.Channel).Guild;
        }
    }

    /// <summary>
    /// See Commands/Config for actual config subcommands.
    /// </summary>
    [Command("configure", "Configure the bot.", DefaultPermission.AdminOnly)]
    public class ConfigCommand : CommandBase
    {
        public ConfigCommand(ILogger logger) : base(logger) { }

        public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
        }
    }
}
