using System;
using System.IO;

namespace CyberAttackDemo
{
    public class ConfigManager
    {
        private const string ConfigFileName = "Settings.txt";

        public string TargetIp { get; private set; } = "127.0.0.1";
        public int DdosDuration { get; private set; } = 15;
        public bool IsDebugMode { get; private set; } = false;
        // 追加: SSHユーザー名
        public string SshUser { get; private set; } = "root";

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
                        else if (key.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                        {
                            if (bool.TryParse(value, out bool isDebug))
                            {
                                IsDebugMode = isDebug;
                            }
                        }
                        // 追加: SSHUserの読み込み
                        else if (key.Equals("SSHUser", StringComparison.OrdinalIgnoreCase))
                        {
                            SshUser = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config Load Error: {ex.Message}");
            }
        }

        private void CreateDefaultConfig()
        {
            // デフォルト設定
            string defaultSettings = "IP=127.0.0.1\nDDoSTime=15\nDebug=false\nSSHUser=root";
            File.WriteAllText(ConfigFileName, defaultSettings);
        }
    }
}