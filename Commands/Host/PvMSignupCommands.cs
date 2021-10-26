using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    [Command("pvm-signup", "Utilities for the host-pvm-signup command", DefaultPermission.AdminOnly)]
    internal class PvMSignupCommands : CommandBase
    {
        public PvMSignupCommands(ILogger logger) : base(logger)
        {
        }

        [SubCommand("add-user", "Adds a user to the signup in the indicated message", parentCommand: typeof(PvMSignupCommands))]
        internal class AddUserToSignup : CommandBase
        {
            [CommandOption("user", "The user to add to the signup")]
            public SocketGuildUser User {  get; set; }

            [CommandOption("boss", "The boss the signup is for", dataProvider: typeof(SignupBossChoiceDataProvider))]
            public long BossIndex { get; set; }

            [CommandOption("signup-message-id", "The id of the signup message to target")]
            public string TargetSignupMessageId { get; set; }

            public AddUserToSignup(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                ulong messageId = 0;
                if (!ulong.TryParse(TargetSignupMessageId, out messageId))
                {
                    throw new ArgumentNullException("Invalid message id");
                }

                var channel = context.Guild.GetTextChannel(context.ChannelId);
                var message = await channel.GetMessageAsync(messageId);

                // TODO: reduce duplication with SignupAction
                if (!message.Embeds.Any())
                {
                    await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                    return;
                }

                var bossEntry = BossData.Entries[BossIndex];

                var configService = context.ServiceProvider.GetRequiredService<ConfigService>();
                List<ulong> rolesForBoss = new();
                if (configService.Configuration.GuildEntries[context.Guild.Id].RolesNeededForBoss.TryGetValue(bossEntry.CommandName, out rolesForBoss))
                {
                    if (!Utility.DoesUserHaveAnyRequiredRole(User, rolesForBoss))
                    {
                        await context.RespondAsync(
                            $"{User.Mention} must have one of the following roles to sign up:\n{Utility.JoinRoleMentions(context.Guild, rolesForBoss)}\n" +
                            $"Please see #pvm-rules.", ephemeral: true);
                        return;
                    }
                }

                var currentEmbed = message.Embeds.First();

                var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
                var ids = HostHelpers.ParseNamesToIdList(names);
                if (ids.Contains(User.Id))
                {
                    Logger.LogDebug($"{User.Mention} is already signed up: ignoring.");
                    await context.RespondAsync($"{User.Mention} is already signed up.", ephemeral: true);
                    return;
                }

                Logger.LogInformation($"{User.Mention} has signed up for {messageId}!");
                names.Add(User.Mention);

                await channel.ModifyMessageAsync(messageId, msgProps =>
                {
                    msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                });

                await context.RespondAsync($"Added {User.Mention} to the signup!", ephemeral: true);
            }
        }

        [SubCommand("remove-user", "Removes a user from the signup in the indicated message", parentCommand: typeof(PvMSignupCommands))]
        internal class RemoveUserFromSignup : CommandBase
        {
            [CommandOption("user", "The user to remove from the signup")]
            public SocketGuildUser User { get; set; }

            [CommandOption("boss", "The boss the signup is for", dataProvider: typeof(SignupBossChoiceDataProvider))]
            public long BossIndex { get; set; }

            [CommandOption("signup-message-id", "The id of the signup message to target")]
            public string TargetSignupMessageId { get; set; }

            public RemoveUserFromSignup(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                ulong messageId = 0;
                if (!ulong.TryParse(TargetSignupMessageId, out messageId))
                {
                    throw new ArgumentNullException("Invalid message id");
                }

                var channel = context.Guild.GetTextChannel(context.ChannelId);
                var message = await channel.GetMessageAsync(messageId);

                if (!message.Embeds.Any())
                {
                    await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                    return;
                }

                var currentEmbed = message.Embeds.First();
                var names = HostHelpers.ParseNamesToList(currentEmbed.Description);
                var ids = HostHelpers.ParseNamesToIdList(names);
                if (!ids.Contains(User.Id))
                {
                    Console.WriteLine($"{User.Mention} isn't signed up: ignoring.");
                    await context.RespondAsync($"{User.Mention} isn't signed up.", ephemeral: true);
                    return;
                }

                Logger.LogInformation($"{User.Mention} has quit signup {messageId}!");
                var index = ids.IndexOf(User.Id);
                names.RemoveAt(index);

                await channel.ModifyMessageAsync(messageId, msgProps =>
                {
                    msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names);
                });

                await context.RespondAsync($"Removed {User.Mention} from the signup.", ephemeral: true);
            }
        }
    }
}
