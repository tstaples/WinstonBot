using Discord;
using System.Reflection;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("version", "Print the bot version number", DefaultPermission.AdminOnly)]
    internal class VersionCommand : CommandBase
    {
        public async override Task HandleCommand(CommandContext context)
        {
            await context.RespondAsync($"Version {Assembly.GetEntryAssembly().GetName().Version}", ephemeral: true);
        }
    }
}
