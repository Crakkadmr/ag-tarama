using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AgTarama.Services;
using AgTarama.Services.Ai;

namespace AgTarama;

public partial class SettingsWindow : Window
{
    public AppSettings Ayarlar { get; private set; }
    private bool _saglayiciDoluyor;
    private CancellationTokenSource? _testCts;

    public SettingsWindow(AppSettings mevcutAyarlar)
    {
        InitializeComponent();
        Ayarlar = mevcutAyarlar;
        AyarlariDoldur();
    }

    private void AyarlariDoldur()
    {
        HedefMbBox.Text = Ayarlar.HedefMB.ToString();
        TestSuresiBox.Text = Ayarlar.TestSuresiSn.ToString();
        PingTimeoutBox.Text = Ayarlar.PingTimeoutMs.ToString();
        ConcurrencyBox.Text = Ayarlar.PortTaramaConcurrency.ToString();
        PortTimeoutBox.Text = Ayarlar.PortTaramaTimeoutMs.ToString();
        WlanRefreshBox.Text = Ayarlar.WlanAutoRefreshSeconds.ToString();
        SesAcikBox.IsChecked = Ayarlar.SesAcik;
        ToastAcikBox.IsChecked = Ayarlar.ToastAcik;

        AiSaglayiciDoldur();
        AiEnabledBox.IsChecked = Ayarlar.AiEnabled;
        AiBaseUrlBox.Text = Ayarlar.AiBaseUrl;
        AiModelBox.Text = Ayarlar.AiModel;
        AiGunlukLimitBox.Text = Ayarlar.AiGunlukTokenLimiti.ToString();
        AiAylikLimitBox.Text = Ayarlar.AiAylikTokenLimiti.ToString();
        AiIpMaskeleBox.IsChecked = Ayarlar.AiYerelIpMaskele;
        AiKeyDurumuYenile();
    }

