using System.Windows;
using AgTarama.Services;

namespace AgTarama;

public partial class App : Application
{
    private async void App_Startup(object sender, StartupEventArgs e)
    {
        // Ortam güvenlik kontrolü — release build'de debugger/analiz aracı varsa sonlandırır
        SecurityService.Dogrula();

        // NTP öncelikli saat doğrulaması — sistem saati kullanılmaz
        var clockCheck = await TrustedTimeService.VerifyClockAsync();
        if (!clockCheck.Ok)
        {
            var kaynak = clockCheck.Source == ClockVerifySource.Ntp ? "NTP sunucusu" : "son kayıt";
            MessageBox.Show(
                $"Sistem saatiniz {kaynak} ile uyuşmuyor.\n\n" +
                $"{clockCheck.Detail}\n\n" +
                "Lütfen sistem saatinizi doğru değere güncelleyin ve uygulamayı yeniden başlatın.",
                "Sistem Saati Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        var cached = LicenseService.CheckCache();

        // 1) Cache geçerli + floor var → hızlı açılış, arka planda doğrula
        if (cached?.Status == LicenseStatus.Valid && TrustedTimeService.HasFloor())
        {
            var main = new MainWindow();
            main.Show();
            _ = ValidateInBackgroundAsync(main, cached.Info!.Key);
            _ = CheckForUpdateInBackgroundAsync(main);
            return;
        }

        // 2) Cache geçerli ama floor yok → ilk kurulum veya tg.dat silindi;
        //    saat manipülasyonu riski var, online doğrulama zorunlu
        if (cached?.Status == LicenseStatus.Valid && !TrustedTimeService.HasFloor())
        {
            var result = await LicenseService.ValidateAsync(cached.Info!.Key);
            if (result.Status == LicenseStatus.Valid)
            {
                new MainWindow().Show();
                return;
            }
        }

        // 3) Geçerli cache yok — lisans ekranı
        new LicenseWindow().Show();
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

    private static async Task ValidateInBackgroundAsync(MainWindow mainWindow, string cachedKey)
    {
        await Task.Delay(2000); // Uygulama tam açılsın
        var result = await LicenseService.ValidateAsync(cachedKey);

        await mainWindow.Dispatcher.InvokeAsync(async () =>
        {
            switch (result.Status)
            {
                case LicenseStatus.Valid:
                    break;

                case LicenseStatus.Expired:
                    MessageBox.Show(
                        $"Lisansınızın süresi doldu:\n{result.Message}\n\nUygulama kapatılıyor.",
                        "Lisans Süresi Doldu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LicenseService.ClearCache();
                    mainWindow.LisansIptalEt();
                    await Task.Delay(500);
                    mainWindow.Close();
                    new LicenseWindow().Show();
                    break;

                case LicenseStatus.Invalid:
                case LicenseStatus.MachineConflict:
                    MessageBox.Show(
                        $"Lisans doğrulanamadı:\n{result.Message}\n\nUygulama kapatılıyor.",
                        "Lisans Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LicenseService.ClearCache();
                    mainWindow.LisansIptalEt();
                    await Task.Delay(500);
                    mainWindow.Close();
                    new LicenseWindow().Show();
                    break;

                case LicenseStatus.NetworkError:
                    // Çevrimdışı — önbellekten devam
                    break;
            }
        });
    }
}
