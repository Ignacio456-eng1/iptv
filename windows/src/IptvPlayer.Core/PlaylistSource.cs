namespace IptvPlayer.Core;

/// <summary>De dónde sale la lista de canales. El usuario aporta siempre su propia fuente.</summary>
public abstract record PlaylistSource
{
    /// <summary>URL (o ruta de archivo local) que devuelve una lista M3U/M3U8.</summary>
    public abstract string PlaylistUrl();

    /// <summary>Lista M3U por URL http(s) o archivo local (.m3u / .m3u8).</summary>
    public sealed record M3u(string Url) : PlaylistSource
    {
        public override string PlaylistUrl() => Url.Trim().Trim('"');
    }

    /// <summary>Proveedor con API Xtream Codes: se pide su lista M3U con las credenciales.</summary>
    public sealed record Xtream(string Server, string Username, string Password) : PlaylistSource
    {
        public override string PlaylistUrl()
        {
            var b = Server.Trim().TrimEnd('/');
            if (!b.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                b = "http://" + b;
            var u = Uri.EscapeDataString(Username.Trim());
            var p = Uri.EscapeDataString(Password.Trim());
            return $"{b}/get.php?username={u}&password={p}&type=m3u_plus&output=ts";
        }
    }
}

/// <summary>Una lista guardada por el usuario.</summary>
public sealed record SavedPlaylist(string Id, string Name, PlaylistSource Source)
{
    /// <summary>Texto corto para mostrar debajo del nombre.</summary>
    public string Description => Source switch
    {
        PlaylistSource.M3u m => m.Url,
        PlaylistSource.Xtream x => $"Xtream · {x.Username} @ {x.Server}",
        _ => "",
    };

    public override string ToString() => Name;
}
