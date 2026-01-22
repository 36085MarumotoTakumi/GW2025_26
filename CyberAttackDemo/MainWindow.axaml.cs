using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CyberAttackDemo;
using System;

namespace CyberAttackDemo
{
    public partial class MainWindow : Window
    {
        // 分割したクラスのインスタンス
        private readonly ConfigManager _config;
        private readonly AttackEngine _engine;

        public MainWindow()
        {
            InitializeComponent();
            
            _config = new ConfigManager();
            _engine = new AttackEngine();

            // エンジンからのログを受け取って画面に表示するイベント登録
            // UIスレッド以外から呼ばれる可能性があるため Dispatcher.UIThread.Post を使用
            _engine.OnLogReceived += msg => Dispatcher.UIThread.Post(() => WriteLog(msg));

            this.KeyDown += OnKeyDown;
            this.Opened += OnWindowOpened;
        }

        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            _config.Load();
            
            // スクリプトの準備（なければ生成）
            await _engine.EnsureAttackScriptExistsAsync();

            WriteLog("SYSTEM INITIALIZED.");
            WriteLog($"TARGET LOCKED: {_config.TargetIp}");
            WriteLog($"ATTACK TIMEOUT SET TO: {_config.DdosDuration} SECONDS");
            WriteLog("WAITING FOR USER AUTHORIZATION...");
            
            // UI表示更新
            if (TargetIpDisplay != null)
            {
                TargetIpDisplay.Text = _config.TargetIp;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // 管理者用ショートカット
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Q) Close();
            if (e.Key == Key.F11) ToggleFullScreen();
        }

        private void ToggleFullScreen()
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

        // --- フェーズ1: ポートスキャン ---
        private async void OnScanClick(object sender, RoutedEventArgs e)
        {
            SetBusyState(true, "SCANNING NETWORK...", Avalonia.Media.Brushes.Yellow);
            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING PORT SCAN ON {_config.TargetIp}...");
            WriteLog("==========================================");

            // Nmap実行 (-F: 高速スキャン)
            await _engine.RunCommandAsync("nmap", $"-F -sV {_config.TargetIp}");

            WriteLog("\n[SCAN COMPLETE] ANALYZING VULNERABILITIES...");
            
            // UI遷移
            if (Phase1Panel != null) Phase1Panel.IsVisible = false;
            if (Phase2Panel != null) Phase2Panel.IsVisible = true;
            
            if (DosAttackButton != null) DosAttackButton.IsEnabled = true; 
            if (BruteForceButton != null) BruteForceButton.IsEnabled = true;
            
            UpdateStatus("VULNERABILITY DETECTED. SELECT ACTION.", Avalonia.Media.Brushes.Red);
        }

        // --- フェーズ2A: DoS攻撃 (Shell Script) ---
        private async void OnDosAttackClick(object sender, RoutedEventArgs e)
        {
            SetBusyState(true, "EXECUTING MAXIMUM LOAD ATTACK...", Avalonia.Media.Brushes.Red);
            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING SHELL-SCRIPTED FLOOD ATTACK...");
            WriteLog($"[*] DURATION LIMIT: {_config.DdosDuration} SECONDS");
            WriteLog("[!] WARNING: EXTREME NETWORK LOAD.");
            WriteLog("==========================================");

            // スクリプトの存在確認
            await _engine.EnsureAttackScriptExistsAsync();

            // bash attack.sh <IP> <DURATION>
            string args = $"/Attack/attack.sh {_config.TargetIp} {_config.DdosDuration}";
            await _engine.RunCommandAsync("bash", args);

            WriteLog("\n[ATTACK STOPPED] SHELL SCRIPT TERMINATED.");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- フェーズ2B: パスワードクラック ---
        private async void OnBruteForceClick(object sender, RoutedEventArgs e)
        {
            SetBusyState(true, "CRACKING PASSWORDS...", Avalonia.Media.Brushes.Red);
            WriteLog("\n==========================================");
            WriteLog("[*] INITIATING BRUTE FORCE ATTACK (SSH)...");
            WriteLog("==========================================");

            // デモ用スクリプトスキャン
            await _engine.RunCommandAsync("nmap", $"-p 22 --script ssh-auth-methods {_config.TargetIp}");

            WriteLog("\n[ATTACK FINISHED] ACCESS ATTEMPTS LOGGED.");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- リセット ---
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            if (LogOutput != null) LogOutput.Text = "";
            if (Phase2Panel != null) Phase2Panel.IsVisible = false;
            if (Phase1Panel != null) Phase1Panel.IsVisible = true;
            if (ScanButton != null) ScanButton.IsEnabled = true;
            
            // 設定再読み込み
            _config.Load();
            if (TargetIpDisplay != null) TargetIpDisplay.Text = _config.TargetIp;
            
            UpdateStatus("WAITING FOR COMMAND", Avalonia.Media.Brushes.Yellow);
            WriteLog("SYSTEM RESET. READY.");
        }

        // --- UI Helper Methods ---
        private void SetBusyState(bool isBusy, string statusText, Avalonia.Media.IBrush? color = null)
        {
            if (isBusy)
            {
                if (DosAttackButton != null) DosAttackButton.IsEnabled = false;
                if (BruteForceButton != null) BruteForceButton.IsEnabled = false;
                if (ScanButton != null) ScanButton.IsEnabled = false;
            }
            else
            {
                if (DosAttackButton != null) DosAttackButton.IsEnabled = true;
                if (BruteForceButton != null) BruteForceButton.IsEnabled = true;
                if (ScanButton != null) ScanButton.IsEnabled = true;
            }
            UpdateStatus(statusText, color);
        }

        private void UpdateStatus(string text, Avalonia.Media.IBrush? color = null)
        {
            if (StatusText != null)
            {
                StatusText.Text = $"STATUS: {text}";
                if (color != null) StatusText.Foreground = color;
            }
        }

        private void WriteLog(string message)
        {
            if (LogOutput == null || LogScrollViewer == null) return;
            LogOutput.Text += $"{message}\n";
            LogScrollViewer.ScrollToEnd();
        }
    }
}