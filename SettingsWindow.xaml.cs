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
        SesAcikBox.IsChecked   = Ayarlar.SesAcik;
        ToastAcikBox.IsChecked = Ayarlar.ToastAcik;
    }

    private void KaydetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HedefMbBox.Text,     out int hedefMb)   || hedefMb   < 1  || hedefMb   > 2048) { Hata("Hedef MB: 1–2048 arasında bir sayı girin."); return; }
        if (!int.TryParse(TestSuresiBox.Text,  out int testSn)    || testSn    < 1  || testSn    > 30)   { Hata("Test süresi: 1–30 saniye arasında girin."); return; }
        if (!int.TryParse(PingTimeoutBox.Text, out int pingMs)     || pingMs    < 100 || pingMs   > 10000) { Hata("Ping timeout: 100–10000 ms arasında girin."); return; }
        if (!int.TryParse(ConcurrencyBox.Text, out int conc)       || conc      < 1  || conc      > 500)  { Hata("Eş zamanlı limit: 1–500 arasında girin."); return; }
        if (!int.TryParse(PortTimeoutBox.Text, out int portMs)     || portMs    < 100 || portMs   > 10000) { Hata("Port timeout: 100–10000 ms arasında girin."); return; }

        Ayarlar = new AppSettings
        {
            HedefMB               = hedefMb,
            TestSuresiSn          = testSn,
            PingTimeoutMs         = pingMs,
            PortTaramaConcurrency = conc,
            PortTaramaTimeoutMs   = portMs,
            SesAcik               = SesAcikBox.IsChecked == true,
            ToastAcik             = ToastAcikBox.IsChecked == true,
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
        => MessageBox.Show(mesaj, "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
}
