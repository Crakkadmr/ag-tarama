using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class LicenseWindow : Window
{
    public bool LicenseAccepted { get; private set; }

    public LicenseWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TxtKey.Focus();
    }

    private void TxtKey_GotFocus(object sender, RoutedEventArgs e)
    {
        KeyBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58A6FF"));
    }

    private void TxtKey_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        TxtStatus.Text = string.Empty;
        TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E"));
        KeyBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30363D"));
    }

    private async void BtnActivate_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtKey.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            SetStatus("Lütfen lisans anahtarını girin.", false);
            return;
        }

        BtnActivate.IsEnabled = false;
        TxtKey.IsEnabled = false;
        SetStatus("Doğrulanıyor...", null);

        var result = await LicenseService.ValidateAsync(key);

        BtnActivate.IsEnabled = true;
        TxtKey.IsEnabled = true;

        switch (result.Status)
        {
            case LicenseStatus.Valid:
                SetStatus("✓ Lisans geçerli! Uygulama açılıyor...", true);
                LicenseAccepted = true;
                await Task.Delay(800);
                var main = new MainWindow();
                main.Show();
                Close();
                break;

            case LicenseStatus.MachineConflict:
                SetStatus(result.Message, false);
                KeyBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F85149"));
                break;

            case LicenseStatus.Expired:
                SetStatus(result.Message, false);
                break;

            case LicenseStatus.NetworkError:
                SetStatus($"Bağlantı hatası: {result.Message}", false);
                break;

            default:
                SetStatus(result.Message, false);
                KeyBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F85149"));
                break;
        }

        TxtKey.Focus();
    }

    private void SetStatus(string message, bool? success)
    {
        TxtStatus.Text = message;
        TxtStatus.Foreground = success switch
        {
            true  => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FB950")),
            false => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F85149")),
            null  => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E"))
        };
    }

    private void Contact_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Destek e-posta adresinizi buraya girin
        Process.Start(new ProcessStartInfo("mailto:destek@example.com") { UseShellExecute = true });
    }
}
