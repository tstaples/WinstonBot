using Discord;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WinstonBot.Attributes;
using System.Net;
using HtmlAgilityPack;
using System.Text;

namespace WinstonBot.Commands
{
    [Command("leaderboard", "Shows the Fruit Wars Leaderboard", DefaultPermission.AdminOnly)]
    internal class FruitWarsCommands : CommandBase
    {
        public FruitWarsCommands(ILogger logger) : base(logger) { }

        private HttpClient _httpClient;
        private FormUrlEncodedContent _params;
        private static int _running = 0;

        private const string lineSep = "---------------------------------------------";

        private static readonly string[] grape = new string[]
        {
            "Shanelle",
            "Situations",
            "NeoNerV",
            "Kadeem",
            "IceRiver225",
            "Gamie",
            "brianward23",
            "Hoffster",
            "Captain Dk53",
            "WARL0RD TH0R",
            "Old_fally",
            "yu-sin-kwan"
        };

        private static readonly string[] cherry = new string[]
        {
            "Batsie",
            "FeralCreator",
            "Ghost Gob",
            "Nexxey",
            "TuggyMcNutty",
            "Ody29",
            "doe gewoon",
            "yare_bear",
            "XxKrazinoxX",
            "Vws dipper",
            "ItsGarfield",
            "Nugget815"
        };

        private static readonly string[] apple = new string[]
        {
            "Catman",
            "Rubiess",
            "hidenpequin",
            "Blaviken",
            "K1ngchile69",
            "MeleeNewb",
            "Feerip",
            "PepperSaltYo",
            "Finnsisjon",
            "Sinteresting",
            "Abyssal Arse",
            "ohitskirsten"
        };

        private static readonly string[] peach = new string[]
        {
            "9tails",
            "Walkers",
            "EatMyBabyz",
            "woefulsteve",
            "Fineapples",
            "Beauty4Ashes",
            "Ayhet",
            "PecorineChan",
            "Matar",
            "Sir Tobias",
            "GW143",
            "Demi3k"
        };

        private static readonly Dictionary<string, string[]> Teams = new()
        {
            { "Grape", grape },
            { "Apple", apple },
            { "Cherry", cherry },
            { "Peach", peach }
        };

        public async override Task HandleCommand(CommandContext context)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
            {
                await context.RespondAsync("Command already running.");
                return;
            }

            await context.RespondAsync("Calculating Results... Call Blaviken a boomer in the meantime.");

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://www.runeclan.com/");
            _params = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "dxp_col", "dxp"}
            });

            Task.Run(async () =>
            {
                List<KeyValuePair<int, string>> messageResults = new();

                // Run the operation for each team in parallel
                var tasks = Teams.Select(async kvp =>
                {
                    // Query runeclan for each user in parallel
                    List<KeyValuePair<string, int>> rsnXpMap = new();
                    var teamTasks = kvp.Value.Select(async (string name) =>
                    {
                        string xp = await GetUserXp(name);
                        Console.WriteLine($"{name} - {xp}");

                        int xpVal = int.Parse(xp, System.Globalization.NumberStyles.AllowThousands);
                        rsnXpMap.Add(new KeyValuePair<string, int>(name, xpVal));
                    });

                    // Wait until everyone has been queried
                    await Task.WhenAll(teamTasks);

                    // Sort descending
                    rsnXpMap.Sort((a, b) => a.Value.CompareTo(b.Value));
                    rsnXpMap.Reverse();

                    // Sum total xp
                    int totalXp = 0;
                    rsnXpMap.ForEach((kvp) => totalXp += kvp.Value);

                    // Format the shit
                    string message = GetFormattedTeamResult(kvp.Key, totalXp, rsnXpMap);
                    messageResults.Add(new KeyValuePair<int, string>(totalXp, message));
                });

                // Wait for all work for all teams to be done
                await Task.WhenAll(tasks);

                messageResults.Sort((a, b) => a.Key.CompareTo(b.Key));
                messageResults.Reverse();

                foreach (var kvp in messageResults)
                {
                    await context.SlashCommand.Channel.SendMessageAsync(kvp.Value);
                }

                Interlocked.Decrement(ref _running);

            }).Forget();
        }

        
        private string GetFormattedTeamResult(string teamName, int totalXp, List<KeyValuePair<string, int>> users)
        {
            var builder = new StringBuilder();
            builder
                .AppendLine("```")
                .AppendLine(lineSep)
                .AppendLine($"Team {teamName} - {String.Format("{0:n0}", totalXp)}")
                .AppendLine(lineSep);
            foreach (var kvp in users)
            {
                string formattedName = String.Format("{0,-20}", kvp.Key);
                string formattedXP = String.Format("{0:n0}", kvp.Value);
                builder.AppendLine($"{formattedName}{formattedXP}");
            }

            builder
                .AppendLine(lineSep)
                .AppendLine("```");

            return builder.ToString();
        }

        private async Task<string> GetUserXp(string name)
        {
            var sanitizedName = name.Replace(' ', '+');
            HttpResponseMessage response = await _httpClient.PostAsync($"/user/{sanitizedName}", _params);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return "0";
            }

            var content = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var node = doc.DocumentNode.SelectSingleNode("//td[@class='xp_tracker_gain xp_tracker_pos']");
            if (node != null)
            {
                string value = node.InnerText;
                var parts = value.Split(' ');
                if (parts.Length > 1)
                {
                    value = parts[0];
                }
                return value;
            }
            return "0";
        }
    }
}
