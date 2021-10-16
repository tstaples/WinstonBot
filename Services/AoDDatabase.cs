using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System.Collections.Immutable;

namespace WinstonBot.Services
{
    public class AoDDatabase
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
            public string Name { get; set; }
            public ulong Id { get; set; } = 0;
            // Range: 0-1
            public float[] RoleWeights { get; set; } = new float[NumRoles];
            public ExperienceType Experience { get; set; }

            public User()
            {
                Array.Fill(RoleWeights, 0.0f);
                // Default to learner
                RoleWeights[(int)Roles.Fumus] = 0.5f;
                Experience = ExperienceType.Learner;
            }
        }

        public class UserQueryEntry
        {
            public int[] RoleCounts { get; set; }
            public int TimesAttended { get; set; } = 0;

            public ulong Id => _user.Id;
            public string Name => _user.Name;
            public float[] RoleWeights => _user.RoleWeights;
            public ExperienceType Experience => _user.Experience;

            private User _user;

            public UserQueryEntry(User user)
            {
                _user = user;
                RoleCounts = new int[NumRoles];
                Array.Fill(RoleCounts, 0);
            }

            public float GetRoleWeight(Roles role) => RoleWeights[(int)role];
        }

        private struct HistoryRow
        {
            public DateTime Date;
            public ulong[] UsersForRoles;
        }

        private class UserHistoryEntry
        {
            public int[] RoleCounts = new int[NumRoles];
            public int TimesAttended => RoleCounts.Sum();

            public UserHistoryEntry()
            {
                Array.Fill(RoleCounts, 0);
            }
        }

        private string _credentialsPath;
        private SheetsService _sheetsService;
        private Dictionary<ulong, User> _userEntries = new();
        private const string spreadsheetId = "1IFhofNHm8R_cPjfEMl0_BQ5ynKaGrajV4uMHM1lmh7A";


        public AoDDatabase(string credentialPath)
        {
            _credentialsPath = credentialPath;
        }

        public void Initialize()
        {
            string[] scopes = { SheetsService.Scope.Spreadsheets };
            string appName = "WinstonBot";

            UserCredential credential;

            using (var stream = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read))
            {
                string credPath = "google_token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Google credentials saved to " + credPath);
            }

            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = appName
            });


            String range = "Users!A2:I";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            ValueRange response = request.Execute();
            IList<IList<Object>> values = response.Values;
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    ulong id = ulong.Parse((string)row[1]);
                    User user = new User()
                    {
                        Name = (string)row[0],
                        Id = id,
                    };

                    for (int i = 2; i < row.Count; i++)
                    {
                        user.RoleWeights[i - 2] = float.Parse((string)row[i]);
                    }

                    user.Experience = CalculateExperience(user.RoleWeights);
                    _userEntries.Add(id, user);
                }
            }
            else
            {
                Console.WriteLine("No data found.");
            }
        }

        public ImmutableArray<UserQueryEntry> GetUsers(IEnumerable<ulong> users, int numDaysToConsider)
        {
            Dictionary<ulong, UserHistoryEntry> history = GetHistoryForUsers(users, numDaysToConsider);

            List<UserQueryEntry> result = new();
            foreach (ulong userId in users)
            {
                UserQueryEntry entry;
                if (_userEntries.ContainsKey(userId))
                {
                    entry = new UserQueryEntry(_userEntries[userId]);
                    if (history.ContainsKey(userId))
                    {
                        UserHistoryEntry historyEntry = history[userId];
                        entry.RoleCounts = historyEntry.RoleCounts;
                        entry.TimesAttended = historyEntry.TimesAttended;
                    }
                }
                else
                {
                    // Default user
                    var user = new User()
                    {
                        Id = userId,
                        Name = "Unknown"
                    };

                    entry = new UserQueryEntry(user);
                }

                result.Add(entry);
            }

            return result.ToImmutableArray();
        }

        private Dictionary<ulong, UserHistoryEntry> GetHistoryForUsers(IEnumerable<ulong> users, int numDays)
        {
            Dictionary<ulong, UserHistoryEntry> entries = new();

            var rows = GetHistoryRows(numDays);
            foreach (HistoryRow row in rows)
            {
                for (int i = 0; i < row.UsersForRoles.Length; ++i)
                {
                    var id = row.UsersForRoles[i];
                    var entry = Utility.GetOrAdd(entries, id);
                    entry.RoleCounts[i]++;
                }
            }
            return entries;
        }

        private List<HistoryRow> GetHistoryRows(int numRows)
        {
            String range = $"History!A2:H{numRows + 1}";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            List<HistoryRow> rows = new();

            ValueRange response = request.Execute();
            IList<IList<Object>> values = response.Values;
            if (values == null || values.Count == 0)
            {
                return rows;
            }

            foreach (var row in values)
            {
                var entry = new HistoryRow();
                entry.Date = DateTime.Parse((string)row[0]);
                entry.UsersForRoles = new ulong[NumRoles];
                for (int i = 0; i < NumRoles; i++)
                {
                    int columnIndex = i + 1;
                    if (!ulong.TryParse((string)row[columnIndex], out entry.UsersForRoles[i]))
                    {
                        entry.UsersForRoles[i] = 0;
                    }
                }

                rows.Add(entry);
            }
            return rows;
        }

        private static ExperienceType CalculateExperience(float[] weights)
        {
            float fumusWeight = weights[(int)Roles.Fumus];
            float cruorWeight = weights[(int)Roles.Cruor];
            float learnerWeight = Math.Max(fumusWeight, cruorWeight);
            foreach (Roles expRole in ExperiencedRoles)
            {
                float roleWeight = weights[(int)expRole];
                if (roleWeight > learnerWeight)
                {
                    return ExperienceType.Experienced;
                }
            }
            return ExperienceType.Learner;
        }
    }
}
