using System;
using System.IO;

namespace CyberScan
{
    public class ConfigManager
    {
        private const string ConfigFileName = "Settings.txt";

        public string TargetIp { get; private set; } = "127.0.0.1";
        public int DdosDuration { get; private set; } = 15;

        public void Load()
        {
            try
            {
                if (!File.Exists(ConfigFileName))
                {
                    CreateDefaultConfig();
                }

                string[] lines = File.ReadAllLines(ConfigFileName);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        if (key.Equals("IP", StringComparison.OrdinalIgnoreCase))
                        {
                            TargetIp = value;
                        }
                        else if (key.Equals("DDoSTime", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(value, out int duration))
                            {
                                DdosDuration = duration;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // エラー時は呼び出し元でログ出力を任せるため、ここでは再スローするか、
                // あるいはConsoleに出す等の対応が一般的ですが、今回は簡易的にデフォルト値を維持します。
                Console.WriteLine($"Config Load Error: {ex.Message}");
            }
        }

        private void CreateDefaultConfig()
        {
            string defaultSettings = "IP=127.0.0.1\nDDoSTime=15";
            File.WriteAllText(ConfigFileName, defaultSettings);
        }
    }
}