    private void AiSaglayiciDoldur()
    {
        _saglayiciDoluyor = true;
        AiSaglayiciBox.Items.Clear();
        foreach (var preset in AiProvider.Presets)
            AiSaglayiciBox.Items.Add(new ComboBoxItem { Content = preset.DisplayName, Tag = preset.Id });

        for (int i = 0; i < AiSaglayiciBox.Items.Count; i++)
        {
            if (AiSaglayiciBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), Ayarlar.AiSaglayici, StringComparison.OrdinalIgnoreCase))
            {
                AiSaglayiciBox.SelectedIndex = i;
                break;
            }
        }
        if (AiSaglayiciBox.SelectedIndex < 0)
            AiSaglayiciBox.SelectedIndex = 0;
        _saglayiciDoluyor = false;
    }

    private void AiSaglayiciBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_saglayiciDoluyor) return;
        if (AiSaglayiciBox.SelectedItem is not ComboBoxItem item) return;
        var id = item.Tag?.ToString() ?? "";
        var preset = AiProvider.GetById(id);
        if (!string.Equals(id, AiProvider.Custom, StringComparison.OrdinalIgnoreCase))
        {
            AiBaseUrlBox.Text = preset.BaseUrl;
            AiModelBox.Text = preset.DefaultModel;
        }
    }

    private void AiKeyDurumuYenile()
    {
        AiKeyDurumText.Text = AiKeyVault.HasKey()
            ? "Kayıtlı anahtar var (vault). Değiştirmek için yeni anahtar yazıp Kaydet."
            : "Anahtar kayıtlı değil. Default anahtar otomatik yüklenir.";
    }

    private void AiKeySifirlaBtn_Click(object sender, RoutedEventArgs e)
    {
        var sonuc = MessageBox.Show(
            "Kayıtlı API anahtarı silinecek. Devam edilsin mi?",
            "AI Anahtarını Sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (sonuc != MessageBoxResult.Yes) return;

        AiKeyVault.Clear();
        AiApiKeyBox.Clear();
        AiKeyDurumuYenile();
        AiTestSonucText.Text = "Anahtar sıfırlandı.";
    }

    private async void AiTestBtn_Click(object sender, RoutedEventArgs e)
    {
        _testCts?.Cancel();
        _testCts?.Dispose();
        _testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var gecici = new AppSettings
        {
            AiEnabled = true,
            AiSaglayici = (AiSaglayiciBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? Ayarlar.AiSaglayici,
            AiBaseUrl = AiBaseUrlBox.Text.Trim(),
            AiModel = AiModelBox.Text.Trim(),
            AiGunlukTokenLimiti = Ayarlar.AiGunlukTokenLimiti,
            AiAylikTokenLimiti = Ayarlar.AiAylikTokenLimiti
        };

        var girilenKey = AiApiKeyBox.Password;
        AiTestBtn.IsEnabled = false;
        AiTestSonucText.Foreground = System.Windows.Media.Brushes.Gray;
        AiTestSonucText.Text = "Test ediliyor...";

        try
        {
            var sonuc = await AiClient.TestConnectionAsync(gecici, girilenKey, _testCts.Token);
            if (sonuc.Success)
            {
                AiTestSonucText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(63, 185, 80));
                AiTestSonucText.Text = $"✓ {sonuc.Message} ({sonuc.LatencyMs} ms)";
            }
            else
            {
                AiTestSonucText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(248, 81, 73));
                AiTestSonucText.Text = $"✗ {sonuc.Message}";
            }
        }
        catch (OperationCanceledException)
        {
            AiTestSonucText.Text = "Test iptal edildi / zaman aşımı.";
        }
        catch (Exception ex)
        {
            AiTestSonucText.Text = "Test hatası: " + ex.Message;
        }
        finally
        {
            AiTestBtn.IsEnabled = true;
        }
    }

    private void KaydetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HedefMbBox.Text, out int hedefMb) || hedefMb < 1 || hedefMb > 2048) { Hata("Hedef MB: 1-2048 arasinda bir sayi girin."); return; }
        if (!int.TryParse(TestSuresiBox.Text, out int testSn) || testSn < 1 || testSn > 30) { Hata("Test suresi: 1-30 saniye arasinda girin."); return; }
        if (!int.TryParse(PingTimeoutBox.Text, out int pingMs) || pingMs < 100 || pingMs > 10000) { Hata("Ping timeout: 100-10000 ms arasinda girin."); return; }
        if (!int.TryParse(ConcurrencyBox.Text, out int conc) || conc < 1 || conc > 500) { Hata("Es zamanli limit: 1-500 arasinda girin."); return; }
        if (!int.TryParse(PortTimeoutBox.Text, out int portMs) || portMs < 100 || portMs > 10000) { Hata("Port timeout: 100-10000 ms arasinda girin."); return; }
        if (!int.TryParse(WlanRefreshBox.Text, out int wlanSn) || wlanSn < 5 || wlanSn > 300) { Hata("Wi-Fi yenileme: 5-300 saniye arasinda girin."); return; }
        if (!int.TryParse(AiGunlukLimitBox.Text, out int aiGunluk) || aiGunluk < 0 || aiGunluk > 100_000_000) { Hata("AI günlük limit: 0-100000000 arasinda girin."); return; }
        if (!int.TryParse(AiAylikLimitBox.Text, out int aiAylik) || aiAylik < 0 || aiAylik > 1_000_000_000) { Hata("AI aylık limit: 0-1000000000 arasinda girin."); return; }

        var saglayiciId = (AiSaglayiciBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? Ayarlar.AiSaglayici;
        var baseUrl = AiBaseUrlBox.Text.Trim();
        var model = AiModelBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl)) { Hata("AI base URL boş olamaz."); return; }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            !baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        { Hata("AI base URL geçersiz veya HTTPS değil. Örnek: https://openrouter.ai/api/v1"); return; }
        if (string.IsNullOrWhiteSpace(model)) { Hata("AI model boş olamaz."); return; }

        var girilenKey = AiApiKeyBox.Password;
        if (!string.IsNullOrWhiteSpace(girilenKey))
        {
            try
            {
                AiKeyVault.Save(girilenKey);
            }
            catch (Exception ex)
            {
                Hata("AI anahtarı kaydedilemedi: " + ex.Message);
                return;
            }
        }

        Ayarlar = new AppSettings
        {
            HedefMB = hedefMb,
            TestSuresiSn = testSn,
            PingTimeoutMs = pingMs,
            PortTaramaConcurrency = conc,
            PortTaramaTimeoutMs = portMs,
            WlanAutoRefreshSeconds = wlanSn,
            EvilTwinSinyalEsigi = Ayarlar.EvilTwinSinyalEsigi,
            AiEnabled = AiEnabledBox.IsChecked == true,
            AiSaglayici = saglayiciId,
            AiBaseUrl = baseUrl,
            AiModel = model,
            AiGunlukTokenLimiti = aiGunluk,
            AiAylikTokenLimiti = aiAylik,
            AiYerelIpMaskele = AiIpMaskeleBox.IsChecked == true,
            SesAcik = SesAcikBox.IsChecked == true,
            ToastAcik = ToastAcikBox.IsChecked == true,
        };

        SettingsService.Kaydet(Ayarlar);
        DialogResult = true;
        Close();
    }

    private void IptalBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Hata(string mesaj)
        => MessageBox.Show(mesaj, "Gecersiz Deger", MessageBoxButton.OK, MessageBoxImage.Warning);
}
