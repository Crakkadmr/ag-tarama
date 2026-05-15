using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Durum ─────────────────────────────────────────────────────────
    private CancellationTokenSource? _wlanCts;
    private readonly ObservableCollection<WlanSatir> _wlanSatirlar = new();
    private DispatcherTimer? _wlanOtoTimer;
    private int _wlanSayac = 0;
    private bool _wlanAdaptorVar = false;

    // ─── Başlatma (constructor'dan çağrılır) ──────────────────────────
    private void WlanPanelBaşlat()
    {
        WlanGrid.ItemsSource = _wlanSatirlar;
        _wlanAdaptorVar = WlanService.WifiAdaptorVarMi();

        if (!_wlanAdaptorVar)
        {
            WlanTab.IsEnabled = false;
            WlanTab.ToolTip   = "Bu cihazda Wi-Fi adaptörü bulunamadı.";
            WlanDurumText.Text = "Wi-Fi adaptörü bulunamadı — sekme devre dışı.";
        }
    }

    // ─── Tara butonu ───────────────────────────────────────────────────
    private async void WlanTaraBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_wlanAdaptorVar) return;
        await WlanTaramaBaslat();
    }

    // ─── Durdur butonu ─────────────────────────────────────────────────
    private void WlanDurdurBtn_Click(object sender, RoutedEventArgs e)
    {
        _wlanCts?.Cancel();
        WlanOtoTimerDurdur();
        WlanOtoYenileCheck.IsChecked = false;
    }

    // ─── Otomatik yenile checkbox ──────────────────────────────────────
    private void WlanOtoYenile_Changed(object sender, RoutedEventArgs e)
    {
        if (WlanOtoYenileCheck.IsChecked == true)
            WlanOtoTimerBaslat();
        else
            WlanOtoTimerDurdur();
    }

    // ─── Timer yönetimi ────────────────────────────────────────────────
    private void WlanOtoTimerBaslat()
    {
        WlanOtoTimerDurdur();
        _wlanSayac = 10;
        WlanSayacText.Text = $"(yenileme: {_wlanSayac}s)";

        _wlanOtoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wlanOtoTimer.Tick += async (_, _) =>
        {
            _wlanSayac--;
            WlanSayacText.Text = $"(yenileme: {_wlanSayac}s)";
            if (_wlanSayac <= 0)
            {
                _wlanSayac = 10;
                await WlanTaramaBaslat();
            }
        };
        _wlanOtoTimer.Start();
    }

    private void WlanOtoTimerDurdur()
    {
        _wlanOtoTimer?.Stop();
        _wlanOtoTimer  = null;
        WlanSayacText.Text = "";
    }

    // ─── Ana tarama metodu ─────────────────────────────────────────────
    private async Task WlanTaramaBaslat()
    {
        _wlanCts?.Cancel();
        _wlanCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);

        WlanTaraBtn.IsEnabled  = false;
        WlanDurdurBtn.IsEnabled = true;
        WlanDurumText.Text = "Taranıyor…";

        try
        {
            var sonuclar = await WlanService.ScanAsync(_wlanCts.Token);

            _wlanSatirlar.Clear();
            foreach (var s in sonuclar.OrderByDescending(x => x.Signal))
                _wlanSatirlar.Add(new WlanSatir(s));

            WlanDurumText.Text = $"{sonuclar.Count} ağ bulundu — {DateTime.Now:HH:mm:ss}";

            var evilCount = sonuclar.Count(x => x.EvilTwin);
            if (evilCount > 0)
                ToastGoster($"⚠ {evilCount} olası Evil-Twin tespit edildi!", hata: true);
        }
        catch (OperationCanceledException)
        {
            WlanDurumText.Text = "Tarama iptal edildi.";
        }
        catch (Exception ex)
        {
            WlanDurumText.Text = "Hata: " + ex.Message;
            ToastGoster("Wi-Fi tarama hatası: " + ex.Message, hata: true);
        }
        finally
        {
            WlanTaraBtn.IsEnabled   = true;
            WlanDurdurBtn.IsEnabled = false;
        }
    }
}

// ─── DataGrid görünüm modeli ────────────────────────────────────────────
public sealed class WlanSatir
{

    public string Ssid        { get; }
    public string Bssid       { get; }
    public string Auth        { get; }
    public string Encryption  { get; }
    public int    Signal      { get; }
    public int    Channel     { get; }
    public string RadioType   { get; }
    public bool   EvilTwin    { get; }

    // Hesaplanan durum sütunu
    public string DurumMetni { get; }
    public Brush  DurumRenk  { get; }

    public WlanSatir(WlanSonuc s)
    {
        Ssid       = s.Ssid;
        Bssid      = s.Bssid;
        Auth       = s.Auth;
        Encryption = s.Encryption;
        Signal     = s.Signal;
        Channel    = s.Channel;
        RadioType  = s.RadioType;
        EvilTwin   = s.EvilTwin;

        if (EvilTwin)
        {
            DurumMetni = "⚠ Evil-Twin";
            DurumRenk  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3B341"));
        }
        else if (GuvenliMi(s))
        {
            DurumMetni = "✓ Güvenli";
            DurumRenk  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FB950"));
        }
        else if (OrtaGüvenliMi(s))
        {
            DurumMetni = "⚠ Orta";
            DurumRenk  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3B341"));
        }
        else
        {
            DurumMetni = "✖ Güvensiz";
            DurumRenk  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F85149"));
        }
    }

    // WPA2 veya WPA3
    private static bool GuvenliMi(WlanSonuc s)
        => s.Auth.Contains("WPA2", StringComparison.OrdinalIgnoreCase)
        || s.Auth.Contains("WPA3", StringComparison.OrdinalIgnoreCase)
        || s.Auth.Contains("WPA2-Personal", StringComparison.OrdinalIgnoreCase)
        || s.Auth.Contains("WPA2-Enterprise", StringComparison.OrdinalIgnoreCase);

    // WPA (1. nesil)
    private static bool OrtaGüvenliMi(WlanSonuc s)
        => s.Auth.StartsWith("WPA", StringComparison.OrdinalIgnoreCase)
        && !s.Auth.Contains("WPA2", StringComparison.OrdinalIgnoreCase)
        && !s.Auth.Contains("WPA3", StringComparison.OrdinalIgnoreCase);
}
