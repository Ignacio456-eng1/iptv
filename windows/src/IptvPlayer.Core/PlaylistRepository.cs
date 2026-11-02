using System.Net;
using System.Text;

namespace IptvPlayer.Core;

/// <summary>Descarga las listas, guarda una copia en disco por lista y las convierte en canales.</summary>
public sealed class PlaylistRepository : IDisposable
{
    public const string UserAgent = "IptvPlayer/0.2 (Windows)";

    private readonly string _dir;
    private readonly HttpClient _http;

    public PlaylistRepository(string cacheDirectory, HttpMessageHandler? handler = null)
    {
        _dir = cacheDirectory;
        Directory.CreateDirectory(_dir);
        handler ??= new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    private string CacheFile(string id) =>
        Path.Combine(_dir, $"playlist_{new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray())}.m3u");

    /// <summary>Canales de la última descarga correcta de esa lista (para arrancar rápido o sin red).</summary>
    public async Task<List<Channel>> CachedAsync(string id)
    {
        var file = CacheFile(id);
        if (!File.Exists(file)) return new List<Channel>();
        var text = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        return await Task.Run(() => M3uParser.Parse(text)).ConfigureAwait(false);
    }

    /// <summary>Descarga la lista, la valida y, si tiene canales, guarda la copia con ese id.</summary>
    public async Task<List<Channel>> LoadAsync(string id, PlaylistSource source, CancellationToken ct = default)
    {
        var location = source.PlaylistUrl();
        if (location.Length == 0) throw new PlaylistException("Falta la dirección de la lista");

        string text;
        if (IsLocalFile(location, out var path))
        {
            if (!File.Exists(path)) throw new PlaylistException($"No se encuentra el archivo {path}");
            text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        else
        {
            if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new PlaylistException("La dirección no es válida (debe empezar por http:// o https://)");
            try
            {
                using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new PlaylistException($"El servidor respondió {(int)resp.StatusCode}");
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                text = Encoding.UTF8.GetString(bytes);
            }
            catch (HttpRequestException e)
            {
                throw new PlaylistException($"No se pudo conectar: {e.Message}", e);
            }
            catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
            {
                throw new PlaylistException("El servidor tardó demasiado en responder", e);
            }
        }

        var channels = await Task.Run(() => M3uParser.Parse(text), ct).ConfigureAwait(false);
        if (channels.Count == 0) throw new PlaylistException("La lista no contiene canales válidos");
        await File.WriteAllTextAsync(CacheFile(id), text, ct).ConfigureAwait(false);
        return channels;
    }

    public void ClearCache(string id)
    {
        try { File.Delete(CacheFile(id)); } catch (IOException) { }
    }

    internal static bool IsLocalFile(string location, out string path)
    {
        path = location;
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) { path = uri.LocalPath; return true; }
            return false;
        }
        // Rutas relativas o sin esquema que existan como archivo.
        return File.Exists(location);
    }

    public void Dispose() => _http.Dispose();
}

public sealed class PlaylistException(string message, Exception? inner = null) : Exception(message, inner);
