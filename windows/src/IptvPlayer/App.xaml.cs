using System.IO;
using System.Windows;
using System.Windows.Threading;
using IptvPlayer.Core;

namespace IptvPlayer;

public partial class App : Application
{
    /// <summary>
    /// Carpeta de datos. Se puede cambiar con la variable IPTVPLAYER_DATA_DIR (lo usan las pruebas automáticas).
    /// </summary>
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("IPTVPLAYER_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IptvPlayer");

    public static string CacheDir { get; } =
        Environment.GetEnvironmentVariable("IPTVPLAYER_DATA_DIR") is { Length: > 0 } custom
            ? Path.Combine(custom, "cache")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvPlayer", "cache");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        var vm = new MainViewModel(new SettingsStore(DataDir), new PlaylistRepository(CacheDir));
        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(Path.Combine(DataDir, "error.log"), $"{DateTime.Now:u} {e.Exception}\n\n");
        }
        catch (IOException) { }
        MessageBox.Show(e.Exception.Message, "IPTV Player", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
