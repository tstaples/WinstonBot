using Discord.Addons.Hosting;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        // TODO: make this internal
        public class User
        {
            public string Name { get; set; }
            public ulong Id { get; set; } = 0;
            // Range: 0-1
            public double[] RoleWeights { get; set; } = new double[NumRoles];

            public int DBRowIndex { get; set; }

            public User()
            {
                RoleWeights = GetDefaultRoleWeights();
            }
        }

        public static double[] GetDefaultRoleWeights()
        {
            var weights = new double[NumRoles];
            Array.Fill(weights, 0.0f);
            // Default to learner
            weights[(int)Roles.Fumus] = 0.5f;
            return weights;
        }

        public class UserQueryEntry
        {
            public int[] RoleCounts { get; set; }
            public int TimesAttended { get; set; } = 0;

            public ulong Id => _user.Id;
            public string Name => _user.Name;
            public double[] RoleWeights => _user.RoleWeights;
            public ExperienceType Experience { get; private set; }

            private User _user;

            public UserQueryEntry(User user)
            {
                _user = user;
                RoleCounts = new int[NumRoles];
                Array.Fill(RoleCounts, 0);

                Experience = CalculateExperience(_user.RoleWeights);
            }

            public double GetRoleWeight(Roles role) => RoleWeights[(int)role];
        }

        private struct HistoryRow
        {
            public DateTime Date;
            public Guid Id;
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

        public class UserNotFoundException : Exception
        {
            public UserNotFoundException(ulong userId) : base($"User {userId} not found in the database.") { }
        }

        public class UserAlreadyExistsException : Exception
        {
            public UserAlreadyExistsException(ulong userId) : base($"User {userId} already exists in the database.") { }
        }

        public class DBOperationFailedException : Exception
        {
            public DBOperationFailedException(string message) : base(message) { }
        }

        private ILogger<AoDDatabase> _logger;
        private string _credentialsPath;
        private SheetsService _sheetsService;
        private Dictionary<ulong, User> _userEntries = new();
        private string spreadsheetId;
        private const string UserSheetName = "Users";
        private const string HistorySheetName = "History";
        private const string UserDBRange = $"{UserSheetName}!A2:I";
        private const string HistoryRange = $"{HistorySheetName}!A2:I";
        private const string HistoryInsertRange = $"{HistorySheetName}!A2:I2";
        private const int UsersSheetId = 0;
        private const int HistorySheetId = 1447996968;


        public AoDDatabase(ILogger<AoDDatabase> logger, IConfiguration configuration)
        {
            _logger = logger;
            spreadsheetId = configuration["aod_db_spreadsheet_id"];
            _credentialsPath = configuration["google_credentials_path"];

            if (spreadsheetId == null) throw new ArgumentNullException("Failed to get aod_db_spreadsheet_id from the config");
            if (_credentialsPath == null) throw new ArgumentNullException("Failed to get google_credentials_path from the config");

            string[] scopes = { SheetsService.Scope.Spreadsheets };
            string appName = "WinstonBot";

            GoogleCredential credential = GoogleCredential.FromFile(_credentialsPath).CreateScoped(scopes);

            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = appName
            });
        }

        public bool DoesUserExist(ulong id)
        {
            return _userEntries.ContainsKey(id);
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

        public void AddUser(ulong userId, string username, double[]? weights)
        {
            weights = weights ?? GetDefaultRoleWeights();
            if (_userEntries.ContainsKey(userId))
            {
                throw new UserAlreadyExistsException(userId);
            }

            var user = new User()
            {
                Id = userId,
                Name = username,
                RoleWeights = weights,
            };

            _userEntries.Add(userId, user);

            ValueRange requestBody = new();
            requestBody.Values = new List<IList<object>>() { MakeUserRow(user) };

            SpreadsheetsResource.ValuesResource.AppendRequest request = 
                _sheetsService.Spreadsheets.Values.Append(requestBody, spreadsheetId, UserDBRange);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
            request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            try
            {
                request.Execute();
            }
            catch (Exception ex)
            {
                throw new DBOperationFailedException(ex.Message);
            }
        }

        public void UpdateUserWeights(ulong userId, double[] weights)
        {
            weights = weights ?? GetDefaultRoleWeights();
            if (!_userEntries.ContainsKey(userId))
            {
                throw new UserNotFoundException(userId);
            }

            User entry = _userEntries[userId];
            entry.RoleWeights = weights;

            ValueRange requestBody = new()
            {
                Values = new List<IList<object>>() { MakeUserRow(entry) }
            };

            string range = $"{UserSheetName}!A{entry.DBRowIndex}:I{entry.DBRowIndex}";
            SpreadsheetsResource.ValuesResource.UpdateRequest request =
                _sheetsService.Spreadsheets.Values.Update(requestBody, spreadsheetId, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            try
            {
                request.Execute();
            }
            catch (Exception ex)
            {
                throw new DBOperationFailedException(ex.Message);
            }
        }

        public void UpdateUserWeight(ulong userId, Roles role, double weight)
        {
            User user;
            if (_userEntries.TryGetValue(userId, out user))
            {
                user.RoleWeights[(int)role] = weight;
                UpdateUserWeights(userId, user.RoleWeights);
                return;
            }
            throw new UserNotFoundException(userId);
        }

        public Guid AddTeamToHistory(Dictionary<string, ulong> team)
        {
            var guid = Guid.NewGuid();

            List<object> row = MakeHistoryRow(guid, team);

            InsertDimensionRequest insertRow = new InsertDimensionRequest();
            insertRow.Range = new DimensionRange()
            {
                SheetId = HistorySheetId,
                Dimension = "ROWS",
                StartIndex = 1,
                EndIndex = 2
            };

            PasteDataRequest data = new PasteDataRequest
            {
                Data = string.Join(",", row),
                Delimiter = ",",
                Coordinate = new GridCoordinate
                {
                    ColumnIndex = 0,
                    RowIndex = 1,
                    SheetId = HistorySheetId
                },
            };

            BatchUpdateSpreadsheetRequest r = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new List<Request>
                {
                    new Request{ InsertDimension = insertRow },
                    new Request{ PasteData = data }
                }
            };

            try
            {
                BatchUpdateSpreadsheetResponse response = _sheetsService.Spreadsheets.BatchUpdate(r, spreadsheetId).Execute();
                _logger.LogDebug($"Added team to history with tag: {response.ETag}");
                return guid;
            }
            catch (Exception ex)
            {
                throw new DBOperationFailedException(ex.Message);
            }
        }

        public void RemoveRowFromHistory(Guid id)
        {
            if (id == Guid.Empty)
            {
                _logger.LogError($"Invalid guid");
                return;
            }

            var rows = GetHistoryRows(0);
            int rowIndex = rows.FindIndex(row => row.Id == id);
            if (rowIndex == -1)
            {
                _logger.LogWarning($"Couldn't remove history row for id {id} - couldn't find matching row.");
                return;
            }

            DeleteDimensionRequest deleteRow = new DeleteDimensionRequest();
            deleteRow.Range = new DimensionRange()
            {
                SheetId = HistorySheetId,
                Dimension = "ROWS",
                StartIndex = rowIndex + 1,
                EndIndex = rowIndex + 2
            };

            BatchUpdateSpreadsheetRequest request = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new List<Request>
                {
                    new Request{ DeleteDimension = deleteRow },
                }
            };

            try
            {
                _sheetsService.Spreadsheets.BatchUpdate(request, spreadsheetId).Execute();
                _logger.LogDebug($"Deleted row {rowIndex + 2} from history");
            }
            catch (Exception ex)
            {
                throw new DBOperationFailedException(ex.Message);
            }
        }

        public void UpdateHistory(Guid id, Dictionary<string, ulong> team)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("Failed to update team history: invalid id.");
            }

            var rows = GetHistoryRows(0);
            int rowIndex = rows.FindIndex(row => row.Id == id);
            if (rowIndex == -1)
            {
                _logger.LogError($"Couldn't find history row for id {id}");
                return;
            }

            ValueRange requestBody = new()
            {
                Values = new List<IList<object>>() { MakeHistoryRow(id, team) }
            };

            int convertedRowIndex = rowIndex + 2;
            string range = $"{HistorySheetName}!A{convertedRowIndex}:I{convertedRowIndex}";
            SpreadsheetsResource.ValuesResource.UpdateRequest request =
                _sheetsService.Spreadsheets.Values.Update(requestBody, spreadsheetId, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            try
            {
                request.Execute();
            }
            catch (Exception ex)
            {
                throw new DBOperationFailedException(ex.Message);
            }
        }

        public void RefreshDB()
        {
            PopulateDatabase();
        }

        /// ///////////////////////////////////////////////////////////////////////////

        // TODO: make this async
        private void PopulateDatabase()
        {
            lock (_userEntries)
            {
                _userEntries.Clear();

                SpreadsheetsResource.ValuesResource.GetRequest request =
                    _sheetsService.Spreadsheets.Values.Get(spreadsheetId, UserDBRange);

                IList<IList<Object>>? rows = null;
                try
                {
                    ValueRange response = request.Execute();
                    rows = response.Values;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to populate database: {ex}");
                    return;
                }

                if (rows == null || rows.Count <= 0)
                {
                    _logger.LogError("[AoDDatabase] no user rows found.");
                    return;
                }

                for (int rowIndex = 0; rowIndex < rows.Count; ++rowIndex)
                {
                    var columns = rows[rowIndex];

                    ulong id = 0;
                    if (!ulong.TryParse((string)columns[1], out id))
                    {
                        _logger.LogError($"Failed to parse user id in row {rowIndex}");
                        continue;
                    }

                    User user = new User()
                    {
                        Name = (string)columns[0],
                        Id = id,
                        DBRowIndex = rowIndex + 2 // offset for title row + fact that rows are 1 based
                    };

                    // start at 2 to skip the name/id columns.
                    for (int columnIndex = 2; columnIndex < columns.Count; ++columnIndex)
                    {
                        ref double weight = ref user.RoleWeights[columnIndex - 2];
                        if (columns[columnIndex] == null || !double.TryParse((string)columns[columnIndex], out weight))
                        {
                            weight = 0.0f;
                        }
                    }

                    _userEntries.Add(id, user);
                }
            }
        }

        private Dictionary<ulong, UserHistoryEntry> GetHistoryForUsers(IEnumerable<ulong> users, int numDays)
        {
            Dictionary<ulong, UserHistoryEntry> entries = new();

            // Multiply by 2 as there shouldn't be more than 2 teams per day
            var date = DateTimeOffset.UtcNow.AddDays(-numDays);
            var rows = GetHistoryRows(numDays * 2);
            foreach (HistoryRow row in rows)
            {
                // Ignore invalid or rows that are too old
                if (!IsHistoryRowValid(row) || row.Date < date)
                {
                    continue;
                }

                for (int i = 0; i < row.UsersForRoles.Length; ++i)
                {
                    var id = row.UsersForRoles[i];
                    var entry = Utility.GetOrAdd(entries, id);
                    entry.RoleCounts[i]++;
                }
            }
            return entries;
        }

        // if numRows is 0 this will return all the rows
        private List<HistoryRow> GetHistoryRows(int numRows)
        {
            string rowRange = numRows > 0 ? $"{numRows + 1}" : "";
            String range = $"{HistoryRange}{rowRange}";
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
                if (row.Count == 0)
                {
                    // Skip empty rows
                    rows.Add(entry);
                    continue;
                }

                DateTime.TryParse(row[0] as string, out entry.Date);
                Guid.TryParse(row[1] as string, out entry.Id);
                entry.UsersForRoles = new ulong[NumRoles];

                int roleStartOffset = 2;
                for (int i = 0; i < NumRoles; i++)
                {
                    int columnIndex = i + roleStartOffset;
                    if (columnIndex >= row.Count ||
                        !ulong.TryParse((string)row[columnIndex], out entry.UsersForRoles[i]))
                    {
                        entry.UsersForRoles[i] = 0;
                    }
                }

                rows.Add(entry);
            }
            return rows;
        }

        private List<object> MakeUserRow(User user)
        {
            List<object> row = new();
            row.Add(user.Name);
            row.Add(user.Id.ToString());
            user.RoleWeights.ToList().ForEach(w => row.Add(w));
            return row;
        }

        // TODO: change this to just take a list of ulongs so it's not boss specific
        private List<object> MakeHistoryRow(Guid guid, Dictionary<string, ulong> team)
        {
            List<object> row = new();
            row.Add(DateTime.UtcNow.ToString("MM/dd/yyyy"));
            row.Add(guid.ToString());
            foreach (string roleName in Enum.GetNames(typeof(Roles)))
            {
                ulong id = 0;
                if (!team.TryGetValue(roleName, out id))
                {
                    // Support lowercase role names for pvm-signup log-team
                    team.TryGetValue(roleName.ToLower(), out id);
                }
                row.Add(id.ToString());
            }
            return row;
        }

        private static ExperienceType CalculateExperience(double[] weights)
        {
            double fumusWeight = weights[(int)Roles.Fumus];
            double cruorWeight = weights[(int)Roles.Cruor];
            double learnerWeight = Math.Max(fumusWeight, cruorWeight);
            foreach (Roles expRole in ExperiencedRoles)
            {
                double roleWeight = weights[(int)expRole];
                if (roleWeight > learnerWeight)
                {
                    return ExperienceType.Experienced;
                }
            }
            return ExperienceType.Learner;
        }

        private static bool IsHistoryRowValid(HistoryRow row)
        {
            return row.UsersForRoles != null;
        }
    }
}
