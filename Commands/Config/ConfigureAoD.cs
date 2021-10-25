using Discord.WebSocket;
using WinstonBot.Services;
using WinstonBot.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using Discord;
using Microsoft.Extensions.Logging;

namespace WinstonBot.Commands.Config
{
    [SubCommand("aod", "Configure aod", typeof(ConfigCommand))]
    internal class ConfigureAoD : CommandBase
    {
        public ConfigureAoD(ILogger logger) : base(logger) { }

        [SubCommand("add-user", "Add a user to the aod database.", typeof(ConfigureAoD))]
        private class AddUser : CommandBase
        {
            [CommandOption("user", "The user to add to the DB.")]
            public SocketGuildUser TargetUser { get; set; }

            [CommandOption("base", "Weight for the base role (0-1)", required: false)]
            public double Base { get; set; } = 0f;
            [CommandOption("chin", "Weight for the chin role (0-1)", required: false)]
            public double Chin { get; set; } = 0f;
            [CommandOption("hammer", "Weight for the hammer role (0-1)", required: false)]
            public double Hammer { get; set; } = 0f;
            [CommandOption("u", "Weight for the u role (0-1)", required: false)]
            public double U { get; set; } = 0f;
            [CommandOption("g", "Weight for the g role (0-1)", required: false)]
            public double G { get; set; } = 0f;
            [CommandOption("c", "Weight for the c role (0-1)", required: false)]
            public double C { get; set; } = 0f;
            [CommandOption("f", "Weight for the f role (0-1)", required: false)]
            public double F { get; set; } = 0f;

            public AddUser(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext context)
            {
                double[] weights = new double[] { Base, Chin, Hammer, U, G, C, F };
                weights = weights.Select(x => Math.Clamp(x, 0f, 1f)).ToArray();
                if (weights.Sum() == 0f)
                {
                    weights = AoDDatabase.GetDefaultRoleWeights();
                }

                var db = context.ServiceProvider.GetRequiredService<AoDDatabase>();
                try
                {
                    db.AddUser(TargetUser.Id, TargetUser.Username, weights);
                    await context.RespondAsync($"Added user {TargetUser.Mention} with weights: {String.Join(',', weights)}", ephemeral: true);
                }
                catch (AoDDatabase.DBOperationFailedException ex)
                {
                    await context.RespondAsync($"Operation failed: {ex.Message}.", ephemeral: true);
                    throw;
                }
                catch (Exception ex)
                {
                    await context.RespondAsync($"Failed to add user to the DB: {ex.Message}.", ephemeral: true);
                }
            }
        }

        [SubCommand("set-role-weights", "Sets the role weights for a user.", typeof(ConfigureAoD))]
        private class SetRoleWeights : CommandBase
        {
            [CommandOption("user", "The user to modify.")]
            public SocketGuildUser TargetUser { get; set; }

            // TODO: put these in a struct and support inlining struct parameters
            [CommandOption("base", "Weight for the base role (0-1)", required: false)]
            public double Base { get; set; } = 0f;
            [CommandOption("chin", "Weight for the chin role (0-1)", required: false)]
            public double Chin { get; set; } = 0f;
            [CommandOption("hammer", "Weight for the hammer role (0-1)", required: false)]
            public double Hammer { get; set; } = 0f;
            [CommandOption("u", "Weight for the u role (0-1)", required: false)]
            public double U { get; set; } = 0f;
            [CommandOption("g", "Weight for the g role (0-1)", required: false)]
            public double G { get; set; } = 0f;
            [CommandOption("c", "Weight for the c role (0-1)", required: false)]
            public double C { get; set; } = 0f;
            [CommandOption("f", "Weight for the f role (0-1)", required: false)]
            public double F { get; set; } = 0f;

            public SetRoleWeights(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<AoDDatabase>();

                double[] weights = new double[] { Base, Chin, Hammer, U, G, C, F };
                weights = weights.Select(x => Math.Clamp(x, 0f, 1f)).ToArray();
                if (weights.Sum() == 0f)
                {
                    await context.RespondAsync($"Must set at least one weight value.", ephemeral: true);
                    return;
                }

                try
                {
                    db.UpdateUserWeights(TargetUser.Id, weights);
                    await context.RespondAsync($"Updated weights for {TargetUser.Mention}: {String.Join(',', weights)}", ephemeral: true);
                }
                catch (AoDDatabase.DBOperationFailedException ex)
                {
                    await context.RespondAsync($"Operation failed: {ex.Message}.", ephemeral: true);
                    throw;
                }
                catch (Exception ex)
                {
                    await context.RespondAsync($"Operation failed: {ex.Message}.", ephemeral: true);
                }
            }
        }

