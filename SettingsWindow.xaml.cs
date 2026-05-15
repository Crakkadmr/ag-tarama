using System.Windows;
using AgTarama.Services;

namespace AgTarama;

public partial class SettingsWindow : Window
{
    public AppSettings Ayarlar { get; private set; }

    public SettingsWindow(AppSettings mevcutAyarlar)
    {
        InitializeComponent();
        Ayarlar = mevcutAyarlar;
        AyarlariDoldur();
    }

    private void AyarlariDoldur()
    {
        HedefMbBox.Text     = Ayarlar.HedefMB.ToString();
        TestSuresiBox.Text  = Ayarlar.TestSuresiSn.ToString();
        PingTimeoutBox.Text = Ayarlar.PingTimeoutMs.ToString();
        ConcurrencyBox.Text = Ayarlar.PortTaramaConcurrency.ToString();
        PortTimeoutBox.Text = Ayarlar.PortTaramaTimeoutMs.ToString();
        WlanRefreshBox.Text = Ayarlar.WlanAutoRefreshSeconds.ToString();
        SesAcikBox.IsChecked   = Ayarlar.SesAcik;
        ToastAcikBox.IsChecked = Ayarlar.ToastAcik;
    }

    private void KaydetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HedefMbBox.Text, out int hedefMb) || hedefMb < 1 || hedefMb > 2048) { Hata("Hedef MB: 1-2048 arasinda bir sayi girin."); return; }
        if (!int.TryParse(TestSuresiBox.Text, out int testSn) || testSn < 1 || testSn > 30) { Hata("Test suresi: 1-30 saniye arasinda girin."); return; }
        if (!int.TryParse(PingTimeoutBox.Text, out int pingMs) || pingMs < 100 || pingMs > 10000) { Hata("Ping timeout: 100-10000 ms arasinda girin."); return; }
        if (!int.TryParse(ConcurrencyBox.Text, out int conc) || conc < 1 || conc > 500) { Hata("Es zamanli limit: 1-500 arasinda girin."); return; }
        if (!int.TryParse(PortTimeoutBox.Text, out int portMs) || portMs < 100 || portMs > 10000) { Hata("Port timeout: 100-10000 ms arasinda girin."); return; }
        if (!int.TryParse(WlanRefreshBox.Text, out int wlanSn) || wlanSn < 5 || wlanSn > 300) { Hata("Wi-Fi yenileme: 5-300 saniye arasinda girin."); return; }

        Ayarlar = new AppSettings
        {
            HedefMB = hedefMb,
            TestSuresiSn = testSn,
            PingTimeoutMs = pingMs,
            PortTaramaConcurrency = conc,
            PortTaramaTimeoutMs = portMs,
            WlanAutoRefreshSeconds = wlanSn,
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
