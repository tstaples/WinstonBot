using WinstonBot.Attributes;

namespace WinstonBot.Commands
{
    [Command("restart", "Restart the bot", DefaultPermission.AdminOnly)]
    internal class RestartCommand : CommandBase
    {
        public override async Task HandleCommand(CommandContext context)
        {
            await context.RespondAsync("Restarting right meow");

            Console.WriteLine("Stopping client...");
            await context.Client.StopAsync();

            context.Client.Disconnected += (args) =>
            {
                Console.WriteLine("Exiting!");
                Environment.Exit(1);
                return Task.CompletedTask;
            };
        }
    }

#if DEBUG
    [Command("delete-commands", "Delete all commands", DefaultPermission.AdminOnly)]
    internal class DeleteCommands : CommandBase
    {
        public override async Task HandleCommand(CommandContext context)
        {
            Console.WriteLine("Deleting guild commands");
            await context.Guild.DeleteApplicationCommandsAsync();
        }
    }
#endif
}
