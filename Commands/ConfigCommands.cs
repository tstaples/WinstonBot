using Discord.Commands;
using WinstonBot.Services;
using WinstonBot.MessageHandlers;
using Discord;
using Microsoft.Extensions.DependencyInjection;

namespace WinstonBot.Commands
{
	[Group("config")]
    public class ConfigCommands : ModuleBase<CommandContext>
    {
        [Group("set")]
        public class Set : ModuleBase<CommandContext>
        {
            [Command("teamconfirmationchannel")]
            public async Task SetTeamConfirmationChannel(IChannel channel)
            {
                Console.WriteLine($"Updating team confirmation channel id to {channel.Id}");

                var configService = Context.ServiceProvider.GetRequiredService<ConfigService>();
                var config = configService.Configuration;
                config.TeamConfirmationChannelId = channel.Id;
                configService.UpdateConfig(config);
                await Context.Channel.SendMessageAsync("Team confirmation channel updated!");
            }
        }

        [Group("get")]
        public class Get : ModuleBase<CommandContext>
        {
            [Command("teamconfirmationchannel")]
            public async Task GetTeamConfirmationChannel()
            {
                var configService = Context.ServiceProvider.GetRequiredService<ConfigService>();
                await Context.Channel.SendMessageAsync($"Team confirmation channel is: {configService.Configuration.TeamConfirmationChannelId}");
            }
        }
    }
}
