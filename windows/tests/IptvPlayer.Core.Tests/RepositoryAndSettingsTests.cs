using IptvPlayer.Core;
using IptvPlayer.Testing;

namespace IptvPlayer.Core.Tests;

public sealed class RepositoryAndSettingsTests : IDisposable
{
    internal const string Playlist = """
        #EXTM3U
        #EXTINF:-1 group-title="Noticias",Canal 1
        http://example.com/1.m3u8
        #EXTINF:-1 group-title="Deportes",Canal 2
        http://example.com/2.m3u8
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "iptv-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task DownloadsParsesAndCaches()
    {
        using var server = new TestHttpServer(_ => (200, Playlist));
        using var repo = new PlaylistRepository(_dir);
        var channels = await repo.LoadAsync("a", new PlaylistSource.M3u(server.BaseUrl + "/lista.m3u"));
        Assert.Equal(new[] { "Canal 1", "Canal 2" }, channels.Select(c => c.Name));
        Assert.Equal(2, (await repo.CachedAsync("a")).Count);
        Assert.Empty(await repo.CachedAsync("otra"));
        Assert.True(server.Requests.TryDequeue(out var req));
        Assert.StartsWith("IptvPlayer", req.Headers["User-Agent"]);
        repo.ClearCache("a");
        Assert.Empty(await repo.CachedAsync("a"));
    }

    [Fact]
    public async Task XtreamRequestsGetPhp()
    {
        using var server = new TestHttpServer(_ => (200, Playlist));
        using var repo = new PlaylistRepository(_dir);
        await repo.LoadAsync("a", new PlaylistSource.Xtream(server.BaseUrl + "/", "ana", "secreto"));
        Assert.True(server.Requests.TryDequeue(out var req));
        Assert.Equal("/get.php?username=ana&password=secreto&type=m3u_plus&output=ts", req.Path);
    }

    [Fact]
    public async Task ErrorsAreReportedAndNothingIsCached()
    {
        using var server = new TestHttpServer(path => path.Contains("vacia") ? (200, "#EXTM3U\n") : (401, "no"));
        using var repo = new PlaylistRepository(_dir);
        var e1 = await Assert.ThrowsAsync<PlaylistException>(() => repo.LoadAsync("a", new PlaylistSource.M3u(server.BaseUrl + "/x.m3u")));
        Assert.Contains("401", e1.Message);
        var e2 = await Assert.ThrowsAsync<PlaylistException>(() => repo.LoadAsync("a", new PlaylistSource.M3u(server.BaseUrl + "/vacia.m3u")));
        Assert.Contains("no contiene canales", e2.Message);
        await Assert.ThrowsAsync<PlaylistException>(() => repo.LoadAsync("a", new PlaylistSource.M3u("ftp://x/y")));
        await Assert.ThrowsAsync<PlaylistException>(() => repo.LoadAsync("a", new PlaylistSource.M3u("http://127.0.0.1:1/nada.m3u")));
        Assert.Empty(await repo.CachedAsync("a"));
    }

    [Fact]
    public async Task LoadsLocalFile()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "mi lista.m3u");
        await File.WriteAllTextAsync(file, Playlist);
        using var repo = new PlaylistRepository(Path.Combine(_dir, "cache"));
        Assert.Equal(2, (await repo.LoadAsync("a", new PlaylistSource.M3u(file))).Count);
        Assert.Equal(2, (await repo.LoadAsync("b", new PlaylistSource.M3u("\"" + file + "\""))).Count);
        Assert.Equal(2, (await repo.LoadAsync("c", new PlaylistSource.M3u(new Uri(file).AbsoluteUri))).Count);
        Assert.Equal("mi lista", MainViewModel.DefaultName(new PlaylistSource.M3u(file), 0));
    }

    [Fact]
    public void SettingsPersistPlaylistsActiveAndFavorites()
    {
        var s = new SettingsStore(_dir);
        Assert.Empty(s.Playlists);
        Assert.Null(s.ActiveId);
        s.AddPlaylist(new SavedPlaylist("1", "Uno", new PlaylistSource.M3u("http://a/l.m3u")));
        s.AddPlaylist(new SavedPlaylist("2", "Dos", new PlaylistSource.Xtream("srv", "u", "p")));
        s.ToggleFavorite("http://x/1");
        s.ToggleFavorite("http://x/2");
        s.ToggleFavorite("http://x/1");
        s.RenamePlaylist("1", "Primera");

        var again = new SettingsStore(_dir);
        Assert.Equal(new[] { "Primera", "Dos" }, again.Playlists.Select(p => p.Name));
        Assert.Equal("2", again.ActiveId);
        Assert.Equal(new PlaylistSource.Xtream("srv", "u", "p"), again.Playlists[1].Source);
        Assert.Equal(new[] { "http://x/2" }, again.Favorites);

        again.RemovePlaylist("2");
        Assert.Equal("1", new SettingsStore(_dir).ActiveId);
    }

    [Fact]
    public void CorruptSettingsStartEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ esto no es json");
        var s = new SettingsStore(_dir);
        Assert.Empty(s.Playlists);
        s.SetActive("x");
        Assert.Null(new SettingsStore(_dir).ActiveId);
    }
}
