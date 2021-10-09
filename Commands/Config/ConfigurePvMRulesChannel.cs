using Discord;
using Discord.WebSocket;
using WinstonBot.Services;
using WinstonBot.Attributes;
using WinstonBot.Data;

namespace WinstonBot.Commands.Config
{
    // TODO: add set/view sub commands
    [SubCommand("pvm-rules-channel", "Sets the pvm rules channel that we point people to.", typeof(ConfigCommand))]
    internal class ConfigurePvMRulesChannel : CommandBase
    {
        [CommandOption("channel", "The channel to set as the pvm rules channel")]
        public SocketGuildChannel TargetChannel { get; set;  }

        public static new CommandContext CreateContext(DiscordSocketClient client, SocketSlashCommand arg, IServiceProvider services)
        {
            return new ConfigCommandContext(client, arg, services);
        }

        public async override Task HandleCommand(CommandContext commandContext)
        {
            var context = (ConfigCommandContext)commandContext;
            var configService = context.ConfigService;
            var guildEntry = Utility.GetOrAdd(configService.Configuration.GuildEntries, context.Guild.Id);
            guildEntry.PvMRulesChannelId = TargetChannel.Id;
            configService.UpdateConfig(configService.Configuration);

            await context.RespondAsync($"Set PvM rules channel to {TargetChannel.Name}");
        }
    }
}
