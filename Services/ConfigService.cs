using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WinstonBot.Services
{
    public class CommandEntry
    {
        // Roles that can use this command
        public List<ulong> Roles { get; set; } = new();
        public Dictionary<string, List<ulong>> ActionRoles { get; set; } = new();
    }

    public class GuildEntry
    {
        // command -> [action -> role id]
        public Dictionary<string, CommandEntry> Commands { get; set; } = new();

        public Dictionary<string, List<ulong>> RolesNeededForBoss {  get; set; } = new();
        public ulong PvMRulesChannelId { get; set; }
    }

    public class Config
    {
        public Dictionary<ulong, GuildEntry> GuildEntries { get; set; } = new();
    }

    public class ConfigService
    {
        private Config _config;
        private string _configPath;

        public Config Configuration { get => _config; }

        public ConfigService(ILogger<BackgroundService> logger, IConfiguration configuration)
        {
            _configPath = configuration["guild_config_path"];
            if (_configPath == null) throw new ArgumentNullException("Failed to get guild_config_path from the config");

            if (File.Exists(_configPath))
            {
                try
                {
                    var configText = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<Config>(configText);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read config file: " + ex.Message);
                }
            }
            else
            {
                _config = new Config();
            }
        }

        public void UpdateConfig(Config newConfig)
        {
            _config = newConfig;
            string value = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(_configPath, value);
            Console.WriteLine("Updated config file");
        }

        //protected override Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    return Task.CompletedTask;
        //}
    }
}
