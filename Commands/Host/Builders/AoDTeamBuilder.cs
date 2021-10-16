using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using WinstonBot.Data;
using WinstonBot.Helpers;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    internal class AoDTeamBuilder : ITeamBuilder
    {
        public IServiceProvider ServiceProvider { get; set; }

        public Dictionary<string, ulong> SelectTeam(List<ulong> inputNames)
        {
            AoDDatabase db = ServiceProvider.GetRequiredService<AoDDatabase>();

            var bossEntry = BossData.Entries[(int)BossData.Boss.AoD];
            int numDaysToConsider = 5; // TODO: where should we store this?
            ImmutableArray<AoDDatabase.UserQueryEntry> users = db.GetUsers(inputNames, numDaysToConsider);

            // each row is a name, each col is a role
            // For the algorithm to work we need the matrix to be square. The extra columns we just set to 0 and hope they don't affect anything.
            int numCols = Math.Max(AoDDatabase.NumRoles, users.Length);
            int[,] costs = new int[users.Length, numCols];
            for (int row = 0; row < users.Length; ++row)
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
                    float score = CalculateUserScoreForRole(user, role, numDaysToConsider);
                    costs[row, col] = (int)score;
                }
            }

            int[] result = new HungarianAlgorithm(costs).Run();
            if (result == null)
            {
                throw new Exception("Failed to calculate users");
            }

            // store in order of the role so we add them to the dict in the correct order, allowing them to appear in the embed in the right order.
            ulong[] userForRole = new ulong[AoDDatabase.NumRoles];
            for (int i = 0; i < users.Length; ++i)
            {
                if (result[i] < AoDDatabase.NumRoles)
                {
                    var user = users[i];
                    var role = (AoDDatabase.Roles)result[i];
                    int roleIndex = result[i];
                    userForRole[roleIndex] = user.Id;
                    Console.WriteLine($"{user.Name} is doing role: {role}");
                }
            }

            Dictionary<string, ulong> userForRoleMap = new();
            for (int i = 0; i < userForRole.Length; ++i)
            {
                userForRoleMap.Add(((AoDDatabase.Roles)i).ToString(), userForRole[i]);
            }

            return userForRoleMap;
        }

        private float CalculateUserScoreForRole(AoDDatabase.UserQueryEntry user, AoDDatabase.Roles role, int numDaysToConsider)
        {
            float attendanceScore = (float)user.TimesAttended / (float)numDaysToConsider;
            // Ignore attendance score for the base role
            float biasedAttendanceScore = role == AoDDatabase.Roles.Base ? 0.0f : attendanceScore;

            // Prefer learners for F
            float learnerScore = role == AoDDatabase.Roles.Fumus && user.Experience == AoDDatabase.ExperienceType.Learner ? -1 : 0f;
            float roleScore = 1.0f - user.GetRoleWeight(role);
            float score = (roleScore + biasedAttendanceScore + learnerScore) * 10.0f;
            return score;
        }
    }
}