        [SubCommand("set-role-weight", "Sets the weight for a single role.", typeof(ConfigureAoD))]
        private class SetRoleWeight : CommandBase
        {
            [CommandOption("user", "The user to modify")]
            public SocketGuildUser TargetUser { get; set; }

            [CommandOption("role", "The role to set the weight for.", dataProvider: typeof(AoDRoleProvider))]
            public long Role { get; set; }

            [CommandOption("weight", "The value to set this role weight to (0-1)")]
            public double Weight { get; set; }

            public SetRoleWeight(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<AoDDatabase>();

                if (Role < 0 || Role >= AoDDatabase.NumRoles)
                {
                    await context.RespondAsync($"Invalid role : {Role}", ephemeral: true);
                    return;
                }

                AoDDatabase.Roles role = (AoDDatabase.Roles)Role;
                Weight = Math.Clamp(Weight, 0f, 1f);

                try
                {
                    db.UpdateUserWeight(TargetUser.Id, role, Weight);
                    await context.RespondAsync($"Updated weights for {TargetUser.Mention}", ephemeral: true);
                }
                catch (AoDDatabase.DBOperationFailedException ex)
                {
                    await context.RespondAsync($"Operation failed: {ex.Message}.", ephemeral: true);
                    throw;
                }
                catch (Exception ex)
                {
                    await context.RespondAsync($"Operation failed: {ex.Message}.", ephemeral: true);
                }
            }
        }

        [SubCommand("view-user", "View the db entry for a user", typeof(ConfigureAoD))]
        private class ViewUser : CommandBase
        {
            [CommandOption("user", "The user to view")]
            public SocketGuildUser TargetUser { get; set; }

            public ViewUser(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<AoDDatabase>();

                if (!db.DoesUserExist(TargetUser.Id))
                {
                    await context.RespondAsync($"{TargetUser.Mention} could not be found in the database.", ephemeral: true);
                    return;
                }

                //TODO: put this somewhere
                int numDaysToCheck = 5;
                ImmutableArray<AoDDatabase.UserQueryEntry> result = db.GetUsers(new ulong[] { TargetUser.Id }, numDaysToCheck);
                if (result.Length == 0)
                {
                    await context.RespondAsync($"ERROR: {TargetUser.Mention} is in the database but the query returned nothing. Tell Catman.", ephemeral: true);
                    return;
                }

                var entry = result.First();
                await context.RespondAsync($"{entry.Name} - Sessions in last {numDaysToCheck} days: {entry.TimesAttended}" +
                    $"\nWeights: " +
                    $"**Base**: {entry.GetRoleWeight(AoDDatabase.Roles.Base)}, " +
                    $"**Chin**: {entry.GetRoleWeight(AoDDatabase.Roles.Chinner)}, " +
                    $"**Hammer**: {entry.GetRoleWeight(AoDDatabase.Roles.Hammer)}, " +
                    $"**U**: {entry.GetRoleWeight(AoDDatabase.Roles.Umbra)}, " +
                    $"**G**: {entry.GetRoleWeight(AoDDatabase.Roles.Glacies)}, " +
                    $"**C**: {entry.GetRoleWeight(AoDDatabase.Roles.Cruor)}, " +
                    $"**F**: {entry.GetRoleWeight(AoDDatabase.Roles.Fumus)}",
                    ephemeral: true);
            }
        }

        [SubCommand("refresh-db", "Reload the database into memory. Use after manually editing the DB.", typeof(ConfigureAoD))]
        private class RefreshDB : CommandBase
        {
            public RefreshDB(ILogger logger) : base(logger) { }

            public async override Task HandleCommand(CommandContext context)
            {
                var db = context.ServiceProvider.GetRequiredService<AoDDatabase>();
                db.RefreshDB();
                await context.RespondAsync($"Local AoD Database refreshed.");
            }
        }
    }
}
