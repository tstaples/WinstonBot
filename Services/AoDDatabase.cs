using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;

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
            public string Name { get; set; } // Temp
            public ulong Id { get; set; } = 0;
            // Range: 0-1
            public float[] RoleWeights { get; set; } = new float[NumRoles];
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

        private string _credentialsPath;
        private SheetsService _sheetsService;

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

            string spreadsheetId = "1IFhofNHm8R_cPjfEMl0_BQ5ynKaGrajV4uMHM1lmh7A";

            String range = "Users!A2:I";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            ValueRange response = request.Execute();
            IList<IList<Object>> values = response.Values;
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    User user = new User()
                    {
                        Name = (string)row[0],
                        Id = ulong.Parse((string)row[1]),
                    };

                    for (int i = 2; i < row.Count; i++)
                    {
                        user.RoleWeights[i - 2] = float.Parse((string)row[i]);
                    }

                    UserEntries.Add(user);
                }
            }
            else
            {
                Console.WriteLine("No data found.");
            }

            foreach (User user in UserEntries)
            {
                Console.WriteLine($"{user.Name}, {user.Id}, weights: {String.Join(',', user.RoleWeights)}");
            }
        }
    }
}
