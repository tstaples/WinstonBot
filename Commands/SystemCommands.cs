using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("restart", "Restart the bot", DefaultPermission.AdminOnly)]
    internal class RestartCommand : CommandBase
    {
        public RestartCommand(ILogger logger) : base(logger) { }

        public override async Task HandleCommand(CommandContext context)
        {
            await context.RespondAsync("Restarting right meow");

            Logger.LogInformation("Stopping client...");
            await context.Client.StopAsync();

            context.Client.Disconnected += (args) =>
            {
                Logger.LogInformation("Exiting!");
                Environment.Exit(1);
                return Task.CompletedTask;
            };
        }
    }

#if DEBUG
    [Command("delete-commands", "Delete all commands", DefaultPermission.AdminOnly)]
    internal class DeleteCommands : CommandBase
    {
        public DeleteCommands(ILogger logger) : base(logger) { }

        public override async Task HandleCommand(CommandContext context)
        {
            Logger.LogInformation("Deleting guild commands");
            await context.Guild.DeleteApplicationCommandsAsync();
            await context.RespondAsync("Guild commands deleted!", ephemeral: true);
        }
    }
#endif
}
