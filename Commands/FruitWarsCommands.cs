using Discord;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WinstonBot.Attributes;
using System.Net;
using HtmlAgilityPack;
using System.Text;
using Discord.WebSocket;

namespace WinstonBot.Commands
{
    [Command("leaderboard", "Shows the Fruit Wars Leaderboard", DefaultPermission.AdminOnly)]
    internal class FruitWarsCommands : CommandBase
    {
        public FruitWarsCommands(ILogger logger) : base(logger) { }

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

        private static Dictionary<string, List<KeyValuePair<string, int>>> LastTeamValues = new()
        {
            { "Grape", new List<KeyValuePair<string, int>>() },
            { "Apple", new List<KeyValuePair<string, int>>() },
            { "Cherry", new List<KeyValuePair<string, int>>() },
            { "Peach", new List<KeyValuePair<string, int>>() },
        };

        public async override Task HandleCommand(CommandContext context)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
            {
                await context.RespondAsync("Command already running.");
                return;
            }

            await context.RespondAsync("Calculating Results... Call Blaviken a boomer in the meantime.");

            Task.Run(async () =>
            {
                await PostResults(context.SlashCommand.Channel, Logger, autoPost: false);
                Interlocked.Decrement(ref _running);
            }).Forget();
        }

        public static async Task PostResults(ISocketMessageChannel channel, ILogger logger, bool autoPost = true)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://www.runeclan.com/");
            var requestArgs = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "dxp_col", "dxp"}
            });

            List<KeyValuePair<int, string>> messageResults = new();

            // Run the operation for each team in parallel
            bool anySuccess = false;
            foreach (var kvp in Teams)
            {
                // Query runeclan for each user in parallel
                List<KeyValuePair<string, int>> rsnXpMap = new();

                // Go nice and slow so we don't annoy Runeclan too much.
                foreach (string name in kvp.Value)
                {
                    string xp = await GetUserXp(httpClient, requestArgs, name);
                    anySuccess = xp != null ? true : anySuccess;
                    xp = xp ?? "0";

                    logger.LogDebug($"{name} - {xp}");

                    int xpVal = int.Parse(xp, System.Globalization.NumberStyles.AllowThousands);
                    rsnXpMap.Add(new KeyValuePair<string, int>(name, xpVal));

                    Thread.Sleep(500);
                }

                // Sort descending
                rsnXpMap.Sort((a, b) => a.Value.CompareTo(b.Value));
                rsnXpMap.Reverse();

                // Sum total xp
                int totalXp = 0;
                rsnXpMap.ForEach((kvp) => totalXp += kvp.Value);

                // Format the shit
                string message = GetFormattedTeamResult(kvp.Key, totalXp, rsnXpMap);
                messageResults.Add(new KeyValuePair<int, string>(totalXp, message));

                if (autoPost)
                {
                    LastTeamValues[kvp.Key] = rsnXpMap;
                }
            }

            if (!anySuccess)
            {
                logger.LogWarning("Failed to retrieve data from RuneClan, site is likely down.");
                await channel.SendMessageAsync("Failed to retrieve data from RuneClan, site is likely down.");
                return;
            }

            messageResults.Sort((a, b) => a.Key.CompareTo(b.Key));
            messageResults.Reverse();

            foreach (var kvp in messageResults)
            {
                await channel.SendMessageAsync(kvp.Value);
            }
        }
        
        private static string GetFormattedTeamResult(string teamName, int totalXp, List<KeyValuePair<string, int>> users)
        {
            var lastHourXp = LastTeamValues[teamName];

            var builder = new StringBuilder();
            builder
                .AppendLine("```")
                .AppendLine(lineSep)
                .AppendLine($"Team {teamName} - {String.Format("{0:n0}", totalXp)}")
                .AppendLine(lineSep);
            foreach (var kvp in users)
            {
                int lastXp = lastHourXp.FirstOrDefault(pair => pair.Key == kvp.Key).Value;
                int xpDiff = lastXp > 0 ? kvp.Value - lastXp : 0;

                string formattedName = String.Format("{0,-15}", kvp.Key);
                string formattedXP = String.Format("{0,-15:n0}", kvp.Value);
                string formattedXPDiff = xpDiff > 0 ? String.Format("+{0:n0}", xpDiff) : "";

                builder.AppendLine($"{formattedName}{formattedXP}{formattedXPDiff}");
            }

            builder
                .AppendLine(lineSep)
                .AppendLine("```");

            return builder.ToString();
        }

        private static async Task<string> GetUserXp(HttpClient httpClient, FormUrlEncodedContent requestArgs, string name)
        {
            var sanitizedName = name.Replace(' ', '+');
            HttpResponseMessage response = await httpClient.PostAsync($"/user/{sanitizedName}", requestArgs);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
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
