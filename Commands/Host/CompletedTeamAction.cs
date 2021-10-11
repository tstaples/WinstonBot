using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using WinstonBot.Attributes;
using WinstonBot.Data;
using WinstonBot.Services;
using WinstonBot.Helpers;

namespace WinstonBot.Commands
{
    [Action("pvm-complete-team")]
    internal class CompleteTeamAction : IAction
    {
        public static string ActionName = "pvm-complete-team";

        [ActionParam]
        public long BossIndex { get; set; }

        private BossData.Entry BossEntry => BossData.Entries[BossIndex];

        public async Task HandleAction(ActionContext actionContext)
        {
            var context = (HostActionContext)actionContext;

            if (!context.Message.Embeds.Any())
            {
                await context.RespondAsync("Message is missing the embed. Please re-create the host message (and don't delete the embed this time)", ephemeral: true);
                return;
            }
            
            var guild = ((SocketGuildChannel)context.Channel).Guild;

            var currentEmbed = context.Message.Embeds.First();
            var ids = HostHelpers.ParseNamesToIdListWithValidation(guild, currentEmbed.Description);
            if (ids.Count == 0)
            {
                await context.RespondAsync("Not enough people signed up.", ephemeral: true);
                return;
            }

            if (!context.TryMarkMessageForEdit(context.Message.Id, ids))
            {
                await context.RespondAsync("This team is already being edited by someone else.", ephemeral: true);
                return;
            }

            var names = Utility.ConvertUserIdListToMentions(guild, ids);

            var temp = SelectUsersForTeam(ids, 5);
            // TODO: calculate who should go.
            List<string> selectedNames = new();
            List<string> unselectedNames = new();
            int i = 0;
            foreach (var mention in names)
            {
                if (i++ < BossEntry.MaxPlayersOnTeam) selectedNames.Add(mention);
                else unselectedNames.Add(mention);
            }

            await context.Message.ModifyAsync(msgProps =>
            {
                msgProps.Components = HostHelpers.BuildSignupButtons(BossIndex, true);
                // footers can't show mentions, so use the username.
                msgProps.Embed = HostHelpers.BuildSignupEmbed(BossIndex, names, context.User.Username);
            });

            // Footed will say "finalized by X" if it's been completed before.
            bool hasBeenConfirmedBefore = currentEmbed.Footer.HasValue;

            var message = await context.User.SendMessageAsync("Confirm or edit the team." +
                "\nClick the buttons to change who is selected to go." +
                "\nOnce you're done click Confirm Team." +
                "\nYou may continue making changes after you confirm the team by hitting confirm again." +
                "\nOnce you're finished making changes you can dismiss this message.",
                embed: HostHelpers.BuildTeamSelectionEmbed(guild.Id, context.Channel.Id, context.Message.Id, hasBeenConfirmedBefore, BossEntry, selectedNames),
                component: HostHelpers.BuildTeamSelectionComponent(guild, BossIndex, selectedNames, unselectedNames));

            // TODO: do this via context instead?
            context.ServiceProvider.GetRequiredService<InteractionService>().AddInteraction(context.OwningCommand, message.Id);

            await context.DeferAsync();
        }

        //--------------------------------

        class AoDDatabase
        {
            public enum Roles
            {
                Base,
                Chinner,
                Hammer,
                Umbra,
                Glacies,
                Cruor,
                Fumus
            }

            public static readonly Roles[] ExperiencedRoles = new[]
            {
                Roles.Base,
                Roles.Chinner,
                Roles.Hammer,
                Roles.Umbra,
                Roles.Glacies // ?
            };

            public enum ExperienceType
            {
                Experienced,
                Learner
            }

            public const int NumRoles = 7;

            // DB row: id, base weight, chin weight, ...
            // /configure aod add-role <user> <role> <weight? or default>
            // /configure aod set-role-weight <user> <role> <weight>
            // /configure aod remove-role <user> <role>
            // how do we set if they're learner/exp? Maybe just based on what role we add them to?
            // - eg adding someone to umbra would make them exp?
            public class User
            {
                public string Name { get; set; } // Temp
                public ulong Id { get; set; } = 0;
                // Range: 0-1
                public float[] RoleWeights {  get; set; } = new float[NumRoles];
                public int SessionsAttendedInLastNDays { get; set; } = 0;

                public float GetRoleWeight(Roles role) => RoleWeights[(int)role];

                public User()
                {
                    Array.Fill(RoleWeights, 0.0f);
                }

