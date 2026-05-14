using System.Windows;
using AgTarama.Services;
using System.Threading.Tasks;

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
            _ = CheckForUpdateInBackgroundAsync(main);
            return;
        }

        // Önbellekte geçerli lisans yok — lisans ekranını göster
        var licWin = new LicenseWindow();
        licWin.Show();
    }

    private static async Task CheckForUpdateInBackgroundAsync(Window mainWindow)
    {
        await Task.Delay(4000); // Uygulama tam yüklensin
        var update = await UpdateService.CheckForUpdateAsync();
        if (update is null) return;

        mainWindow.Dispatcher.Invoke(() =>
        {
            var win = new UpdateWindow(update) { Owner = mainWindow };
            win.Show();
        });
    }

    private static async Task ValidateInBackgroundAsync(Window mainWindow, string cachedKey)
    {
        await Task.Delay(2000); // Uygulama tam açılsın
        var result = await LicenseService.ValidateAsync(cachedKey);

        mainWindow.Dispatcher.Invoke(() =>
        {
            switch (result.Status)
            {
                case LicenseStatus.Valid:
                    // Geçerli — MainWindow'daki lisans sekmesi zaten önbellekten okuyor, ek işlem yok
                    break;

                case LicenseStatus.Expired:
                    MessageBox.Show(
                        $"Lisansınızın süresi doldu:\n{result.Message}\n\nUygulama kapatılıyor.",
                        "Lisans Süresi Doldu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LicenseService.ClearCache();
                    mainWindow.Close();
                    new LicenseWindow().Show();
                    break;

                case LicenseStatus.Invalid:
                case LicenseStatus.MachineConflict:
                    MessageBox.Show(
                        $"Lisans doğrulanamadı:\n{result.Message}\n\nUygulama kapatılıyor.",
                        "Lisans Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LicenseService.ClearCache();
                    mainWindow.Close();
                    new LicenseWindow().Show();
                    break;

                case LicenseStatus.NetworkError:
                    // Çevrimdışı — önbellekten devam, kullanıcıya sessizce bilgi ver
                    // (Toast için MainWindow referansı gerekir; ileride geliştirilebilir)
                    break;
            }
        });
    }
}
