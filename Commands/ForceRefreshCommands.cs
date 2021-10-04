using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot.Commands
{
    public class ForceRefreshCommands : ICommand
    {
        public string Name => "force-refresh-commands";
        public int Id => 5; // TODO: just remove this and use names for options menu.
        public ICommand.Permission DefaultPermission => ICommand.Permission.AdminOnly;
        public ulong AppCommandId { get; set; }
        public IEnumerable<IAction> Actions => new List<IAction>();

        private CommandHandler _commandHandler;

        public ForceRefreshCommands(CommandHandler commandHandler)
        {
            _commandHandler = commandHandler;
        }

        public SlashCommandProperties BuildCommand()
        {
            var configureCommands = new SlashCommandBuilder()
                .WithName(Name)
                .WithDefaultPermission(false)
                .WithDescription("Delete all applications commands and re-create them.");

            return configureCommands.Build();
        }

        public async Task HandleCommand(CommandContext context)
        {
            if (context.SlashCommand.Channel is SocketGuildChannel channel)
            {
                Console.WriteLine("Clearing all commands for this bot from guild:");
                await channel.Guild.DeleteApplicationCommandsAsync();

                Console.WriteLine("Registering commands");
                await RegisterCommands(context.Client, channel.Guild, _commandHandler.Commands);

                await context.SlashCommand.RespondAsync("All commands refreshed", ephemeral: true);
            }
        }

        public static async Task RegisterCommands(DiscordSocketClient client, SocketGuild guild, IEnumerable<ICommand> commands)
        {
            // Register the commands in all the guilds
            // NOTE: registering the same command will just update it, so we won't hit the 200 command create rate limit.

            // TODO: batch update breaks because we pass in more than 10 roles in the dict since there's 17 admin roles.
            //var adminRoles = guild.Roles.Where(role => role.Permissions.Administrator);
            var adminRoles = guild.Roles.Where(role => role.Id == 773757083904114689);
            

            try
            {
                foreach (ICommand command in commands)
                {
                    Console.WriteLine($"Registering command {command.Name}.");
                    SocketApplicationCommand appCommand = await guild.CreateApplicationCommandAsync(command.BuildCommand());
                    if (appCommand == null)
                    {
                        Console.WriteLine($"Failed to register command: {command.Name}");
                        continue;
                    }

                    command.AppCommandId = appCommand.Id;
                }
            }
            catch (ApplicationCommandException ex)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(ex.Error, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }

            // Setup default command permissions
            var permDict = new Dictionary<ulong, ApplicationCommandPermission[]>();
            foreach (ICommand command in commands)
            {
                List<ApplicationCommandPermission> perms = new();
                if (command.DefaultPermission == ICommand.Permission.AdminOnly)
                {
                    foreach (var role in adminRoles)
                    {
                        perms.Add(new ApplicationCommandPermission(role, true));
                    }
                }

                permDict.Add(command.AppCommandId, perms.ToArray());
            }

            await client.Rest.BatchEditGuildCommandPermissions(guild.Id, permDict);
        }

        public ActionContext CreateActionContext(DiscordSocketClient client, SocketMessageComponent arg, IServiceProvider services)
        {
            return new ActionContext(client, arg, services);
        }
    }
}
