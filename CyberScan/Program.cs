using Avalonia;
using Avalonia.Media; // ★追加: FontManagerOptionsを使うために必要
using Avalonia.ReactiveUI;
using System;

namespace CyberScan
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont() // 内蔵フォント(Inter)をロード
                .With(new FontManagerOptions
                {
                    // ★追加: OSのデフォルトフォントが見つからない場合のエラー回避策
                    // WithInterFont()でロードされた "Inter" をアプリ全体のデフォルトに強制指定します
                    DefaultFamilyName = "Inter"
                })
                .LogToTrace()
                .UseReactiveUI();
    }
}