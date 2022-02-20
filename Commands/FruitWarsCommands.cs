using Discord;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WinstonBot.Attributes;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace WinstonBot.Commands
{
    [Command("leaderboard", "Shows the Fruit Wars Leaderboard", DefaultPermission.AdminOnly)]
    internal class FruitWarsCommands : CommandBase
    {
        public FruitWarsCommands(ILogger logger) : base(logger) { }

        public async override Task HandleCommand(CommandContext context)
        {
            // Need to DeferAsync() here as the scarpe takes ~60sec
            await context.SlashCommand.DeferAsync();

            ScriptEngine engine = Python.CreateEngine();
            var searchPaths = engine.GetSearchPaths();
            searchPaths.Add("/usr/lib/python3/dist-packages");
            engine.SetSearchPaths(searchPaths);
            ScriptScope scope = engine.CreateScope();
            
            engine.ExecuteFile(@"dxpscrape.py");
            dynamic output = scope.GetVariable("allFormatted");

            // Need to FollowUpAsync() instead once DeferAsync() is added
            await context.SlashCommand.FollowupAsync($"{output}", ephemeral: true);
        }
    }
}
