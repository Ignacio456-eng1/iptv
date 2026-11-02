[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IptvPlayer.E2E;

/// <summary>Emisiones de prueba públicas (no son canales de TV): HLS con fragmentos TS.</summary>
internal static class TestStreams
{
    public const string MuxHls = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";
    public const string AppleBipBop = "https://devstreaming-cdn.apple.com/videos/streaming/examples/bipbop_4x3/bipbop_4x3_variant.m3u8";

    public static T WaitFor<T>(Func<T?> probe, TimeSpan timeout, string what) where T : class
    {
        var end = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < end)
        {
            try
            {
                if (probe() is { } value) return value;
            }
            catch (Exception e)
            {
                last = e;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Tiempo agotado esperando: {what}", last);
    }

    public static void WaitUntil(Func<bool> condition, TimeSpan timeout, string what, Func<string>? describe = null)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            try
            {
                if (condition()) return;
            }
            catch (Exception)
            {
                // El elemento puede no existir todavía.
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Tiempo agotado esperando: {what}. {describe?.Invoke()}");
    }
}
