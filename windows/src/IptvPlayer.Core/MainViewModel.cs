using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IptvPlayer.Core;

/// <summary>Fila de la lista de canales: el canal y si es favorito.</summary>
public sealed record ChannelRow(Channel Channel, bool IsFavorite)
{
    public string Name => Channel.Name;
    public string? Logo => Channel.Logo;
    public string? Group => Channel.Group;
    public string Star => IsFavorite ? "★" : "☆";
}

/// <summary>
/// Estado de la aplicación (listas, canales, grupos, búsqueda, favoritos y canal en reproducción),
/// independiente de la interfaz para poder probarlo. Los métodos se llaman desde el hilo de la interfaz.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public const string GroupAll = "Todos";
    public const string GroupFavorites = "★ Favoritos";

    private readonly SettingsStore _settings;
    private readonly PlaylistRepository _repo;
    private CancellationTokenSource? _loadCts;

    private List<Channel> _channels = new();
    private IReadOnlyList<ChannelRow> _visible = Array.Empty<ChannelRow>();
    private HashSet<string> _favorites;
    private string? _activeId;
    private string _selectedGroup = GroupAll;
    private string _query = "";
    private bool _loading;
    private string? _error;
    private Channel? _playing;
    private bool _ready;

    public MainViewModel(SettingsStore settings, PlaylistRepository repo)
    {
        _settings = settings;
        _repo = repo;
        _favorites = new HashSet<string>(settings.Favorites);
        foreach (var p in settings.Playlists) Playlists.Add(p);
        _activeId = settings.ActiveId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SavedPlaylist> Playlists { get; } = new();
    public ObservableCollection<string> Groups { get; } = new() { GroupAll };

    public bool Ready { get => _ready; private set => Set(ref _ready, value); }
    public bool HasPlaylists => Playlists.Count > 0;
    public bool Loading { get => _loading; private set => Set(ref _loading, value); }
    public string? Error { get => _error; private set => Set(ref _error, value); }
    public IReadOnlyList<Channel> Channels => _channels;
    public IReadOnlyList<ChannelRow> VisibleChannels { get => _visible; private set => Set(ref _visible, value); }
    public IReadOnlySet<string> Favorites => _favorites;
    public Channel? Playing { get => _playing; private set => Set(ref _playing, value); }

    public string? ActiveId
    {
        get => _activeId;
        private set { if (Set(ref _activeId, value)) OnPropertyChanged(nameof(Active)); }
    }

    public SavedPlaylist? Active => Playlists.FirstOrDefault(p => p.Id == _activeId);

    public string SelectedGroup
    {
        get => _selectedGroup;
        set { if (Set(ref _selectedGroup, value ?? GroupAll)) UpdateVisible(); }
    }

    public string Query
    {
        get => _query;
        set { if (Set(ref _query, value ?? "")) UpdateVisible(); }
    }

    /// <summary>Muestra la copia guardada de la lista activa y la actualiza desde la red.</summary>
    public async Task InitializeAsync()
    {
        var active = Active;
        if (active != null)
        {
            SetChannels(await _repo.CachedAsync(active.Id));
            Ready = true;
            await ReloadAsync(active);
        }
        Ready = true;
    }

    /// <summary>Añade una lista nueva: solo se guarda si se descarga bien y tiene canales. Devuelve el error, o null.</summary>
    public async Task<string?> AddPlaylistAsync(string name, PlaylistSource source)
    {
        var ct = NewLoad();
        Loading = true;
        try
        {
            var id = Guid.NewGuid().ToString();
            var channels = await _repo.LoadAsync(id, source, ct);
            var finalName = string.IsNullOrWhiteSpace(name) ? DefaultName(source, Playlists.Count) : name.Trim();
            var playlist = new SavedPlaylist(id, finalName, source);
            _settings.AddPlaylist(playlist);
            Playlists.Add(playlist);
            OnPropertyChanged(nameof(HasPlaylists));
            ActiveId = id;
            Error = null;
            SetChannels(channels, resetFilters: true);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return "Cancelado";
        }
        catch (Exception e)
        {
            return e.Message;
        }
        finally
        {
            if (!ct.IsCancellationRequested) Loading = false;
        }
    }

    /// <summary>Cambia a otra lista guardada: muestra su copia guardada y la actualiza.</summary>
    public async Task SelectPlaylistAsync(string id)
    {
        var playlist = Playlists.FirstOrDefault(p => p.Id == id);
        if (playlist == null || id == ActiveId) return;
        _settings.SetActive(id);
        ActiveId = id;
        SetChannels(await _repo.CachedAsync(id), resetFilters: true);
        await ReloadAsync(playlist);
    }

    public async Task DeletePlaylistAsync(string id)
    {
        var playlist = Playlists.FirstOrDefault(p => p.Id == id);
        if (playlist == null) return;
        var wasActive = ActiveId == id;
        _settings.RemovePlaylist(id);
        _repo.ClearCache(id);
        Playlists.Remove(playlist);
        OnPropertyChanged(nameof(HasPlaylists));
        if (!wasActive) return;

        _loadCts?.Cancel();
        Loading = false;
        Error = null;
        var next = Playlists.FirstOrDefault();
        ActiveId = next?.Id;
        if (next == null)
        {
            SetChannels(new List<Channel>(), resetFilters: true);
            return;
        }
        _settings.SetActive(next.Id);
        SetChannels(await _repo.CachedAsync(next.Id), resetFilters: true);
        await ReloadAsync(next);
    }

    public void RenamePlaylist(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var idx = Playlists.ToList().FindIndex(p => p.Id == id);
        if (idx < 0) return;
        _settings.RenamePlaylist(id, name.Trim());
        Playlists[idx] = Playlists[idx] with { Name = name.Trim() };
        OnPropertyChanged(nameof(Active));
    }

    public Task RefreshAsync() => Active is { } p ? ReloadAsync(p) : Task.CompletedTask;

    private async Task ReloadAsync(SavedPlaylist playlist)
    {
        var ct = NewLoad();
        Loading = true;
        Error = null;
        try
        {
            var channels = await _repo.LoadAsync(playlist.Id, playlist.Source, ct);
            if (ActiveId == playlist.Id) SetChannels(channels);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception e)
        {
            if (ActiveId == playlist.Id) Error = e.Message;
        }
        finally
        {
            if (!ct.IsCancellationRequested) Loading = false;
        }
    }

    private CancellationToken NewLoad()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    public void DismissError() => Error = null;

    public void ToggleFavorite(Channel channel)
    {
        _settings.ToggleFavorite(channel.Key);
        if (!_favorites.Remove(channel.Key)) _favorites.Add(channel.Key);
        OnPropertyChanged(nameof(Favorites));
        UpdateVisible();
    }

    public bool IsFavorite(Channel channel) => _favorites.Contains(channel.Key);

    public void Play(Channel channel) => Playing = channel;
    public void Stop() => Playing = null;

    /// <summary>Cambia de canal dentro de la lista visible (+1 siguiente, -1 anterior).</summary>
    public void Zap(int delta)
    {
        var list = VisibleChannels.Count > 0 ? VisibleChannels.Select(r => r.Channel).ToList() : _channels;
        var current = Playing;
        if (current == null || list.Count == 0) return;
        var idx = Math.Max(0, list.FindIndex(c => c.Key == current.Key));
        var next = (idx + delta) % list.Count;
        if (next < 0) next += list.Count;
        Playing = list[next];
    }

    private void SetChannels(List<Channel> channels, bool resetFilters = false)
    {
        _channels = channels;
        var groups = channels.Select(c => c.Group).OfType<string>().Distinct().ToList();
        var keep = !resetFilters && (groups.Contains(_selectedGroup) || _selectedGroup == GroupFavorites);

        Groups.Clear();
        Groups.Add(GroupAll);
        Groups.Add(GroupFavorites);
        foreach (var g in groups) Groups.Add(g);

        _selectedGroup = keep ? _selectedGroup : GroupAll;
        if (resetFilters) _query = "";
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(Query));
        OnPropertyChanged(nameof(Channels));
        UpdateVisible();
    }

    private void UpdateVisible()
    {
        var q = _query.Trim();
        VisibleChannels = _channels
            .Where(c => _selectedGroup switch
            {
                GroupAll => true,
                GroupFavorites => _favorites.Contains(c.Key),
                _ => c.Group == _selectedGroup,
            })
            .Where(c => q.Length == 0 || c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .Select(c => new ChannelRow(c, _favorites.Contains(c.Key)))
            .ToList();
    }

    /// <summary>Nombre por defecto: el dominio de la URL, el nombre del archivo o "Lista N".</summary>
    public static string DefaultName(PlaylistSource source, int existing)
    {
        var url = source.PlaylistUrl();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) return Path.GetFileNameWithoutExtension(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(uri.Host)) return uri.Host;
        }
        return $"Lista {existing + 1}";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
