using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CyberAttackDemo
{
    public class AttackEngine
    {
        // ログ出力イベント（UIに通知するため）
        public event Action<string>? OnLogReceived;
        
        public const string AttackScriptName = @"./Attack/attack.sh";

        // 外部コマンド実行
        public async Task RunCommandAsync(string command, string args)
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

                // 標準出力
                process.OutputDataReceived += (s, e) => 
                {
                    if (e.Data != null)
                    {
                        // ここで翻訳ロジックを通す
                        string translated = LogTranslator.Translate(e.Data);
                        OnLogReceived?.Invoke(translated);
                    }
                };
                
                // エラー出力
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) OnLogReceived?.Invoke($"[STDERR] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Command Execution Failed: {ex.Message}");
                
                // デモ用フォールバックメッセージ
                await Task.Delay(1000);
                if (command.Contains("bash") || args.Contains("attack.sh"))
                {
                     OnLogReceived?.Invoke("[SIMULATION] Executing shell script sequence...");
                }
                else
                {
                    OnLogReceived?.Invoke("Target appears to be secure or tool not installed.");
                }
            }
        }

        // 攻撃用シェルスクリプトの存在確認（自動生成は削除）
        public async Task EnsureAttackScriptExistsAsync()
        {
            try
            {
                string? dir = Path.GetDirectoryName(AttackScriptName);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    OnLogReceived?.Invoke($"[SYSTEM] Created directory: {dir}");
                }

                // ファイルが存在するか確認するだけに変更（上書きしない）
                if (File.Exists(AttackScriptName))
                {
                    OnLogReceived?.Invoke($"[SYSTEM] Loaded attack script: {AttackScriptName}");
                    
                    // 念のため実行権限を付与
                    try { Process.Start("chmod", $"+x {AttackScriptName}").WaitForExit(); } catch {}
                }
                else
                {
                    OnLogReceived?.Invoke($"[WARNING] Attack script not found at: {AttackScriptName}");
                    OnLogReceived?.Invoke($"[WARNING] Please place your 'attack.sh' file in the 'Attack' folder.");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Failed to check script: {ex.Message}");
            }
        }
    }
}