using System.Windows;
using AgTarama.Services;

namespace AgTarama;

public partial class App : Application
{
    private async void App_Startup(object sender, StartupEventArgs e)
    {
        // Önce yerel şifreli önbelleği kontrol et (hızlı, çevrimdışı da çalışır)
        var cached = LicenseService.CheckCache();
        if (cached?.Status == LicenseStatus.Valid)
        {
            var main = new MainWindow();
            main.Show();

            // Arka planda bulutla doğrula — sonuç geçersizse pencereyi kapat
            _ = ValidateInBackgroundAsync(main, cached.Info!.Key);
            return;
        }

        // Önbellekte geçerli lisans yok — lisans ekranını göster
        var licWin = new LicenseWindow();
        licWin.Show();
    }

    private static async Task ValidateInBackgroundAsync(Window mainWindow, string cachedKey)
    {
        await Task.Delay(3000); // Uygulama tam açılsın
        var result = await LicenseService.ValidateAsync(cachedKey);

        if (result.Status is LicenseStatus.Invalid or LicenseStatus.MachineConflict)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Lisans doğrulanamadı:\n{result.Message}\n\nUygulama kapatılıyor.",
                    "Lisans Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                LicenseService.ClearCache();
                mainWindow.Close();
                var licWin = new LicenseWindow();
                licWin.Show();
            });
        }
    }
}
