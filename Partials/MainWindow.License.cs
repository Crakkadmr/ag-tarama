using System.Windows;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    private void LisansPanelGuncelle()
    {
        var cached = LicenseService.CheckCache();
        if (cached is null)
        {
            SetLisansUI(LicenseStatus.Invalid, "Aktif lisans bulunamadı.", null);
            return;
        }
        SetLisansUI(cached.Status, cached.Message, cached.Info);
    }

    private void SetLisansUI(LicenseStatus status, string mesaj, LicenseInfo? info)
    {
        // Durum kartı
        switch (status)
        {
            case LicenseStatus.Valid:
                LisansStatusIkon.Text     = "✓";
                LisansStatusBaslik.Text   = "Lisans Geçerli";
                LisansStatusIkon.Foreground    = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
                LisansStatusBaslik.Foreground  = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
                LisansStatusKart.BorderBrush   = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
                break;
            case LicenseStatus.Expired:
                LisansStatusIkon.Text     = "✕";
                LisansStatusBaslik.Text   = "Lisans Süresi Doldu";
                LisansStatusIkon.Foreground    = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
                LisansStatusBaslik.Foreground  = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
                LisansStatusKart.BorderBrush   = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
                break;
            default:
                LisansStatusIkon.Text     = "⚠";
                LisansStatusBaslik.Text   = "Lisans Geçersiz";
                LisansStatusIkon.Foreground    = new SolidColorBrush(Color.FromRgb(0xD2, 0x9A, 0x22));
                LisansStatusBaslik.Foreground  = new SolidColorBrush(Color.FromRgb(0xD2, 0x9A, 0x22));
                LisansStatusKart.BorderBrush   = new SolidColorBrush(Color.FromRgb(0xD2, 0x9A, 0x22));
                break;
        }
        LisansStatusAciklama.Text = mesaj;

        // Detay alanları
        if (info is not null)
        {
            LisansTurMetin.Text     = info.Type == "subscription" ? "Abonelik" : "Ömür Boyu";
            LisansAnahtarMetin.Text = MaskeLisansAnahtari(info.Key);

            if (info.Type == "subscription" && info.ExpiresAt.HasValue)
            {
                LisansKalanKart.Visibility = Visibility.Visible;
                var bitisUtc = info.ExpiresAt!.Value.Kind == DateTimeKind.Utc
                    ? info.ExpiresAt.Value
                    : info.ExpiresAt.Value.ToUniversalTime();
                var trustedNow = TrustedTimeService.GetTrustedNowSync();
                var kalan = bitisUtc - trustedNow;
                var bitis = bitisUtc.ToLocalTime();

                LisansBitisTarihi.Text = bitis.ToString("dd.MM.yyyy");

                if (kalan.TotalDays > 0)
                {
                    int gun  = (int)kalan.TotalDays;
                    int saat = kalan.Hours;

                    LisansKalanGun.Text      = gun > 0 ? $"{gun} gün" : $"{saat} saat";
                    LisansKalanAciklama.Text = bitis.ToString("dd MMMM yyyy HH:mm") + " tarihinde bitiyor";

                    var kalanRenk = gun < 7
                        ? Color.FromRgb(0xF8, 0x51, 0x49)
                        : gun < 30
                            ? Color.FromRgb(0xD2, 0x9A, 0x22)
                            : Color.FromRgb(0x58, 0xA6, 0xFF);
                    LisansKalanGun.Foreground   = new SolidColorBrush(kalanRenk);
                    LisansKalanKart.BorderBrush = new SolidColorBrush(kalanRenk);

                    // Sticky banner: 7 günden az kaldıysa göster
                    if (gun < 7 && !_lisansBannerGizle)
                    {
                        LisansBannerMetin.Text = gun == 0
                            ? $"Lisansınız bugün ({saat} saat içinde) sona eriyor!  [Yenile]"
                            : $"Lisansınız {gun} gün içinde sona eriyor.  [Yenile]";
                        LisansBanner.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    LisansKalanGun.Text         = "Süresi doldu";
                    LisansKalanAciklama.Text    = bitis.ToString("dd.MM.yyyy") + " tarihinde sona erdi";
                    LisansKalanGun.Foreground   = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
                    LisansKalanKart.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
                }
            }
            else
            {
                LisansKalanKart.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            LisansTurMetin.Text        = "—";
            LisansAnahtarMetin.Text    = "—";
            LisansKalanKart.Visibility = Visibility.Collapsed;
        }

        // Makine kimliği (ilk 8 karakter)
        var machineId = LicenseService.GetMachineId();
        LisansMakineMetin.Text = machineId[..Math.Min(8, machineId.Length)] + "…";

        // Son online doğrulama
        var lastValidation = LicenseService.GetLastValidationTime();
        LisansSonDogrulamaMetin.Text = lastValidation.HasValue
            ? lastValidation.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC"
            : "—";

        // NTP zamanı
        var ntpTime = TrustedTimeService.LastNtpTime;
        LisansNtpMetin.Text = ntpTime == DateTime.MinValue
            ? "NTP henüz sorgulanmadı"
            : ntpTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC";
    }

    private static string MaskeLisansAnahtari(string key)
    {
        if (key.Length <= 4) return key;
        var son4    = key[^4..];
        var parcalar = key.Split('-');
        if (parcalar.Length > 1)
        {
            var maskli = string.Join("-", parcalar[..^1].Select(p => new string('*', p.Length)));
            return $"{maskli}-{son4}";
        }
        return new string('*', key.Length - 4) + son4;
    }

    private async void LisansYenile_Click(object sender, RoutedEventArgs e)
    {
        var cached = LicenseService.CheckCache();
        if (cached?.Info is null)
        {
            ToastGoster("Önbellekte lisans anahtarı yok. Lisansı yeniden etkinleştirin.", hata: true);
            return;
        }

        ToastGoster("Sunucudan doğrulanıyor…");
        var sonuc = await LicenseService.ValidateAsync(cached.Info.Key);
        SetLisansUI(sonuc.Status, sonuc.Message, sonuc.Info);

        if (sonuc.Status == LicenseStatus.Valid)
            ToastGoster("Lisans başarıyla doğrulandı.");
        else
            ToastGoster(sonuc.Message, hata: true);
    }

    private void LisansSifirla_Click(object sender, RoutedEventArgs e)
    {
        var onay = MessageBox.Show(
            "Lisans önbelleği silinecek.\nUygulamayı bir sonraki açışta lisans anahtarı girilmesi gerekecek.\n\nDevam etmek istiyor musunuz?",
            "Lisansı Sıfırla", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (onay != MessageBoxResult.Yes) return;

        LicenseService.ClearCache();
        SetLisansUI(LicenseStatus.Invalid, "Lisans önbelleği silindi.", null);
        ToastGoster("Lisans önbelleği temizlendi.", hata: false);
    }

    private void LisansBannerKapat_Click(object sender, RoutedEventArgs e)
    {
        _lisansBannerGizle = true;
        LisansBanner.Visibility = Visibility.Collapsed;
    }

    private void LisansKopyala_Click(object sender, RoutedEventArgs e)
    {
        var cached = LicenseService.CheckCache();
        var machineId = LicenseService.GetMachineId();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Network Sniffer Lisans Bilgileri ===");
        sb.AppendLine($"Tarih      : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Durum      : {(cached?.Status.ToString() ?? "Bilinmiyor")}");
        sb.AppendLine($"Lisans Türü: {(cached?.Info?.Type ?? "—")}");
        sb.AppendLine($"Bitiş      : {(cached?.Info?.ExpiresAt?.ToString("yyyy-MM-dd") ?? "—")}");
        sb.AppendLine($"Makine ID  : {machineId[..Math.Min(16, machineId.Length)]}…");
        sb.AppendLine($"Son Doğrulama: {(LicenseService.GetLastValidationTime()?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "—")} UTC");
        sb.AppendLine($"NTP Zamanı : {(TrustedTimeService.LastNtpTime == DateTime.MinValue ? "—" : TrustedTimeService.LastNtpTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC")}");

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            ToastGoster("Lisans bilgileri panoya kopyalandı.");
        }
        catch
        {
            ToastGoster("Pano erişimi başarısız.", hata: true);
        }
    }
}
