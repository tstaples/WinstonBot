using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WinstonBot.Services
{
    public class Config
    {
        public ulong TeamConfirmationChannelId { get; set; } = 0;
        public string[] DebugTestNames { get; set; } = new string[] { };
    }

    public class ConfigService
    {
        private Config _config;
        private string _configPath;

        public Config Configuration { get => _config; }

        public ConfigService(string configPath)
        {
            _configPath = configPath;

            if (File.Exists(configPath))
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
            string value = JsonConvert.SerializeObject(_config);
            File.WriteAllText(_configPath, value);
            Console.WriteLine("Updated config file");
        }
    }
}