                // TODO: could cache this when loaded
                // TODO: might need to just explicitly set people to learner
                public ExperienceType GetExperience()
                {
                    float fumusWeight = GetRoleWeight(Roles.Fumus);
                    float cruorWeight = GetRoleWeight(Roles.Cruor);
                    float learnerWeight = Math.Max(fumusWeight, cruorWeight);
                    foreach (Roles expRole in ExperiencedRoles)
                    {
                        float roleWeight = GetRoleWeight(expRole);
                        if (roleWeight > learnerWeight)
                        {
                            return ExperienceType.Experienced;
                        }
                    }
                    return ExperienceType.Learner;
                }
            }

            public List<User> UserEntries { get; set; } = new();
        }

        // ----

        private static List<AoDDatabase.User> TestUsers = new List<AoDDatabase.User>()
        {
            new AoDDatabase.User()
            {
                Name = "Tails",
                Id = 1,
                RoleWeights = new float[] {  1.0f, 0, 0, 0.5f, 0.5f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 5
            },
            new AoDDatabase.User()
            {
                Name = "Catman",
                Id = 2,
                RoleWeights = new float[] {  0f, 1f, 0f, 0.8f, 0.5f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 5
            },
            new AoDDatabase.User()
            {
                Name = "Vinnie",
                Id = 3,
                RoleWeights = new float[] {  0.7f, 0.6f, 0f, 0.5f, 0.6f, 0.6f, 0.6f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Kadeem",
                Id = 4,
                RoleWeights = new float[] {  0f, 0.7f, 0.8f, 0.7f, 0.6f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Dilli",
                Id = 5,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0.5f, 0.8f, 0f, 0.5f, 0.7f, 0.6f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "poopoo",
                Id = 6,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0.6f, 1f, 0.8f, 0.2f, 0.1f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Shalimar",
                Id = 7,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0f,   0f, 0.2f, 0.4f, 0.5f},
                SessionsAttendedInLastNDays = 4
            },
            new AoDDatabase.User()
            {
                Name = "AmericanFry",
                Id = 9,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0f,   0f, 0.2f, 0.4f, 0.6f},
                SessionsAttendedInLastNDays = 5
            },
        };

        private Dictionary<string, ulong> SelectUsersForTeam(List<ulong> inputNames, int numDaysToConsider)
        {
            AoDDatabase db = new(); // todo: get from service
            HashSet<ulong> selectedNames = new();

            List<AoDDatabase.User> users = TestUsers;//db.GetUsers(inputNames, numDaysToConsider);

            // each row is a name, each col is a role
            // For the algorithm to work we need the matrix to be square. The extra columns we just set to 0 and hope they don't affect anything.
            int numCols = Math.Max(AoDDatabase.NumRoles, users.Count);
            int[,] costs = new int[users.Count, numCols];
            for (int row = 0; row < users.Count; ++row)
            {
                var user = users[row];
                // TODO: we could easily score attendance per role here too with an array.
                // Though making sure people are logged as the role they did might prove tricky.
                for (int col = 0; col < numCols; ++col)
                {
                    if (col >= AoDDatabase.NumRoles)
                    {
                        costs[row, col] = 0;
                        continue;
                    }

                    var role = (AoDDatabase.Roles)col;
                    float score = CalculateUserScoreForRole(user, role);
                    costs[row, col] = (int)score;
                }
            }

            int[] result = new HungarianAlgorithm(costs).Run();
            if (result == null)
            {
                throw new Exception("Failed to calculate users");
            }

            Dictionary<string, ulong> userForRoleMap = new();
            for (int i = 0; i < users.Count; ++i)
            {
                if (result[i] < AoDDatabase.NumRoles)
                {
                    var user = users[i];
                    var role = (AoDDatabase.Roles)result[i];
                    userForRoleMap.Add(role.ToString(), user.Id);
                    Console.WriteLine($"{user.Name} is doing role: {role}");
                }
            }

            return userForRoleMap;
        }

        private float CalculateUserScoreForRole(AoDDatabase.User user, AoDDatabase.Roles role)
        {
            float attendanceScore = (float)user.SessionsAttendedInLastNDays / (float)5;
            // Ignore attendance score for the base role
            float biasedAttendanceScore = role == AoDDatabase.Roles.Base ? 0.0f : attendanceScore;

            // Prefer learners for F
            float learnerScore = role == AoDDatabase.Roles.Fumus && user.GetExperience() == AoDDatabase.ExperienceType.Learner ? -1 : 0f;
            float roleScore = 1.0f - user.GetRoleWeight(role);
            float score = (roleScore + biasedAttendanceScore + learnerScore) * 10.0f;
            return score;
        }
    }
}
