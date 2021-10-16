using Discord;
using Discord.WebSocket;
using WinstonBot.Data;
using WinstonBot.Helpers;
using WinstonBot.Services;

namespace WinstonBot.Commands
{
    internal class AoDTeamBuilder : ITeamBuilder
    {
        private static List<AoDDatabase.User> TestUsers = new List<AoDDatabase.User>()
        {
            new AoDDatabase.User()
            {
                Name = "Tails",
                Id = 517886402466152450,
                RoleWeights = new float[] {  1.0f, 0, 0, 0.5f, 0.5f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 5
            },
            new AoDDatabase.User()
            {
                Name = "Catman",
                Id = 141439679890325504,
                RoleWeights = new float[] {  0f, 1f, 0f, 0.8f, 0.5f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 5
            },
            new AoDDatabase.User()
            {
                Name = "Vinnie",
                Id = 295027430299795456,
                RoleWeights = new float[] {  0.7f, 0.6f, 0f, 0.5f, 0.6f, 0.6f, 0.6f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Kadeem",
                Id = 668161362249121796,
                RoleWeights = new float[] {  0f, 0.7f, 0.8f, 0.7f, 0.6f, 0.5f, 0.5f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Dilli",
                Id = 197872300802965504,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0.5f, 0.8f, 0f, 0.5f, 0.7f, 0.6f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "poopoo",
                Id = 159691258804174849,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0.6f, 1f, 0.8f, 0.2f, 0.1f},
                SessionsAttendedInLastNDays = 2
            },
            new AoDDatabase.User()
            {
                Name = "Shalimar",
                Id = 172497655992156160,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0f,   0f, 0.2f, 0.4f, 0.5f},
                SessionsAttendedInLastNDays = 4
            },
            new AoDDatabase.User()
            {
                Name = "AmericanFry",
                Id = 414119139506913280,
                //                         base, chin, ham,  u,  g,    c,    f
                RoleWeights = new float[] {  0f, 0f,   0f,   0f, 0.2f, 0.4f, 0.6f},
                SessionsAttendedInLastNDays = 5
            },
        };


        public Dictionary<string, ulong> SelectTeam(List<ulong> inputNames)
        {
            //AoDDatabase db = new(); // todo: get from service
            //HashSet<ulong> selectedNames = new();

            var bossEntry = BossData.Entries[(int)BossData.Boss.AoD];
            int numDaysToConsider = 5; // TODO: where should we store this?
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
            for (int i = 0; i < users.Count; ++i)
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

        private float CalculateUserScoreForRole(AoDDatabase.User user, AoDDatabase.Roles role, int numDaysToConsider)
        {
            float attendanceScore = (float)user.SessionsAttendedInLastNDays / (float)numDaysToConsider;
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
