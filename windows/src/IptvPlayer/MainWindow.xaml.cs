using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IptvPlayer.Core;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace IptvPlayer;

public partial class MainWindow : Window
{
    private const string IconPlay = "";
    private const string IconPause = "";
    private const string IconVolume = "";
    private const string IconMuted = "";
    private const string IconFullscreen = "";
    private const string IconExitFullscreen = "";

    private readonly MainViewModel _vm;
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly DispatcherTimer _osdTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    // Las órdenes a VLC (que pueden bloquear un momento) se ejecutan en segundo plano y en orden.
    private Task _playerQueue = Task.CompletedTask;
    private bool _syncingCombo;
    private bool _fullscreen;
    private bool _playbackFailed;
    private (WindowState State, WindowStyle Style, ResizeMode Resize, GridLength Sidebar) _beforeFullscreen;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;

        // En pantallas pequeñas, la ventana no debe salirse del escritorio.
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width * 0.92);
        Height = Math.Min(Height, area.Height * 0.92);
        if (Width < 1000) SidebarColumn.Width = new GridLength(300);

        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--no-snapshot-preview", "--no-stats");
        _player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true,
            EnableKeyInput = false,
            EnableMouseInput = false,
        };
        VideoView.MediaPlayer = _player;
        HookPlayerEvents();

        _vm.PropertyChanged += OnViewModelChanged;
        _vm.Playlists.CollectionChanged += (_, _) => SyncPlaylistCombo();
        _osdTimer.Tick += (_, _) => { _osdTimer.Stop(); OsdBox.Visibility = Visibility.Collapsed; };

        Loaded += OnLoaded;
        Overlay.Loaded += (_, _) =>
        {
            // La capa sobre el vídeo vive en su propia ventana: también ahí funcionan los atajos.
            if (GetWindow(Overlay) is { } w && !ReferenceEquals(w, this)) w.PreviewKeyDown += OnPreviewKeyDown;
        };
        Closing += OnClosing;

        SyncPlaylistCombo();
        UpdateCount();
        UpdatePlaceholder();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();
        UpdatePlaceholder();
        if (!_vm.HasPlaylists)
        {
            // Primera vez: se abre directamente la ventana para añadir la lista.
            await Dispatcher.InvokeAsync(ShowAddPlaylistDialog, DispatcherPriority.ApplicationIdle);
        }
    }

    private bool _readyToClose;

    /// <summary>
    /// VLC se detiene antes de destruir la ventana y en segundo plano: si se detiene desde el hilo de la
    /// interfaz con la ventana ya cerrada, la salida de vídeo espera a ese mismo hilo y la aplicación se cuelga.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_readyToClose) return;
        e.Cancel = true;
        App.Log("Cerrando: deteniendo VLC");
        Hide();
        var stop = _playerQueue.ContinueWith(_ =>
        {
            try { _player.Stop(); } catch (Exception) { }
        }, TaskScheduler.Default);
        await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5)));
        App.Log(stop.IsCompleted ? "VLC detenido" : "VLC no se detuvo en 5 s");
        if (stop.IsCompleted)
        {
            VideoView.MediaPlayer = null;
            await Task.WhenAny(Task.Run(() =>
            {
                try { _player.Dispose(); _libVlc.Dispose(); } catch (Exception) { }
            }), Task.Delay(TimeSpan.FromSeconds(3)));
        }
        App.Log("Cerrando ventana");
        _readyToClose = true;
        Close();
    }

    // ---------- Estado → interfaz ----------

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Playing):
                StartPlayback(_vm.Playing);
                break;
            case nameof(MainViewModel.VisibleChannels):
            case nameof(MainViewModel.Channels):
                UpdateCount();
                SelectPlayingInList(scroll: false);
                break;
            case nameof(MainViewModel.Active):
            case nameof(MainViewModel.ActiveId):
                SyncPlaylistCombo();
                break;
            case nameof(MainViewModel.HasPlaylists):
                UpdatePlaceholder();
                break;
        }
    }

    private void SyncPlaylistCombo()
    {
        _syncingCombo = true;
        try { PlaylistCombo.SelectedItem = _vm.Active; }
        finally { _syncingCombo = false; }
        RenameMenuItem.IsEnabled = DeleteMenuItem.IsEnabled = RefreshMenuItem.IsEnabled = _vm.Active != null;
    }

    private void UpdateCount()
    {
        var shown = _vm.VisibleChannels.Count;
        var total = _vm.Channels.Count;
        CountText.Text = total == 0 ? "" : shown == total ? $"{total} canales" : $"{shown} de {total} canales";
    }

    private void UpdatePlaceholder()
    {
        if (_vm.Playing != null && !_playbackFailed)
        {
            Placeholder.Visibility = Visibility.Collapsed;
            return;
        }
        Placeholder.Visibility = Visibility.Visible;
        if (_playbackFailed && _vm.Playing != null)
        {
            PlaceholderTitle.Text = $"No se pudo reproducir «{_vm.Playing.Name}»";
            PlaceholderText.Text = "Puede que el canal no esté emitiendo ahora o que el enlace haya caducado. Prueba otro canal o actualiza la lista.";
        }
        else if (!_vm.HasPlaylists)
        {
            PlaceholderTitle.Text = "Bienvenido";
            PlaceholderText.Text = "Añade tu lista M3U o tus datos Xtream Codes con el botón + de arriba a la izquierda. La aplicación no incluye canales.";
        }
        else
        {
            PlaceholderTitle.Text = "Elige un canal";
            PlaceholderText.Text = "Haz clic en un canal de la lista. Doble clic sobre el vídeo o F11 para pantalla completa; RePág/AvPág cambian de canal.";
        }
    }

    private void ShowOsd(string text)
    {
        OsdText.Text = text;
        OsdBox.Visibility = Visibility.Visible;
        _osdTimer.Stop();
        _osdTimer.Start();
    }

    private void SelectPlayingInList(bool scroll)
    {
        var playing = _vm.Playing;
        if (playing == null) return;
        var row = _vm.VisibleChannels.FirstOrDefault(r => r.Channel.Key == playing.Key);
        if (row == null) return;
        ChannelList.SelectedItem = row;
        if (scroll) ChannelList.ScrollIntoView(row);
    }

    // ---------- Reproductor ----------

    private void EnqueuePlayer(Action action)
    {
        _playerQueue = _playerQueue.ContinueWith(_ =>
        {
            try { action(); }
            catch (Exception) { /* VLC informa de los errores con sus eventos */ }
        }, TaskScheduler.Default);
    }

    private void StartPlayback(Channel? channel)
    {
        _playbackFailed = false;
        _videoSize = null;
        Interlocked.Increment(ref _sizeRequests);
        if (channel == null)
        {
            EnqueuePlayer(() => _player.Stop());
            NowPlayingText.Text = "";
            SetStatus("");
            UpdatePlaceholder();
            return;
        }

        var media = new Media(_libVlc, channel.Url, FromType.FromLocation);
        media.AddOption(":network-caching=2000");
        media.AddOption(":http-reconnect");
        if (channel.Headers.TryGetValue("User-Agent", out var ua) && ua.Length > 0) media.AddOption($":http-user-agent={ua}");
        if (channel.Headers.TryGetValue("Referer", out var referer) && referer.Length > 0) media.AddOption($":http-referrer={referer}");

        EnqueuePlayer(() =>
        {
            _player.Play(media);
            media.Dispose();
        });

        NowPlayingText.Text = channel.Name;
        SetStatus("Conectando…");
        PlayPauseButton.Content = IconPause;
        UpdatePlaceholder();
        ShowOsd(channel.Name);
        SelectPlayingInList(scroll: true);
    }

    // Estado del reproductor según sus eventos. La interfaz nunca llama a VLC de forma síncrona: si el hilo de la
    // interfaz espera a VLC mientras la salida de vídeo espera a la ventana, la aplicación se queda bloqueada.
    private volatile VLCState _state = VLCState.NothingSpecial;
    private volatile bool _muted;
    private string? _videoSize;
    private int _sizeRequests;

    private void HookPlayerEvents()
    {
        // Los eventos llegan desde hilos de VLC: se pasan al hilo de la interfaz y nunca se llama a VLC desde ellos.
        _player.Opening += (_, _) => _state = VLCState.Opening;
        _player.Playing += (_, _) =>
        {
            _state = VLCState.Playing;
            Ui(() =>
            {
                _playbackFailed = false;
                PlayPauseButton.Content = IconPause;
                ShowPlayingStatus();
                UpdatePlaceholder();
            });
            RequestVideoSize();
        };
        _player.Vout += (_, e) => { if (e.Count > 0) RequestVideoSize(); };
        _player.Buffering += (_, e) => Ui(() =>
        {
            if (e.Cache < 100f && _vm.Playing != null) SetStatus($"Cargando… {e.Cache:0}%");
            else if (_state == VLCState.Playing) ShowPlayingStatus();
        });
        _player.Paused += (_, _) =>
        {
            _state = VLCState.Paused;
            Ui(() => { SetStatus("En pausa"); PlayPauseButton.Content = IconPlay; });
        };
        _player.EncounteredError += (_, _) =>
        {
            _state = VLCState.Error;
            Ui(() =>
            {
                _playbackFailed = true;
                SetStatus("Error: no se pudo reproducir este canal");
                PlayPauseButton.Content = IconPlay;
                UpdatePlaceholder();
            });
        };
        _player.EndReached += (_, _) =>
        {
            _state = VLCState.Ended;
            Ui(() =>
            {
                SetStatus("La emisión se ha cortado. Pulsa ▶ para reintentar.");
                PlayPauseButton.Content = IconPlay;
            });
        };
        _player.Stopped += (_, _) =>
        {
            _state = VLCState.Stopped;
            Ui(() =>
            {
                if (_vm.Playing == null) SetStatus("");
                PlayPauseButton.Content = IconPlay;
            });
        };
    }

    private void ShowPlayingStatus() =>
        SetStatus(_videoSize is { } size ? $"Reproduciendo · {size}" : "Reproduciendo");

    /// <summary>Pregunta a VLC la resolución en un hilo aparte (nunca desde sus eventos ni desde la interfaz).</summary>
    private void RequestVideoSize()
    {
        var request = Interlocked.Increment(ref _sizeRequests);
        Task.Run(async () =>
        {
            for (var i = 0; i < 20 && request == _sizeRequests; i++)
            {
                uint w = 0, h = 0;
                bool ok;
                try { ok = _player.Size(0, ref w, ref h) && w > 0 && h > 0; }
                catch (Exception) { ok = false; }
                if (ok)
                {
                    Ui(() =>
                    {
                        if (request != _sizeRequests) return;
                        _videoSize = $"{w}×{h}";
                        if (_state == VLCState.Playing) ShowPlayingStatus();
                    });
                    return;
                }
                await Task.Delay(250);
            }
        });
    }

    private void Ui(Action action) => Dispatcher.BeginInvoke(action);

    private void SetStatus(string text) => PlayerStatusText.Text = text;

    private void TogglePause()
    {
        if (_vm.Playing == null) return;
        var state = _state;
        if (state == VLCState.Playing || state == VLCState.Buffering || state == VLCState.Opening)
            EnqueuePlayer(() => _player.SetPause(true));
        else if (state == VLCState.Paused)
            EnqueuePlayer(() => _player.SetPause(false));
        else
            StartPlayback(_vm.Playing); // detenido, error o fin: se vuelve a conectar
    }

    private void ToggleMute()
    {
        _muted = !_muted;
        var muted = _muted;
        Task.Run(() => { try { _player.Mute = muted; } catch (Exception) { } });
        MuteButton.Content = muted ? IconMuted : IconVolume;
        ShowOsd(muted ? "Silenciado" : $"Volumen {(int)VolumeSlider.Value}%");
    }

    // ---------- Pantalla completa ----------

    private void ToggleFullscreen()
    {
        if (_fullscreen) ExitFullscreen(); else EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        if (_fullscreen) return;
        _fullscreen = true;
        _beforeFullscreen = (WindowState, WindowStyle, ResizeMode, SidebarColumn.Width);
        Sidebar.Visibility = Visibility.Collapsed;
        Splitter.Visibility = Visibility.Collapsed;
        ControlBar.Visibility = Visibility.Collapsed;
        SidebarColumn.MinWidth = 0;
        SidebarColumn.Width = new GridLength(0);
        SplitterColumn.Width = new GridLength(0);
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
        WindowState = WindowState.Maximized;
        FullscreenButton.Content = IconExitFullscreen;
        ShowOsd("Pantalla completa · Esc para salir");
    }

    private void ExitFullscreen()
    {
        if (!_fullscreen) return;
        _fullscreen = false;
        WindowStyle = _beforeFullscreen.Style;
        ResizeMode = _beforeFullscreen.Resize;
        WindowState = _beforeFullscreen.State;
        SidebarColumn.Width = _beforeFullscreen.Sidebar;
        SidebarColumn.MinWidth = 260;
        SplitterColumn.Width = new GridLength(4);
        Sidebar.Visibility = Visibility.Visible;
        Splitter.Visibility = Visibility.Visible;
        ControlBar.Visibility = Visibility.Visible;
        FullscreenButton.Content = IconFullscreen;
    }

    // ---------- Eventos de la interfaz ----------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var typing = Keyboard.FocusedElement is TextBox;
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        switch (key)
        {
            case Key.F11:
            case Key.Enter when alt:
            case Key.F when !typing && !ctrl:
                ToggleFullscreen();
                break;
            case Key.Escape when _fullscreen:
                ExitFullscreen();
                break;
            case Key.F when ctrl:
                if (_fullscreen) ExitFullscreen();
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;
            case Key.F5:
                _ = _vm.RefreshAsync();
                break;
            case Key.MediaNextTrack:
            case Key.PageDown when !typing:
            case Key.Down when _fullscreen:
                _vm.Zap(+1);
                break;
            case Key.MediaPreviousTrack:
            case Key.PageUp when !typing:
            case Key.Up when _fullscreen:
                _vm.Zap(-1);
                break;
            case Key.MediaPlayPause:
            case Key.Space when !typing:
                TogglePause();
                break;
            case Key.MediaStop:
                _vm.Stop();
                break;
            case Key.M when !typing && !ctrl:
                ToggleMute();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleFullscreen();
        // Devuelve el foco a la ventana principal para que sigan funcionando los atajos.
        Dispatcher.BeginInvoke(() => Activate(), DispatcherPriority.Input);
    }

    private void OnChannelClicked(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null) return; // pulsó la estrella
        if (sender is ListBoxItem { DataContext: ChannelRow row }) _vm.Play(row.Channel);
    }

    private void OnChannelListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ChannelList.SelectedItem is ChannelRow row)
        {
            _vm.Play(row.Channel);
            e.Handled = true;
        }
    }

    private void OnToggleFavorite(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChannelRow row })
        {
            _vm.ToggleFavorite(row.Channel);
            ShowOsd(_vm.IsFavorite(row.Channel) ? $"★ {row.Name} añadido a favoritos" : $"{row.Name} quitado de favoritos");
        }
    }

    private async void OnPlaylistSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCombo) return;
        if (PlaylistCombo.SelectedItem is SavedPlaylist p && p.Id != _vm.ActiveId)
            await _vm.SelectPlaylistAsync(p.Id);
    }

    private void OnAddPlaylist(object sender, RoutedEventArgs e) => ShowAddPlaylistDialog();

    private void ShowAddPlaylistDialog()
    {
        var dialog = new AddPlaylistWindow(_vm) { Owner = this };
        dialog.ShowDialog();
        UpdatePlaceholder();
    }

    private void OnPlaylistMenu(object sender, RoutedEventArgs e)
    {
        PlaylistMenu.PlacementTarget = PlaylistMenuButton;
        PlaylistMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        PlaylistMenu.IsOpen = true;
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => _ = _vm.RefreshAsync();

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (_vm.Active is not { } p) return;
        var dialog = new InputDialog("Renombrar lista", "Nombre de la lista:", p.Name) { Owner = this };
        if (dialog.ShowDialog() == true) _vm.RenamePlaylist(p.Id, dialog.Value);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_vm.Active is not { } p) return;
        var answer = MessageBox.Show(this, $"¿Borrar la lista «{p.Name}»? Los canales de otras listas no se tocan.",
            "Borrar lista", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        if (_vm.Playing != null) _vm.Stop();
        await _vm.DeletePlaylistAsync(p.Id);
        UpdatePlaceholder();
        if (!_vm.HasPlaylists) ShowAddPlaylistDialog();
    }

    private void OnDismissError(object sender, RoutedEventArgs e) => _vm.DismissError();

    private void OnPrev(object sender, RoutedEventArgs e) => _vm.Zap(-1);
    private void OnNext(object sender, RoutedEventArgs e) => _vm.Zap(+1);
    private void OnPlayPause(object sender, RoutedEventArgs e) => TogglePause();
    private void OnStop(object sender, RoutedEventArgs e) => _vm.Stop();
    private void OnMute(object sender, RoutedEventArgs e) => ToggleMute();
    private void OnFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player == null) return; // durante InitializeComponent
        var volume = (int)e.NewValue;
        var unmute = _muted && volume > 0;
        if (unmute) { _muted = false; MuteButton.Content = IconVolume; }
        Task.Run(() =>
        {
            try { _player.Volume = volume; if (unmute) _player.Mute = false; } catch (Exception) { }
        });
    }

    private async void OnTracks(object sender, RoutedEventArgs e)
    {
        // Las pistas se leen en segundo plano para no bloquear la interfaz.
        var info = await Task.Run(() =>
        {
            try
            {
                return (Audio: _player.AudioTrackDescription.Select(t => (t.Id, t.Name)).ToList(), AudioId: _player.AudioTrack,
                        Subs: _player.SpuDescription.Select(t => (t.Id, t.Name)).ToList(), SubId: _player.Spu);
            }
            catch (Exception)
            {
                return (Audio: new List<(int, string)>(), AudioId: -1, Subs: new List<(int, string)>(), SubId: -1);
            }
        });

        var menu = new ContextMenu { PlacementTarget = TracksButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };

        var audio = new MenuItem { Header = "Audio" };
        foreach (var (id, name) in info.Audio)
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = info.AudioId == id };
            item.Click += (_, _) => EnqueuePlayer(() => _player.SetAudioTrack(id));
            audio.Items.Add(item);
        }
        audio.IsEnabled = audio.Items.Count > 0;
        menu.Items.Add(audio);

        var subs = new MenuItem { Header = "Subtítulos" };
        foreach (var (id, name) in info.Subs)
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = info.SubId == id };
            item.Click += (_, _) => EnqueuePlayer(() => _player.SetSpu(id));
            subs.Items.Add(item);
        }
        subs.IsEnabled = subs.Items.Count > 0;
        menu.Items.Add(subs);

        var aspect = new MenuItem { Header = "Formato de imagen" };
        foreach (var (label, value) in new[] { ("Automático", (string?)null), ("16:9", "16:9"), ("4:3", "4:3"), ("Rellenar ventana", "fill") })
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = CurrentAspect == value };
            item.Click += (_, _) => SetAspect(value);
            aspect.Items.Add(item);
        }
        menu.Items.Add(aspect);
        menu.IsOpen = true;
    }

    private string? CurrentAspect { get; set; }

    private void SetAspect(string? value)
    {
        CurrentAspect = value;
        var ratio = value;
        if (value == "fill")
        {
            var size = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var w = (int)(VideoView.ActualWidth * size.M11);
            var h = (int)(VideoView.ActualHeight * size.M22);
            ratio = w > 0 && h > 0 ? $"{w}:{h}" : null;
        }
        EnqueuePlayer(() => _player.AspectRatio = ratio);
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}
