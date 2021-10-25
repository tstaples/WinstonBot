using Discord;
using Discord.WebSocket;
using WinstonBot.Services;
using WinstonBot.Attributes;
using WinstonBot.Data;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands.Config
{
    [SubCommand("boss-signup", "Configure roles for boss signups", typeof(ConfigCommand))]
    internal class ConfigureBossSignup : CommandBase
    {
        public ConfigureBossSignup(ILogger logger) : base(logger) { }

        [SubCommand("add-role", "Add a signup role requirement to this boss.", typeof(ConfigureBossSignup))]
        private class AddRoleOperation : CommandBase
        {
            [CommandOption("boss", "The boss to modify", dataProvider: typeof(BossChoiceDataProvider))]
            public long TargetBoss { get; set; }

            [CommandOption("role", "The role to add")]
            public SocketRole TargetRole { get; set; }

            private BossData.Entry BossEntry => BossData.Entries[TargetBoss];

            public AddRoleOperation(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                if (!BossData.ValidBossIndex(TargetBoss))
                {
                    await context.RespondAsync($"Invalid boss selection: {TargetBoss}", ephemeral: true);
                    return;
                }

                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot add {context.Guild.EveryoneRole.Mention} as it is the default.\n" +
                        $"To set a command to {context.Guild.EveryoneRole.Mention}, remove all roles for it.", ephemeral: true);
                    return;
                }

                var configService = context.ConfigService;
                var rolesForBosses = Utility.GetOrAdd(configService.Configuration.GuildEntries, context.Guild.Id).RolesNeededForBoss;
                var bossEntry = Utility.GetOrAdd(rolesForBosses, BossEntry.CommandName);
                if (Utility.AddUnique(bossEntry, TargetRole.Id))
                {
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Added signup role requirement: {TargetRole.Mention} to boss {BossEntry.PrettyName}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{BossEntry.PrettyName} already contains role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand("remove-role", "Remove a signup role requirement to this boss.", typeof(ConfigureBossSignup))]
        private class RemoveRoleOperation : CommandBase
        {
            [CommandOption("boss", "The boss to modify", dataProvider: typeof(BossChoiceDataProvider))]
            public long TargetBoss { get; set; }

            [CommandOption("role", "The role to remove")]
            public SocketRole TargetRole { get; set; }

            private BossData.Entry BossEntry => BossData.Entries[TargetBoss];

            public RemoveRoleOperation(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                if (!BossData.ValidBossIndex(TargetBoss))
                {
                    await context.RespondAsync($"Invalid boss selection: {TargetBoss}", ephemeral: true);
                    return;
                }

                if (TargetRole.Id == context.Guild.EveryoneRole.Id)
                {
                    await context.RespondAsync($"Cannot remove {context.Guild.EveryoneRole.Mention} as it is the default.\n" +
                        $"To remove {context.Guild.EveryoneRole.Mention}, add a specific role for it.", ephemeral: true);
                    return;
                }

                string bossName = BossEntry.CommandName;

                var configService = context.ConfigService;
                var rolesForBosses = Utility.GetOrAdd(configService.Configuration.GuildEntries, context.Guild.Id).RolesNeededForBoss;
                if (rolesForBosses.ContainsKey(bossName) && rolesForBosses[bossName].Contains(TargetRole.Id))
                {
                    rolesForBosses[bossName].Remove(TargetRole.Id);
                    configService.UpdateConfig(configService.Configuration);
                    await context.RespondAsync($"Removed signup role requirement: {TargetRole.Mention} from boss {BossEntry.PrettyName}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"{BossEntry.PrettyName} doesn't contains role {TargetRole.Mention}", ephemeral: true);
                }
            }
        }

        [SubCommand("view-roles", "View the role requirements for this boss", typeof(ConfigureBossSignup))]
        private class ViewRolesOperation : CommandBase
        {
            [CommandOption("boss", "The boss to view the roles for", dataProvider: typeof(BossChoiceDataProvider))]
            public long TargetBoss { get; set; }

            private BossData.Entry BossEntry => BossData.Entries[TargetBoss];

            public ViewRolesOperation(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext commandContext)
            {
                var context = (ConfigCommandContext)commandContext;

                if (!BossData.ValidBossIndex(TargetBoss))
                {
                    await context.RespondAsync($"Invalid boss selection: {TargetBoss}", ephemeral: true);
                    return;
                }

                var guildEntries = context.ConfigService.Configuration.GuildEntries;
                if (!guildEntries.ContainsKey(context.Guild.Id))
                {
                    await context.RespondAsync("No entry found for this guild.", ephemeral: true);
                    return;
                }

                string bossName = BossEntry.CommandName;

                List<ulong> roles = new();
                if (guildEntries[context.Guild.Id].RolesNeededForBoss.TryGetValue(bossName, out roles))
                {
                    await context.RespondAsync($"Signup roles for {BossEntry.PrettyName}: \n{Utility.JoinRoleMentions(context.Guild, roles)}", ephemeral: true);
                }
                else
                {
                    await context.RespondAsync($"Signup roles for {BossEntry.PrettyName}: \n{context.Guild.EveryoneRole.Mention}", ephemeral: true);
                }
            }
        }
    }
}
