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

        public Dictionary<string, ulong>[] SelectTeams(IEnumerable<ulong> inputNames, int numTeams)
        {
            // TODO: pass in a generic db interface
            AoDDatabase db = ServiceProvider.GetRequiredService<AoDDatabase>();

            var bossEntry = BossData.Entries[(int)BossData.Boss.AoD];
            int numDaysToConsider = 5; // TODO: where should we store this?
            ImmutableArray<AoDDatabase.UserQueryEntry> users = db.GetUsers(inputNames, numDaysToConsider);

            AoDDatabase.Roles GetRoleForCol(int columnIndex)
            {
                return (AoDDatabase.Roles)(columnIndex % AoDDatabase.NumRoles);
            }

            // Create a matrix where we calculate the score for every person in every role.
            // 
            int scaledRoleCount = AoDDatabase.NumRoles * numTeams;

            // each row is a name, each col is a role
            // For the algorithm to work we need the matrix to be square. The extra columns we just set to 0 and hope they don't affect anything.
            int numCols = Math.Max(scaledRoleCount, users.Length);
            int numRows = Math.Max(scaledRoleCount, users.Length);
            int[,] costs = new int[numRows, numCols];
            for (int row = 0; row < numRows; ++row)
            {
                var user = row < users.Length ? users[row] : null;
                // TODO: we could easily score attendance per role here too with an array.
                // Though making sure people are logged as the role they did might prove tricky.
                for (int col = 0; col < numCols; ++col)
                {
                    // We can have extra columns as we need the matrix to be square for the algorithm to work. So just 0 them out.
                    // We can also have extra rows if we have less people than the number of roles.
                    if (col >= scaledRoleCount || user == null)
                    {
                        costs[row, col] = 0;
                        continue;
                    }

                    var role = GetRoleForCol(col);
                    double score = CalculateUserScoreForRole(user, role, numDaysToConsider);
                    costs[row, col] = (int)score;
                }
            }

            int[] result = new HungarianAlgorithm(costs).Run();
            if (result == null)
            {
                throw new Exception("Failed to calculate users");
            }

            // store in order of the role so we add them to the dict in the correct order, allowing them to appear in the embed in the right order.
            ulong[] userForRole = new ulong[scaledRoleCount];
            for (int i = 0; i < users.Length; ++i)
            {
                if (result[i] < scaledRoleCount)
                {
                    var user = users[i];
                    int roleIndex = result[i];
                    userForRole[roleIndex] = user.Id;
                }
            }

            Dictionary<string, ulong>[] userForRoleMap = new Dictionary<string, ulong>[numTeams];
            for (int i = 0; i < numTeams; ++i)
            {
                userForRoleMap[i] = new Dictionary<string, ulong>();
            }

            for (int i = 0; i < userForRole.Length; ++i)
            {
                var role = GetRoleForCol(i);
                var dict = userForRoleMap[i / AoDDatabase.NumRoles];
                dict.Add(role.ToString(), userForRole[i]);
            }

            return userForRoleMap;
        }

        private double CalculateUserScoreForRole(AoDDatabase.UserQueryEntry user, AoDDatabase.Roles role, int numDaysToConsider)
        {
            double attendanceScore = (double)user.TimesAttended / (double)numDaysToConsider;
            double roleImportanceScore = ((double)role / (double)AoDDatabase.NumRoles) - 1.0;
            double roleScore = (1.0f - user.GetRoleWeight(role));

            // Prefer learners for F
            double learnerScore = role == AoDDatabase.Roles.Fumus && user.Experience == AoDDatabase.ExperienceType.Learner ? -1 : 0f;

            double score =
                  (roleScore * 1.5)
                + (attendanceScore * 3.0) // attendance is the biggest factor
                + learnerScore
                + roleImportanceScore;

            return score * 10.0;
        }
    }
}
