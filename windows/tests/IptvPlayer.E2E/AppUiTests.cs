using System.Diagnostics;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using IptvPlayer.Testing;
using Xunit.Abstractions;
using static IptvPlayer.E2E.TestStreams;

namespace IptvPlayer.E2E;

/// <summary>
/// Prueba de extremo a extremo: abre IptvPlayer.exe (variable IPTV_EXE), añade listas M3U y Xtream servidas
/// por un servidor local, busca, filtra, marca favoritos, reproduce emisiones reales, usa pantalla completa,
/// reinicia la aplicación y borra una lista.
/// </summary>
public sealed class AppUiTests : IDisposable
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(90);

    private readonly ITestOutputHelper _out;
    private readonly string _exe;
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "iptv-e2e-" + Guid.NewGuid());
    private readonly string _artifacts;
    private readonly TestHttpServer _server;
    private readonly UIA3Automation _automation = new();
    private Application? _app;

    public AppUiTests(ITestOutputHelper output)
    {
        _out = output;
        _exe = Environment.GetEnvironmentVariable("IPTV_EXE")
               ?? throw new InvalidOperationException("Define IPTV_EXE con la ruta de IptvPlayer.exe");
        _artifacts = Environment.GetEnvironmentVariable("E2E_ARTIFACTS") ?? Path.Combine(_dataDir, "capturas");
        Directory.CreateDirectory(_artifacts);
        _server = new TestHttpServer(Serve);
    }

    private (int, string) Serve(string path)
    {
        var b = _server.BaseUrl;
        if (path.StartsWith("/lista.m3u"))
            return (200, $"""
                #EXTM3U
                #EXTINF:-1 tvg-id="mux" tvg-logo="{b}/logo.png" group-title="Pruebas",Mux HLS
                {TestStreams.MuxHls}
                #EXTINF:-1 group-title="Pruebas",Apple TS
                #EXTVLCOPT:http-user-agent=IptvPlayerTest/1.0
                {TestStreams.AppleBipBop}
                #EXTINF:-1 group-title="Roto",Canal roto
                {b}/no-existe.m3u8
                """);
        if (path.StartsWith("/get.php?username=ana&password=secreta"))
            return (200, $"""
                #EXTM3U
                #EXTINF:-1 group-title="Xtream",Xtream Uno
                {TestStreams.MuxHls}
                #EXTINF:-1 group-title="Xtream",Xtream Dos
                {TestStreams.AppleBipBop}
                """);
        if (path.StartsWith("/get.php")) return (401, "credenciales incorrectas");
        return (404, "no");
    }

    public void Dispose()
    {
        try { if (_app is { HasExited: false }) _app.Kill(); } catch (Exception) { }
        _app?.Dispose();
        _automation.Dispose();
        _server.Dispose();
        try { Directory.Delete(_dataDir, recursive: true); } catch (Exception) { }
    }

    private Window Launch()
    {
        var psi = new ProcessStartInfo(_exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(_exe)! };
        psi.Environment["IPTVPLAYER_DATA_DIR"] = _dataDir;
        _app = Application.Launch(psi);
        var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(60))
                   ?? throw new TimeoutException("No apareció la ventana principal");
        WaitUntil(() => main.Title == "IPTV Player", Short, "título de la ventana");
        return main;
    }

    private void CloseApp(Window main)
    {
        // Cierre normal de la ventana (WM_CLOSE, como el botón X). Si no surtiera efecto, Alt+F4.
        main.Patterns.Window.Pattern.Close();
        var sw = Stopwatch.StartNew();
        while (!_app!.HasExited && sw.Elapsed < TimeSpan.FromSeconds(5)) Thread.Sleep(200);
        if (!_app.HasExited && !File.ReadAllText(Path.Combine(_dataDir, "app.log")).Contains("Cerrando"))
        {
            _out.WriteLine("WindowPattern.Close no cerró la ventana; se usa Alt+F4");
            main.SetForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        }
        while (!_app!.HasExited && sw.Elapsed < TimeSpan.FromSeconds(20)) Thread.Sleep(200);
        var log = Path.Combine(_dataDir, "app.log");
        if (File.Exists(log)) _out.WriteLine("app.log:\n" + File.ReadAllText(log));
        Assert.True(_app.HasExited, "La aplicación no se cerró en 20 s");
        Assert.DoesNotContain("Salida forzada", File.ReadAllText(log));
        _app.Dispose();
        _app = null;
    }

    private void Shot(Window main, string name)
    {
        try
        {
            FlaUI.Core.Capturing.Capture.Element(main).ToFile(Path.Combine(_artifacts, name + ".png"));
        }
        catch (Exception e)
        {
            _out.WriteLine($"No se pudo capturar {name}: {e.Message}");
        }
    }

    private static AutomationElement ById(AutomationElement root, string id) =>
        WaitFor(() => root.FindFirstDescendant(cf => cf.ByAutomationId(id)), Short, id);

    private Window FindDialog(Window main, string automationId) =>
        WaitFor(() =>
            main.FindFirstChild(cf => cf.ByAutomationId(automationId))?.AsWindow()
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(automationId))?.AsWindow()
            ?? main.ModalWindows.FirstOrDefault(w => w.AutomationId == automationId),
            Short, "ventana " + automationId);

    private static string[] ChannelNames(Window main) =>
        ById(main, "ChannelList").AsListBox().Items.Select(i => i.Name).ToArray();

    /// <summary>Los elementos ocultos (Collapsed) desaparecen del árbol de UI Automation.</summary>
    private static bool IsShown(Window main, string id) =>
        main.FindFirstDescendant(cf => cf.ByAutomationId(id)) is { IsOffscreen: false };

    private static string Text(Window main, string id) => ById(main, id).Name ?? "";

    private static void SetText(AutomationElement root, string id, string value)
    {
        var box = ById(root, id);
        if (box.Patterns.Value.IsSupported)
        {
            box.Patterns.Value.Pattern.SetValue(value);
        }
        else
        {
            box.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(value);
        }
    }

    private void WaitChannels(Window main, params string[] expected) =>
        WaitUntil(() => ChannelNames(main).SequenceEqual(expected), Long,
            "canales " + string.Join(", ", expected), () => "Hay: " + string.Join(", ", ChannelNames(main)) + " · Error: " + ErrorOf(main));

    private static string ErrorOf(Window main) =>
        main.FindFirstDescendant(cf => cf.ByAutomationId("ErrorText"))?.Name ?? "";

    private void WaitPlaying(Window main, string channel)
    {
        WaitUntil(() => Text(main, "NowPlayingText") == channel, Short, "canal " + channel);
        WaitUntil(() => Regex.IsMatch(Text(main, "PlayerStatus"), @"^Reproduciendo · \d+×\d+$"), Long,
            "que se vea el vídeo de " + channel, () => "Estado: " + Text(main, "PlayerStatus"));
        _out.WriteLine($"{channel}: {Text(main, "PlayerStatus")}");
    }

    [Fact]
    public void FullFlow()
    {
        var main = Launch();

        // 1. Primera vez: se abre sola la ventana "Añadir lista". Una URL que falla muestra el error.
        var dialog = FindDialog(main, "AddPlaylistWindow");
        Shot(main, "01-bienvenida");
        SetText(dialog, "NameBox", "Pruebas");
        SetText(dialog, "UrlBox", _server.BaseUrl + "/no-hay-lista.m3u");
        ById(dialog, "SaveButton").AsButton().Invoke();
        WaitUntil(() => ById(dialog, "DialogError").Name.Contains("404"), Long, "error 404 en el diálogo");
        SetText(dialog, "UrlBox", _server.BaseUrl + "/lista.m3u");
        ById(dialog, "SaveButton").AsButton().Invoke();
        WaitUntil(() => dialog.IsOffscreen || !dialog.IsAvailable, Long, "que se cierre el diálogo");

        // 2. Canales, grupos y búsqueda.
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");
        Assert.Equal("3 canales", Text(main, "CountText"));
        Assert.Equal("Pruebas", ById(main, "PlaylistCombo").AsComboBox().SelectedItem?.Name ?? ById(main, "PlaylistCombo").AsComboBox().Value);
        SetText(main, "SearchBox", "apple");
        WaitChannels(main, "Apple TS");
        Assert.Equal("1 de 3 canales", Text(main, "CountText"));
        SetText(main, "SearchBox", "");
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");
        var groups = ById(main, "GroupCombo").AsComboBox();
        groups.Select("Roto");
        WaitChannels(main, "Canal roto");
        groups.Select("Todos");
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");

        // 3. Reproducir con el teclado (seleccionar + Intro): se ve el vídeo (estado con la resolución).
        main.SetForeground();
        var list = ById(main, "ChannelList").AsListBox();
        var first = list.Items.First(i => i.Name == "Mux HLS");
        first.Select();
        first.Focus();
        Keyboard.Type(VirtualKeyShort.ENTER);
        WaitPlaying(main, "Mux HLS");
        Thread.Sleep(2000);
        Shot(main, "02-reproduciendo");

        // 4. Siguiente canal (MPEG-TS con User-Agent propio) y canal roto: muestra el error sin cerrarse.
        ById(main, "NextButton").AsButton().Invoke();
        WaitPlaying(main, "Apple TS");
        ById(main, "NextButton").AsButton().Invoke();
        WaitUntil(() => Text(main, "PlayerStatus").StartsWith("Error"), Long, "error del canal roto",
            () => "Estado: " + Text(main, "PlayerStatus"));
        Shot(main, "03-canal-roto");
        ById(main, "PrevButton").AsButton().Invoke();
        WaitPlaying(main, "Apple TS");

        // 5. Favoritos.
        var mux = ById(main, "ChannelList").AsListBox().Items.First(i => i.Name == "Mux HLS");
        mux.FindFirstDescendant(cf => cf.ByAutomationId("FavoriteButton"))!.AsButton().Invoke();
        groups.Select("★ Favoritos");
        WaitChannels(main, "Mux HLS");
        groups.Select("Todos");
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");

        // 6. Pantalla completa y salir con Esc.
        ById(main, "FullscreenButton").AsButton().Invoke();
        WaitUntil(() => !IsShown(main, "ChannelList"), Short, "que se oculte la lista en pantalla completa");
        Thread.Sleep(1500);
        Shot(main, "04-pantalla-completa");
        main.SetForeground();
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        WaitUntil(() => IsShown(main, "ChannelList"), Short, "que vuelva la lista al salir con Esc");

        // 6b. Reproducir con un clic de ratón.
        main.SetForeground();
        Thread.Sleep(500);
        ById(main, "ChannelList").AsListBox().Items.First(i => i.Name == "Mux HLS").Click();
        WaitPlaying(main, "Mux HLS");

        // 7. Segunda lista con Xtream Codes (primero con contraseña mala).
        ById(main, "AddPlaylistButton").AsButton().Invoke();
        dialog = FindDialog(main, "AddPlaylistWindow");
        ById(dialog, "XtreamTab").AsTabItem().Select();
        SetText(dialog, "NameBox", "Mi Xtream");
        SetText(dialog, "ServerBox", _server.BaseUrl);
        SetText(dialog, "UserBox", "ana");
        SetText(dialog, "PasswordBox", "mala");
        ById(dialog, "SaveButton").AsButton().Invoke();
        WaitUntil(() => ById(dialog, "DialogError").Name.Contains("401"), Long, "error 401 en el diálogo");
        SetText(dialog, "PasswordBox", "secreta");
        ById(dialog, "SaveButton").AsButton().Invoke();
        WaitUntil(() => dialog.IsOffscreen || !dialog.IsAvailable, Long, "que se cierre el diálogo Xtream");
        WaitChannels(main, "Xtream Uno", "Xtream Dos");
        Shot(main, "05-xtream");

        // 8. Cambiar de lista desde "Mis listas".
        ById(main, "PlaylistCombo").AsComboBox().Select("Pruebas");
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");
        ById(main, "PlaylistCombo").AsComboBox().Select("Mi Xtream");
        WaitChannels(main, "Xtream Uno", "Xtream Dos");

        // 9. Cerrar y volver a abrir: listas, lista activa y favoritos guardados.
        CloseApp(main);
        main = Launch();
        WaitChannels(main, "Xtream Uno", "Xtream Dos");
        var combo = ById(main, "PlaylistCombo").AsComboBox();
        combo.Expand();
        WaitUntil(() => combo.Items.Length == 2, Short, "dos listas guardadas");
        combo.Collapse();
        ById(main, "PlaylistCombo").AsComboBox().Select("Pruebas");
        WaitChannels(main, "Mux HLS", "Apple TS", "Canal roto");
        ById(main, "GroupCombo").AsComboBox().Select("★ Favoritos");
        WaitChannels(main, "Mux HLS");
        ById(main, "GroupCombo").AsComboBox().Select("Todos");

        // 10. Borrar la lista activa: pasa a la otra.
        ById(main, "PlaylistMenuButton").AsButton().Invoke();
        var delete = WaitFor(() => _automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId("DeleteMenuItem")), Short, "menú Borrar");
        delete.AsMenuItem().Invoke();
        var confirm = WaitFor(() =>
            main.FindFirstChild(cf => cf.ByName("Borrar lista"))
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Borrar lista")), Short, "confirmación de borrado");
        confirm.FindFirstDescendant(cf => cf.ByAutomationId("6"))!.AsButton().Invoke(); // botón "Sí"
        WaitChannels(main, "Xtream Uno", "Xtream Dos");
        combo = ById(main, "PlaylistCombo").AsComboBox();
        combo.Expand();
        WaitUntil(() => combo.Items.Length == 1, Short, "una lista tras borrar");
        combo.Collapse();
        Shot(main, "06-final");

        CloseApp(main);
    }
}
