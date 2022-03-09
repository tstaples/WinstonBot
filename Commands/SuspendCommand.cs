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

            [CommandOption("expiry-date", "Override the default expiration date with a custom one", required: false)]
            public string? ExpirationDate { get; set; }

            public Suspend(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                DateTime? expiry = null;
                if (ExpirationDate != null)
                {
                    try
                    {
                        expiry = DateTime.Parse(ExpirationDate);
                    }
                    catch (FormatException ex)
                    {
                        Logger.LogError($"Failed to parse expiry {ExpirationDate}: {ex.Message}");
                        await context.RespondAsync($"Failed to parse expiry {ExpirationDate}: {ex.Message}", ephemeral: true);
                        return;
                    }
                }

                var eventSuspension = context.ServiceProvider.GetRequiredService<EventControl>();
                var result = await eventSuspension.SuspendUser(User, Reason, expiry);
                if (result == EventControl.SuspendResult.AlreadySuspended)
                {
                    await context.RespondAsync($"{User.Mention} is already suspended", ephemeral: true);
                }
                else
                {
                    var info = eventSuspension.GetSuspensionInfo(User);
                    var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Value.Expiry);
                    await context.RespondAsync($"{User.Mention} has been suspended until {formattedExpiry}", ephemeral: true);
                }
            }
        }

        [SubCommand("un-suspend", "Unsuspends a user from events.", parentCommand: typeof(EventControlCommand))]
        internal class Unsuspend : CommandBase
        {
            [CommandOption("user", "The user to unsuspend")]
            public SocketGuildUser User { get; set; }

            public Unsuspend(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventSuspension = context.ServiceProvider.GetRequiredService<EventControl>();
                await eventSuspension.RemoveSuspensionFromUser(User);
                await context.RespondAsync($"{User.Mention} unsuspended", ephemeral: true);
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
                var eventSuspension = context.ServiceProvider.GetRequiredService<EventControl>();
                var suspendedUsers = eventSuspension.GetSuspendedUsers(context.Guild.Id);

                StringBuilder sb = new StringBuilder();
                foreach (var info in suspendedUsers)
                {
                    var user = context.Guild.GetUser(info.UserId);
                    var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Expiry);
                    sb.AppendLine($"* {user.Mention} Expires: {formattedExpiry} Times Suspended: {info.TimesSuspended}, Last Reason: {info.Reason}");
                }

                if (suspendedUsers.Any())
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
                var eventSuspension = context.ServiceProvider.GetRequiredService<EventControl>();
                eventSuspension.ResetCount(User);
                await context.RespondAsync($"Reset suspension count for {User.Mention}", ephemeral: true);
            }
        }
    }
}
