namespace IptvPlayer.Core;

/// <summary>Un canal de una lista IPTV.</summary>
public sealed record Channel(
    string Name,
    string Url,
    string? Logo = null,
    string? Group = null,
    string? TvgId = null,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>Cabeceras HTTP extra que pide la lista (User-Agent, Referer...).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        Headers ?? new Dictionary<string, string>();

    /// <summary>Identificador estable para favoritos.</summary>
    public string Key => Url;
}
