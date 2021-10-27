using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;
using WinstonBot.Data;

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
                // TODO: use semaphore
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

                var currentEmbed = message.Embeds.First();
                if (currentEmbed.Fields.Any())
                {
                    await context.RespondAsync($"Cannot add a user to a completed team. User pvm-signup set-role to modify after a team has been completed.", ephemeral: true);
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
                // TODO: use semaphore
                // TODO: don't allow running this on a completed team.
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
                if (currentEmbed.Fields.Any())
                {
                    await context.RespondAsync($"Cannot add a user to a completed team. User pvm-signup set-role to modify after a team has been completed.", ephemeral: true);
                    return;
                }

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

        [SubCommand("log-team", "Logs a team to the history in the DB", dynamicSubcommands: true, parentCommand: typeof(PvMSignupCommands))]
        internal class AddTeamToHistory : CommandBase
        {
            public override bool WantsToHandleSubCommands => true;

            public AddTeamToHistory(ILogger logger) : base(logger)
            {
            }

            public static new SlashCommandOptionBuilder BuildCommandOption(ILogger logger)
            {
                var builder = new SlashCommandOptionBuilder()
                    .WithName("log-team")
                    .WithDescription("Logs a team to the history in the DB")
                    .WithType(ApplicationCommandOptionType.SubCommandGroup);

                foreach (BossData.Entry entry in BossData.Entries)
                {
                    if (!entry.SupportsSignup || entry.RolesEnumType == null)
                    {
                        continue;
                    }

                    var entryBuilder = new SlashCommandOptionBuilder()
                        .WithName(entry.CommandName)
                        .WithDescription(entry.PrettyName)
                        .WithType(ApplicationCommandOptionType.SubCommand);

                    foreach (var role in Enum.GetNames(entry.RolesEnumType))
                    {
                        entryBuilder.AddOption(role.ToLower(), ApplicationCommandOptionType.User, "The user for this role", required: false);
                    }

                    builder.AddOption(entryBuilder);
                }
                return builder;
            }

            public override async Task HandleSubCommand(CommandContext context, CommandInfo subCommandInfo, IEnumerable<CommandDataOption>? options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException("Expected valid options");
                }

                string bossName = options.First().Name;
                var roleOptions = options.First().Options;
                if (roleOptions == null)
                {
                    throw new ArgumentNullException("Expected valid roles");
                }

                Dictionary<string, ulong> team = roleOptions.ToDictionary(opt => opt.Name, opt => Utility.SafeGetUserId((SocketGuildUser)opt.Value));

                context.ServiceProvider.GetRequiredService<AoDDatabase>().AddTeamToHistory(team);

                await context.RespondAsync("Added team to history", ephemeral:true);
            }
        }

        [SubCommand("set-role", "Sets a user as the selected role on a completed team..", typeof(PvMSignupCommands))]
        internal class SetRole : CommandBase
        {
            [CommandOption("signup-message-id", "The id of the signup message to target")]
            public string TargetSignupMessageId { get; set; }

            [CommandOption("role", "The name of the role to set (case-insensitive)")]
            public string RoleName { get; set; }

            [CommandOption("user", "The user to set as this role. If empty we just clear the role.", required: false)]
            public SocketGuildUser? UserToAdd { get; set; }

            public SetRole(ILogger logger) : base(logger)
            {
            }

            public override async Task HandleCommand(CommandContext context)
            {
                ulong messageId = 0;
                if (!ulong.TryParse(TargetSignupMessageId, out messageId))
                {
                    throw new ArgumentNullException("Invalid message id");
                }

                // TODO: use semaphore
                var channel = context.Guild.GetTextChannel(context.ChannelId);
                var message = await channel.GetMessageAsync(messageId);
                if (message == null)
                {
                    throw new ArgumentNullException($"Signup message doesn't exist: id = {messageId}");
                }

                bool success = false;
                IEmbed embed = message.Embeds.First();
                if (embed == null)
                {
                    throw new ArgumentNullException("$Target message is missing an embed.");
                }

                var builder = Utility.CreateBuilderForEmbed(embed);

                Dictionary<string, ulong> team = builder.Fields.ToDictionary(ks => ks.Name, vs => Utility.GetUserIdFromMention((string)vs.Value));

                string userToAddMention = UserToAdd != null ? UserToAdd.Mention : "None";
                foreach (var field in builder.Fields)
                {
                    if (field.Name.ToLower() == RoleName.ToLower())
                    {
                        if (UserToAdd != null && team.ContainsValue(UserToAdd.Id))
                        {
                            await context.RespondAsync($"{UserToAdd.Mention} is already on the team.", ephemeral:true);
                            return;
                        }
                        else
                        {
                            success = true;
                            field.Value = userToAddMention;
                            team[field.Name] = UserToAdd != null ? UserToAdd.Id : 0;
                        }
                        break;
                    }
                }

                if (success)
                {
                    // TODO: we should store the DB row in the message or validate the team names so 
                    var aodDb = context.ServiceProvider.GetRequiredService<AoDDatabase>();
                    aodDb.RemoveLastRowFromHistory();

                    aodDb.AddTeamToHistory(team);

                    await channel.ModifyMessageAsync(messageId, props =>
                    {
                        props.Embed = builder.Build();
                    });

                    Logger.LogInformation($"Set role {RoleName} with {userToAddMention}.");
                    await context.RespondAsync($"Set role {RoleName} with {userToAddMention}.", ephemeral: true);
                }
                else
                {
                    Logger.LogInformation($"Didn't find role {RoleName} in any team.");
                    await context.RespondAsync($"Didn't find role {RoleName} in any team.", ephemeral: true);
                }
            }
        }
    }
}
