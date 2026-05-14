using System.IO;
using System.Windows;
using AgTarama.Services;

namespace AgTarama;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private CancellationTokenSource? _cts;

    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TxtMevcut.Text = $"v{UpdateService.CurrentVersion}";
        TxtYeni.Text   = $"v{_info.Version}";
        TxtNotlar.Text = string.IsNullOrWhiteSpace(_info.ReleaseNotes) ? "—" : _info.ReleaseNotes;

        if (_info.SizeBytes > 0)
            TxtIndirme.Text = $"İndiriliyor… ({_info.SizeBytes / 1024.0 / 1024.0:F1} MB)";
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnKapat_Click(object sender, RoutedEventArgs e) => Kapat();
    private void BtnSonra_Click(object sender, RoutedEventArgs e) => Kapat();

    private void Kapat()
    {
        _cts?.Cancel();
        Close();
    }

    private async void BtnIndir_Click(object sender, RoutedEventArgs e)
    {
        BtnIndir.IsEnabled = false;
        BtnSonra.IsEnabled = false;
        BtnKapat.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        var zipPath = Path.Combine(Path.GetTempPath(), "AgTaramaUpdate", $"AgTarama-v{_info.Version}.zip");
        _cts = new CancellationTokenSource();

        var progress = new Progress<int>(pct =>
        {
            TxtYuzde.Text      = $"{pct}%";
            ProgressFill.Width = ProgressPanel.ActualWidth * pct / 100.0;
        });

        try
        {
            TxtIndirme.Text = _info.SizeBytes > 0
                ? $"İndiriliyor… ({_info.SizeBytes / 1024.0 / 1024.0:F1} MB)"
                : "İndiriliyor…";

            await UpdateService.DownloadAsync(_info.DownloadUrl, zipPath, progress, _cts.Token);

            TxtIndirme.Text = "Kuruluyor…";
            TxtYuzde.Text   = "";

            UpdateService.ExtractAndRestart(zipPath); // uygulamayı kapatır
        }
        catch (OperationCanceledException)
        {
            // kullanıcı iptal etti
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Güncelleme hatası:\n{ex.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);

            BtnIndir.IsEnabled = true;
            BtnSonra.IsEnabled = true;
            BtnKapat.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }
}
