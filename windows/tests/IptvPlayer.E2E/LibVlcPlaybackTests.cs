using LibVLCSharp.Shared;

namespace IptvPlayer.E2E;

/// <summary>Comprueba que el libVLC que se distribuye abre y decodifica emisiones HLS reales.</summary>
public class LibVlcPlaybackTests
{
    [Theory]
    [InlineData(TestStreams.MuxHls)]
    [InlineData(TestStreams.AppleBipBop)]
    public void PlaysHlsStream(string url)
    {
        Core.Initialize();
        using var libVlc = new LibVLC("--vout=dummy", "--aout=dummy", "--no-video-title-show");
        using var player = new MediaPlayer(libVlc);
        var playing = new ManualResetEventSlim();
        var failed = new ManualResetEventSlim();
        player.Playing += (_, _) => playing.Set();
        player.EncounteredError += (_, _) => failed.Set();

        using var media = new Media(libVlc, url, FromType.FromLocation);
        media.AddOption(":network-caching=2000");
        Assert.True(player.Play(media));

        var which = WaitHandle.WaitAny(new[] { playing.WaitHandle, failed.WaitHandle }, TimeSpan.FromSeconds(60));
        Assert.True(which == 0, which == 1 ? "VLC dio error al abrir la emisión" : "La emisión no empezó en 60 s");

        // El tiempo de reproducción avanza: se están decodificando fragmentos.
        TestStreams.WaitUntil(() => player.Time > 3000, TimeSpan.FromSeconds(45), "que avance la reproducción",
            () => $"tiempo={player.Time} estado={player.State}");
        Assert.False(failed.IsSet);
        player.Stop();
    }

    [Fact]
    public void ReportsErrorForBrokenStream()
    {
        Core.Initialize();
        using var server = new Testing.TestHttpServer(_ => (404, "no"));
        using var libVlc = new LibVLC("--vout=dummy", "--aout=dummy");
        using var player = new MediaPlayer(libVlc);
        var failed = new ManualResetEventSlim();
        player.EncounteredError += (_, _) => failed.Set();
        player.EndReached += (_, _) => failed.Set();
        using var media = new Media(libVlc, server.BaseUrl + "/roto.m3u8", FromType.FromLocation);
        player.Play(media);
        Assert.True(failed.Wait(TimeSpan.FromSeconds(30)), "VLC debería informar de que no puede abrirla");
        player.Stop();
    }
}
