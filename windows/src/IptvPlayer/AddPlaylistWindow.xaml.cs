using System.Windows;
using IptvPlayer.Core;
using Microsoft.Win32;

namespace IptvPlayer;

public partial class AddPlaylistWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _busy;

    public AddPlaylistWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Loaded += (_, _) => UrlBox.Focus();
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegir lista M3U",
            Filter = "Listas M3U (*.m3u;*.m3u8)|*.m3u;*.m3u8|Todos los archivos (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true) UrlBox.Text = dialog.FileName;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        PlaylistSource source;
        if (Tabs.SelectedItem == XtreamTab)
        {
            if (string.IsNullOrWhiteSpace(ServerBox.Text) || string.IsNullOrWhiteSpace(UserBox.Text) || PasswordBox.Password.Length == 0)
            {
                ShowError("Rellena el servidor, el usuario y la contraseña.");
                return;
            }
            source = new PlaylistSource.Xtream(ServerBox.Text, UserBox.Text, PasswordBox.Password);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(UrlBox.Text))
            {
                ShowError("Escribe la dirección de la lista o elige un archivo.");
                return;
            }
            source = new PlaylistSource.M3u(UrlBox.Text.Trim());
        }

        SetBusy(true);
        var error = await _vm.AddPlaylistAsync(NameBox.Text, source);
        SetBusy(false);
        if (error == null)
        {
            DialogResult = true;
            return;
        }
        ShowError($"No se pudo cargar la lista: {error}");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = CancelButton.IsEnabled = Tabs.IsEnabled = NameBox.IsEnabled = !busy;
        SaveButton.Content = busy ? "Cargando…" : "Guardar y cargar";
        if (busy) ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
