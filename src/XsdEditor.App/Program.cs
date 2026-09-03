using Avalonia;

namespace XsdEditor.App;

internal static class Program
{
    // Avalonia configuration. Must not be renamed or made async: the visual designer
    // and the XAML tooling both look for this shape.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            // XE-026 wants the platform's own UI face, resolved from the system rather
            // than shipped, so no font package is referenced here.
            .UsePlatformDetect()
            .LogToTrace();
}
