namespace CyberAttackDemo
{
    public static class LogTranslator
    {
        public static string Translate(string rawLog)
        {
            string output = rawLog;
            
            // --- Nmap 関連 ---
            if (rawLog.Contains("80/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] Webサーバー(HTTP)が動いています。";
            if (rawLog.Contains("443/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] SSL Webサーバー(HTTPS)です。DoS攻撃の標的になります。";
            if (rawLog.Contains("22/tcp") && rawLog.Contains("open")) 
                output += "   <-- [発見] SSHポートです。パスワードクラック可能です。";
            
            // --- 攻撃スクリプト (hping3) 関連 ---
            if (rawLog.Contains("Vector 1")) 
                output = $"[攻撃プロセス起動] TCP SYN Flood (Port 443/HTTPS) 開始...";
            if (rawLog.Contains("Vector 2")) 
                output = $"[攻撃プロセス起動] TCP SYN Flood (Port 80/HTTP) 開始...";
            if (rawLog.Contains("Vector 3")) 
                output = $"[攻撃プロセス起動] UDP Flood (帯域幅枯渇攻撃) 開始...";
            if (rawLog.Contains("Vector 4")) 
                output = $"[攻撃プロセス起動] ICMP Large Packet Flood (CPU負荷攻撃) 開始...";
            
            if (rawLog.Contains("ALL GUNS BLAZING"))
            {
                output = $"[全全開] 全ての攻撃ベクトルが最大出力で実行中。ネットワーク負荷が極大化しています...";
            }
            if (rawLog.Contains("CEASE FIRE"))
            {
                output = $"[攻撃停止] 制限時間に達しました。攻撃を終了します。";
            }

            // --- NetSTRIK 関連 ---
            if (rawLog.Contains("NetSTRIK") || rawLog.Contains(".py"))
            {
                output = $"[外部ツール] Pythonスクリプトを実行中... ({rawLog.Trim()})";
            }
            
            return output;
        }
    }
}