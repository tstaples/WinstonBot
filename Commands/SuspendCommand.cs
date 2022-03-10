using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using WinstonBot.Attributes;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Command("event-control", "Handles event suspension", DefaultPermission.AdminOnly)]
    internal class EventControlCommand : CommandBase
    {
        public EventControlCommand(ILogger logger) : base(logger)
        {
        }

        [SubCommand("suspend", "Suspends a user from events for the given time.", parentCommand: typeof(EventControlCommand))]
        internal class Suspend : CommandBase
        {
            [CommandOption("user", "The user to suspend")]
            public SocketGuildUser User { get; set; }

            [CommandOption("reason", "The reason the user was suspended")]
            public string Reason { get; set; }

            [CommandOption("days-to-suspend", "Override the default expiration date with a custom number of days", required: false)]
            public long CustomNumberOfDays { get; set; } = 0;

            public Suspend(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                DateTime? expiry = null;
                if (CustomNumberOfDays > 0)
                {
#if DEBUG
                    expiry = DateTime.UtcNow + TimeSpan.FromMinutes(CustomNumberOfDays);
#else
                    expiry = DateTime.UtcNow + TimeSpan.FromDays(CustomNumberOfDays);
#endif
                }

                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                var result = await eventControl.SuspendUser(User, Reason, expiry);
                if (result == EventControl.SuspendResult.AlreadySuspended)
                {
                    await context.RespondAsync($"{User.Mention} is already suspended", ephemeral: true);
                }
                else
                {
                    var info = eventControl.GetSuspensionInfo(User);
                    var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Value.Expiry);
                    var formattedRelativeTime = Discord.TimestampTag.FromDateTime(info.Value.Expiry, Discord.TimestampTagStyles.Relative);
                    await context.RespondAsync($"{User.Mention} has been suspended until {formattedExpiry} and will be un-suspended {formattedRelativeTime}", ephemeral: true);
                }
            }
        }

        [SubCommand("un-suspend", "Unsuspends a user from events.", parentCommand: typeof(EventControlCommand))]
        internal class Unsuspend : CommandBase
        {
            [CommandOption("user", "The user to unsuspend")]
            public SocketGuildUser User { get; set; }

            [CommandOption("notify-user", "DM the user to tell them they've been unsuspended", required: false)]
            public bool NotifyUser { get; set; } = true;

            public Unsuspend(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                if (eventControl.IsUserSuspended(User))
                {
                    await eventControl.RemoveSuspensionFromUser(User, NotifyUser);
                    await context.RespondAsync($"{User.Mention} unsuspended", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{User.Mention} isn't suspended", ephemeral: true);
                }
            }
        }

        [SubCommand("list-suspensions", "Lists all suspended users.", parentCommand: typeof(EventControlCommand))]
        internal class ListSuspensions : CommandBase
        {
            public ListSuspensions(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                var suspendedUsers = eventControl.GetSuspendedUsers(context.Guild.Id);

                StringBuilder sb = new StringBuilder();
                foreach (var info in suspendedUsers)
                {
                    if (info.Expiry > DateTime.UtcNow)
                    {
                        var user = context.Guild.GetUser(info.UserId);
                        var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Expiry);
                        var formattedRelativeTime = Discord.TimestampTag.FromDateTime(info.Expiry, Discord.TimestampTagStyles.Relative);
                        sb.AppendLine($"* {user.Mention} Expires: {formattedExpiry}({formattedRelativeTime}) Times Suspended: {info.TimesSuspended}, Last Reason: {info.Reason}");
                    }
                }

                if (sb.Length > 0)
                {
                    await context.RespondAsync(sb.ToString(), ephemeral: true);
                }
                else
                {
                    await context.RespondAsync("No users currently suspended", ephemeral: true);
                }
            }
        }

        [SubCommand("reset-suspension-count", "Lists all suspended users.", parentCommand: typeof(EventControlCommand))]
        internal class ResetCount : CommandBase
        {
            [CommandOption("user", "The user to reset the count for")]
            public SocketGuildUser User { get; set; }

            public ResetCount(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                eventControl.ResetCount(User);
                await context.RespondAsync($"Reset suspension count for {User.Mention}", ephemeral: true);
            }
        }

        [SubCommand("check-suspension-expiry", "Shows when your suspension will expire.", parentCommand: typeof(EventControlCommand), defaultPermissionOverride: DefaultPermission.Everyone)]
        internal class CheckSuspensionExpiry : CommandBase
        {
            public CheckSuspensionExpiry(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                var user = (SocketGuildUser)context.User;
                if (eventControl.IsUserSuspended(user))
                {
                    var info = eventControl.GetSuspensionInfo(user);
                    if (info != null)
                    {
                        var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Value.Expiry);
                        var formattedRelativeTime = Discord.TimestampTag.FromDateTime(info.Value.Expiry, Discord.TimestampTagStyles.Relative);
                        await context.RespondAsync($"Your suspension expires on {formattedExpiry}({formattedRelativeTime}).\n" +
                            $"Suspension reason: {info.Value.Reason}.\n" +
                            $"Suspension count: {info.Value.TimesSuspended}", ephemeral: true);
                    }
                }
                else
                {
                    await context.RespondAsync($"You are not currently suspended.", ephemeral: true);
                }
            }
        }
    }
}
