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

            [CommandOption("notify-user", "DM the user to tell them they've been suspended", required: false)]
            public bool NotifyUser { get; set; } = true;

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
                var result = await eventControl.SuspendUser(User, Reason, NotifyUser, expiry);
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
                        sb.AppendLine($"* {user.Mention} Expires: {formattedExpiry}({formattedRelativeTime})\n" +
                            $"Times Suspended: {info.TimesSuspended}\n" +
                            $"Times Warned: {info.TimesWarned}\n" +
                            $"Last Reason: {info.Reason}\n");
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

        [SubCommand("reset-suspension-count", "Resets the number of times a user has been suspended to 0.", parentCommand: typeof(EventControlCommand))]
        internal class ResetSuspensionCount : CommandBase
        {
            [CommandOption("user", "The user to reset the count for")]
            public SocketGuildUser User { get; set; }

            public ResetSuspensionCount(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                eventControl.ResetSuspensionCount(User);
                await context.RespondAsync($"Reset suspension count for {User.Mention}", ephemeral: true);
            }
        }

        [SubCommand("reset-warning-count", "Resets the number of times a user has been warned to 0.", parentCommand: typeof(EventControlCommand))]
        internal class ResetWarningCount : CommandBase
        {
            [CommandOption("user", "The user to reset the count for")]
            public SocketGuildUser User { get; set; }

            public ResetWarningCount(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
                eventControl.ResetWarningCount(User);
                await context.RespondAsync($"Reset warning count for {User.Mention}", ephemeral: true);
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
                var info = eventControl.GetSuspensionInfo(user);
                if (info != null)
                {
                    if (eventControl.IsUserSuspended(user))
                    {
                        if (info != null)
                        {
                            var formattedExpiry = Discord.TimestampTag.FromDateTime(info.Value.Expiry);
                            var formattedRelativeTime = Discord.TimestampTag.FromDateTime(info.Value.Expiry, Discord.TimestampTagStyles.Relative);
                            await context.RespondAsync($"Your suspension expires on {formattedExpiry}({formattedRelativeTime}).\n" +
                                $"Suspension reason: {info.Value.Reason}.\n" +
                                $"Suspension count: {info.Value.TimesSuspended}\n" +
                                $"Warning count: {info.Value.TimesWarned}",
                                ephemeral: true);
                        }
                    }
                    else
                    {
                        await context.RespondAsync($"You are not currently suspended and have {info.Value.TimesWarned} warning(s)", ephemeral: true);
                    }
                }
                else
                {
                    await context.RespondAsync($"You are not currently suspended and have 0 warnings :)", ephemeral: true);
                }
            }
        }
    }

    [Command("pvm-noshow", "Warns or suspends a user for not showing up to a pvm event.", DefaultPermission.AdminOnly)]
    internal class PvMNoShowCommand : CommandBase
    {
        [CommandOption("user", "The user that didn't show up")]
        public SocketGuildUser User { get; set; }

        [CommandOption("notify-user", "DM the user to tell them they've been warned", required: false)]
        public bool NotifyUser { get; set; } = true;

        public PvMNoShowCommand(ILogger logger) : base(logger)
        {
        }

        public override async Task HandleCommand(CommandContext context)
        {
            var eventControl = context.ServiceProvider.GetRequiredService<EventControl>();
            EventControl.NoShowResult result = await eventControl.HandlePvmNoShow(User, NotifyUser);
            switch (result.ActionTaken)
            {
                case EventControl.NoShowAction.Warned:
                    await SendMessage(context, 
                        $"{User.Mention} has been warned for a pvm no-show. They have {EventControl.MaxWarnings - result.TimesWarned} warnings before being suspended.");
                    break;

                case EventControl.NoShowAction.Suspended:
                    await SendMessage(context, 
                        $"{User.Mention} has been warned for a pvm no-show the max amount of times so " +
                        $"they have been suspended. They will be unsuspended {Discord.TimestampTag.FromDateTime(result.SuspensionExpiry.Value, Discord.TimestampTagStyles.Relative)}.");
                    break;

                case EventControl.NoShowAction.AlreadySuspended:
                    await SendMessage(context, $"{User.Mention} is already suspended until {Discord.TimestampTag.FromDateTime(result.SuspensionExpiry.Value)}");
                    break;
            }
        }

        private async Task SendMessage(CommandContext context, string message)
        {
            SocketTextChannel adminBotSpam = context.Guild.GetTextChannel(831222448388440106);
            await adminBotSpam.SendMessageAsync(message);

            await context.RespondAsync(message, ephemeral: true);
        }
    }
}
