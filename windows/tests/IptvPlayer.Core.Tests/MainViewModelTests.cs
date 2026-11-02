using IptvPlayer.Core;
using IptvPlayer.Testing;

namespace IptvPlayer.Core.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private const string Second = """
        #EXTM3U
        #EXTINF:-1 group-title="Cine",Peli 1
        http://example.com/p1.m3u8
        #EXTINF:-1 group-title="Cine",Peli 2
        http://example.com/p2.m3u8
        #EXTINF:-1 group-title="Música",Radio
        http://example.com/r.mp3
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "iptv-vm-" + Guid.NewGuid());
    private readonly TestHttpServer _server = new(path => path switch
    {
        _ when path.StartsWith("/uno") => (200, RepositoryAndSettingsTests.Playlist),
        _ when path.StartsWith("/dos") => (200, Second),
        _ when path.StartsWith("/get.php?username=ana&password=ok") => (200, Second),
        _ => (404, "no"),
    });

    public void Dispose()
    {
        _server.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private MainViewModel NewVm() =>
        new(new SettingsStore(Path.Combine(_dir, "data")), new PlaylistRepository(Path.Combine(_dir, "cache")));

    [Fact]
    public async Task AddSwitchFilterFavoriteZapDelete()
    {
        var vm = NewVm();
        await vm.InitializeAsync();
        Assert.True(vm.Ready);
        Assert.False(vm.HasPlaylists);

        // Una lista que falla no se guarda.
        Assert.NotNull(await vm.AddPlaylistAsync("Mala", new PlaylistSource.M3u(_server.BaseUrl + "/nada.m3u")));
        Assert.False(vm.HasPlaylists);

        Assert.Null(await vm.AddPlaylistAsync("", new PlaylistSource.M3u(_server.BaseUrl + "/uno.m3u")));
        Assert.Equal("127.0.0.1", vm.Active!.Name);
        Assert.Equal(2, vm.VisibleChannels.Count);
        Assert.Equal(new[] { MainViewModel.GroupAll, MainViewModel.GroupFavorites, "Noticias", "Deportes" }, vm.Groups);

        Assert.Null(await vm.AddPlaylistAsync("Xtream", new PlaylistSource.Xtream(_server.BaseUrl, "ana", "ok")));
        Assert.Equal(2, vm.Playlists.Count);
        Assert.Equal("Xtream", vm.Active!.Name);
        Assert.Equal(3, vm.VisibleChannels.Count);

        vm.SelectedGroup = "Cine";
        Assert.Equal(new[] { "Peli 1", "Peli 2" }, vm.VisibleChannels.Select(r => r.Name));
        vm.Query = "2";
        Assert.Equal(new[] { "Peli 2" }, vm.VisibleChannels.Select(r => r.Name));
        vm.Query = "";

        vm.ToggleFavorite(vm.VisibleChannels[1].Channel);
        Assert.True(vm.VisibleChannels[1].IsFavorite);
        vm.SelectedGroup = MainViewModel.GroupFavorites;
        Assert.Equal(new[] { "Peli 2" }, vm.VisibleChannels.Select(r => r.Name));

        vm.SelectedGroup = MainViewModel.GroupAll;
        vm.Play(vm.VisibleChannels[2].Channel);
        vm.Zap(+1);
        Assert.Equal("Peli 1", vm.Playing!.Name);
        vm.Zap(-1);
        Assert.Equal("Radio", vm.Playing!.Name);

        // Cambiar de lista, renombrar y reabrir: todo queda guardado.
        var first = vm.Playlists[0].Id;
        await vm.SelectPlaylistAsync(first);
        Assert.Equal(new[] { "Canal 1", "Canal 2" }, vm.VisibleChannels.Select(r => r.Name));
        vm.RenamePlaylist(first, "Mi lista");
        Assert.Equal("Mi lista", vm.Active!.Name);

        var reopened = NewVm();
        Assert.Equal(first, reopened.ActiveId);
        await reopened.InitializeAsync();
        Assert.Equal(2, reopened.VisibleChannels.Count);
        Assert.Contains("http://example.com/p2.m3u8", reopened.Favorites);

        await reopened.DeletePlaylistAsync(first);
        Assert.Single(reopened.Playlists);
        Assert.Equal("Xtream", reopened.Active!.Name);
        Assert.Equal(3, reopened.VisibleChannels.Count);
        await reopened.DeletePlaylistAsync(reopened.ActiveId!);
        Assert.False(reopened.HasPlaylists);
        Assert.Empty(reopened.VisibleChannels);
    }

    [Fact]
    public async Task ShowsCachedChannelsWhenServerIsDown()
    {
        var vm = NewVm();
        Assert.Null(await vm.AddPlaylistAsync("Uno", new PlaylistSource.M3u(_server.BaseUrl + "/uno.m3u")));
        _server.Dispose();

        var offline = NewVm();
        await offline.InitializeAsync();
        Assert.Equal(2, offline.VisibleChannels.Count);
        Assert.NotNull(offline.Error);
        Assert.False(offline.Loading);
    }
}
