using System.Text.Json;
using System.Text.Json.Serialization;

namespace IptvPlayer.Core;

/// <summary>
/// Guarda las listas del usuario, cuál está activa y los favoritos en un JSON
/// (por defecto en %APPDATA%\IptvPlayer\settings.json).
/// </summary>
public sealed class SettingsStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private Data _data;

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _file = Path.Combine(directory, "settings.json");
        _data = Load(_file);
    }

    public IReadOnlyList<SavedPlaylist> Playlists
    {
        get { lock (_lock) return _data.Playlists.Select(ToPlaylist).OfType<SavedPlaylist>().ToList(); }
    }

    /// <summary>Lista activa; si la guardada ya no existe, la primera.</summary>
    public string? ActiveId
    {
        get
        {
            lock (_lock)
            {
                var lists = Playlists;
                return lists.Any(p => p.Id == _data.ActiveId) ? _data.ActiveId : lists.FirstOrDefault()?.Id;
            }
        }
    }

    public IReadOnlySet<string> Favorites
    {
        get { lock (_lock) return new HashSet<string>(_data.Favorites); }
    }

    /// <summary>Añade una lista nueva (o sustituye la del mismo id) y la deja activa.</summary>
    public void AddPlaylist(SavedPlaylist playlist) => Edit(d =>
    {
        d.Playlists.RemoveAll(p => p.Id == playlist.Id);
        d.Playlists.Add(ToDto(playlist));
        d.ActiveId = playlist.Id;
    });

    public void RenamePlaylist(string id, string name) => Edit(d =>
    {
        foreach (var p in d.Playlists.Where(p => p.Id == id)) p.Name = name;
    });

    public void RemovePlaylist(string id) => Edit(d =>
    {
        d.Playlists.RemoveAll(p => p.Id == id);
        if (d.ActiveId == id) d.ActiveId = d.Playlists.FirstOrDefault()?.Id;
    });

    public void SetActive(string id) => Edit(d => d.ActiveId = id);

    public void ToggleFavorite(string key) => Edit(d =>
    {
        if (!d.Favorites.Remove(key)) d.Favorites.Add(key);
    });

    private void Edit(Action<Data> change)
    {
        lock (_lock)
        {
            change(_data);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data, JsonOptions));
            File.Move(tmp, _file, overwrite: true);
        }
    }

    private static Data Load(string file)
    {
        try
        {
            if (File.Exists(file))
                return JsonSerializer.Deserialize<Data>(File.ReadAllText(file), JsonOptions) ?? new Data();
        }
        catch (Exception)
        {
            // Archivo dañado: se empieza de cero en lugar de no arrancar.
        }
        return new Data();
    }

    private static SavedPlaylist? ToPlaylist(PlaylistDto p)
    {
        PlaylistSource? source = p.Type switch
        {
            "m3u" when p.Url != null => new PlaylistSource.M3u(p.Url),
            "xtream" => new PlaylistSource.Xtream(p.Server ?? "", p.Username ?? "", p.Password ?? ""),
            _ => null,
        };
        return source == null || p.Id == null ? null : new SavedPlaylist(p.Id, p.Name ?? "Lista", source);
    }

    private static PlaylistDto ToDto(SavedPlaylist p) => p.Source switch
    {
        PlaylistSource.M3u m => new PlaylistDto { Id = p.Id, Name = p.Name, Type = "m3u", Url = m.Url },
        PlaylistSource.Xtream x => new PlaylistDto
        {
            Id = p.Id, Name = p.Name, Type = "xtream", Server = x.Server, Username = x.Username, Password = x.Password,
        },
        _ => throw new ArgumentException("Tipo de lista desconocido"),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class Data
    {
        public List<PlaylistDto> Playlists { get; set; } = new();
        public string? ActiveId { get; set; }
        public List<string> Favorites { get; set; } = new();
    }

    private sealed class PlaylistDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Url { get; set; }
        public string? Server { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
