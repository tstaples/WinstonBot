using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using WinstonBot.Attributes;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Command("watch-cat", "He's watching you.", DefaultPermission.AdminOnly)]
    internal class WatchCatCommands : CommandBase
    {
        public WatchCatCommands(ILogger logger) : base(logger)
        {
        }

        [SubCommand("set-notify-channel", "Sets the channel to send notifications to", typeof(WatchCatCommands))]
        internal class SetNotifyChannel : CommandBase
        {
            [CommandOption("channel", "The channel to send notifications to")]
            public SocketGuildChannel Channel { get; set; }

            public SetNotifyChannel(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();
                db.SetNotifyChannel(context.Guild.Id, Channel.Id);
                await context.RespondAsync($"Set notify channel to {Channel.Name}", ephemeral: true);
            }
        }

        [SubCommand("set-notify-role", "Sets the role to ping when we notify that a user joined", typeof(WatchCatCommands))]
        internal class SetNotifyRole : CommandBase
        {
            [CommandOption("channel", "The channel to send notifications to")]
            public SocketRole Role { get; set; }

            public SetNotifyRole(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();
                db.SetNotifyRole(context.Guild.Id, Role.Id);
                await context.RespondAsync($"Set notify channel to {Role.Name}", ephemeral: true);
            }
        }

        [SubCommand("add-user-by-name", "Adds a user to be watched", typeof(WatchCatCommands))]
        internal class AddUserByName : CommandBase
        {
            [CommandOption("username", "Full username including discriminator (eg Catman#6968)")]
            public string FullUserName {  get; set; }

            [CommandOption("action", "The action to take when this user joins", dataProvider:typeof(WatchCatActionDataProvider))]
            public long Action { get; set; }

            public AddUserByName(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();
                try
                {
                    var parts = FullUserName.Split('#');
                    string username = parts[0];
                    string discrim = parts[1];
                    var action = (WatchCatDB.UserAction)Action;

                    db.AddUser(context.Guild.Id, username, discrim, action);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to parse username from {FullUserName}");
                    await context.RespondAsync($"Invalid username format", ephemeral: true);
                }

                await context.RespondAsync($"Now watching for user {FullUserName} meow");
            }
        }

        [SubCommand("add-user-by-id", "Adds a user to be watched", typeof(WatchCatCommands))]
        internal class AddUserById : CommandBase
        {
            [CommandOption("id", "User id found from copying the id of the user (need dev mode on)")]
            public string UserId { get; set; }

            [CommandOption("action", "The action to take when this user joins", dataProvider: typeof(WatchCatActionDataProvider))]
            public long Action { get; set; }

            public AddUserById(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                ulong id = 0;
                if (!ulong.TryParse(UserId, out id))
                {
                    await context.RespondAsync("Invalid user id", ephemeral: true);
                    return;
                }

                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();
                var action = (WatchCatDB.UserAction)Action;

                db.AddUser(context.Guild.Id, id, action);

                await context.RespondAsync($"Now watching for user {id} meow");
            }
        }

        [SubCommand("remove-user-by-name", "Stops watching for a user", typeof(WatchCatCommands))]
        internal class RemoveUserByName : CommandBase
        {
            [CommandOption("username", "Full username including discriminator (eg Catman#6968)")]
            public string FullUserName { get; set; }

            public RemoveUserByName(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();

                bool success = false;
                try
                {
                    var parts = FullUserName.Split('#');
                    string username = parts[0];
                    string discrim = parts[1];

                    success = db.RemoveUser(context.Guild.Id, username, discrim);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to parse username from {FullUserName}: {ex.Message}");
                    await context.RespondAsync($"Invalid username format", ephemeral: true);
                }

                if (success)
                {
                    await context.RespondAsync($"No longer watching for user {FullUserName} meow");
                }
                else
                {
                    await context.RespondAsync($"{FullUserName} was not being watched", ephemeral: true);
                }
            }
        }

        [SubCommand("remove-user-by-id", "Stops watching for a user", typeof(WatchCatCommands))]
        internal class RemoveUserById : CommandBase
        {
            [CommandOption("id", "User id found from copying the id of the user (need dev mode on)")]
            public string UserId { get; set; }

            public RemoveUserById(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                ulong id = 0;
                if (!ulong.TryParse(UserId, out id))
                {
                    await context.RespondAsync("Invalid user id", ephemeral: true);
                    return;
                }

                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();

                if (db.RemoveUser(context.Guild.Id, id))
                {
                    await context.RespondAsync($"No longer watching for user {id} meow");
                }
                else
                {
                    await context.RespondAsync($"User {id} is not being watched.", ephemeral: true);
                }
            }
        }

        [SubCommand("view-users", "View the users being tracked", typeof(WatchCatCommands))]
        internal class ViewTrackedUsers : CommandBase
        {
            public ViewTrackedUsers(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<WatchCatDB>();
                var entry = db.GetEntry(context.Guild.Id);

                StringBuilder builder = new StringBuilder()
                    .Append("Watched users:\n");

                foreach (WatchCatDB.UserEntry userEntry in entry.Entries)
                {
                    builder
                        .Append($"* {GetUserString(userEntry)} **Action**: {userEntry.Action}")
                        .AppendLine();
                }

                await context.RespondAsync(builder.ToString());
            }

            private string GetUserString(WatchCatDB.UserEntry entry)
            {
                if (entry.Id != null)
                {
                    return entry.Id.ToString();
                }
                return $"{entry.Username}#{entry.Discriminator}";
            }
        }
    }
}
