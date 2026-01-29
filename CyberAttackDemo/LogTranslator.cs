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
            
            // --- Hydra 関連 ---
            if (rawLog.Contains("Hydra") && rawLog.Contains("starting"))
                output = "[開始] Hydraによるパスワード解析を開始しました...";
            
            if (rawLog.Contains("login:") && rawLog.Contains("password:"))
                output = $"[試行中] {rawLog.Trim()}";
            
            if (rawLog.Contains("valid password found"))
                output = $"★ [成功] パスワードが判明しました! -> {rawLog.Trim()}";

            return output;
        }
    }
}
