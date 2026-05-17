using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    private CancellationTokenSource? _wlanCts;
    private readonly ObservableCollection<WlanSatir> _wlanSatirlar = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _wlanBilinenBssid =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WlanSonuc> _sonWlanSonuclar = new();
    private DispatcherTimer? _wlanOtoTimer;
    private int _wlanSayac;
    private bool _wlanAdaptorVar;
    private bool _wlanTaramaDevamEdiyor;

    private void WlanPanelBaslat()
    {
        WlanGrid.ItemsSource = _wlanSatirlar;
        _wlanAdaptorVar = WlanService.WifiAdaptorVarMi();
        WlanSayacText.Text = "";
        WlanKanalOzetText.Text = "Kanal dagilimi hazir degil.";

        if (!_wlanAdaptorVar)
        {
            WlanTab.IsEnabled = false;
            WlanTab.ToolTip = "Bu cihazda Wi-Fi adaptoru bulunamadi.";
            WlanDurumText.Text = "Wi-Fi adaptoru bulunamadi, sekme devre disi.";
            return;
        }

        var interval = WlanYenilemeAraligiAl();
        WlanOtoYenileCheck.Content = $"Otomatik yenile ({interval}s)";
    }

    private async void WlanTaraBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_wlanAdaptorVar) return;
        await WlanTaramaBaslat();
    }

    private void WlanDurdurBtn_Click(object sender, RoutedEventArgs e)
    {
        _wlanCts?.Cancel();
        WlanOtoTimerDurdur();
        WlanOtoYenileCheck.IsChecked = false;
    }

    private void WlanOtoYenile_Changed(object sender, RoutedEventArgs e)
    {
        if (WlanOtoYenileCheck.IsChecked == true) WlanOtoTimerBaslat();
        else WlanOtoTimerDurdur();
    }

    private void WlanOtoTimerBaslat()
    {
        WlanOtoTimerDurdur();
        var interval = WlanYenilemeAraligiAl();
        _wlanSayac = interval;
        WlanOtoYenileCheck.Content = $"Otomatik yenile ({interval}s)";
        WlanSayacText.Text = $"(yenileme: {_wlanSayac}s)";

        _wlanOtoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _wlanOtoTimer.Tick += (_, _) =>
        {
            _wlanSayac--;
            if (_wlanSayac > 0)
            {
                WlanSayacText.Text = $"(yenileme: {_wlanSayac}s)";
                return;
            }

            if (_wlanTaramaDevamEdiyor)
            {
                _wlanSayac = 1;
                WlanSayacText.Text = "(tarama suruyor...)";
                return;
            }

            _wlanSayac = WlanYenilemeAraligiAl();
            WlanSayacText.Text = $"(yenileme: {_wlanSayac}s)";
            _ = WlanTaramaBaslat();
        };
        _wlanOtoTimer.Start();
    }

    private void WlanOtoTimerDurdur()
    {
        _wlanOtoTimer?.Stop();
        _wlanOtoTimer = null;
        WlanSayacText.Text = "";
    }

    private async Task WlanTaramaBaslat()
    {
        if (_wlanTaramaDevamEdiyor) return;

        _wlanTaramaDevamEdiyor = true;
        _wlanCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);

        WlanTaraBtn.IsEnabled = false;
        WlanDurdurBtn.IsEnabled = true;
        WlanDurumText.Text = "Taraniyor...";

        try
        {
            var sonuclar = await WlanService.ScanAsync(_wlanCts.Token);
            SupheliEvilTwinSinyalleriniGuncelle(sonuclar);

            _wlanSatirlar.Clear();
            foreach (var s in sonuclar.OrderByDescending(x => x.Signal))
                _wlanSatirlar.Add(new WlanSatir(s));

            _sonWlanSonuclar.Clear();
            _sonWlanSonuclar.AddRange(sonuclar);
            WlanKanalGrafikCiz(_sonWlanSonuclar);

            var supheli = sonuclar.Count(x => x.SupheliEvilTwin);
            var coklu = sonuclar.Count(x => x.CokluAp);
            WlanDurumText.Text = $"{sonuclar.Count} ag bulundu - {DateTime.Now:HH:mm:ss} | Coklu AP: {coklu}, Supheli: {supheli}";

            if (supheli > 0)
                ToastGoster($"{supheli} agda supheli Evil-Twin sinyali var.", hata: true);

            WlanTaramaGecmiseYaz(sonuclar, supheli, coklu);
        }
        catch (OperationCanceledException)
        {
            WlanDurumText.Text = "Tarama iptal edildi.";
        }
        catch (Exception ex)
        {
            WlanDurumText.Text = "Hata: " + ex.Message;
            ToastGoster("Wi-Fi tarama hatasi: " + ex.Message, hata: true);
        }
        finally
        {
            _wlanTaramaDevamEdiyor = false;
            WlanTaraBtn.IsEnabled = true;
            WlanDurdurBtn.IsEnabled = false;
        }
    }

    private void WlanKanalGrafikCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        => WlanKanalGrafikCiz(_sonWlanSonuclar);

    private void WlanKanalGrafikCiz(IReadOnlyList<WlanSonuc> sonuclar)
    {
        if (WlanKanalGrafikCanvas is null) return;

        WlanKanalGrafikCanvas.Children.Clear();

        var width = WlanKanalGrafikCanvas.ActualWidth;
        var height = WlanKanalGrafikCanvas.ActualHeight;
        if (width < 40 || height < 30 || sonuclar.Count == 0)
        {
            WlanKanalOzetText.Text = sonuclar.Count == 0
                ? "Kanal dagilimi icin once tarama yapin."
                : "Kanal grafigi olusturulamadi.";
            return;
        }

        var yogunluk24 = new Dictionary<int, int>();
        for (int ch = 1; ch <= 14; ch++) yogunluk24[ch] = 0;

        int ag5 = 0, ag6 = 0;
        foreach (var s in sonuclar)
        {
            if (s.Channel is >= 1 and <= 14) yogunluk24[s.Channel]++;
            else if (s.Channel >= 32 && s.Channel < 200) ag5++;
            else if (s.Channel >= 200) ag6++;
        }

        var maxDeger = Math.Max(1, yogunluk24.Values.Max());
        const double leftPad = 8;
        const double barGap = 3;
        var count = yogunluk24.Count;
        var usableWidth = Math.Max(1, width - leftPad * 2);
        var barWidth = Math.Max(3, (usableWidth - ((count - 1) * barGap)) / count);
        var usableHeight = Math.Max(20, height - 24);

        int idx = 0;
        foreach (var ch in yogunluk24.Keys.OrderBy(x => x))
        {
            var deger = yogunluk24[ch];
            var oran = deger / (double)maxDeger;
            var h = Math.Max(2, usableHeight * oran);
            var x = leftPad + idx * (barWidth + barGap);
            var y = usableHeight - h + 4;

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = h,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = KanalRenk(deger),
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            WlanKanalGrafikCanvas.Children.Add(rect);

            if (ch is 1 or 6 or 11 || deger == maxDeger)
            {
                var label = new TextBlock
                {
                    Text = ch.ToString(),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B949E")),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                };
                Canvas.SetLeft(label, x + 1);
                Canvas.SetTop(label, usableHeight + 6);
                WlanKanalGrafikCanvas.Children.Add(label);
            }

            idx++;
        }

        var enYogun = yogunluk24.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Take(3).ToList();
        var onerilen = new[] { 1, 6, 11 }.OrderBy(ch => yogunluk24[ch]).ThenBy(ch => ch).First();
        var yogunMetin = string.Join(", ", enYogun.Select(x => $"{x.Key} ({x.Value})"));
        WlanKanalOzetText.Text = $"2.4GHz yogun: {yogunMetin} | Oneri: kanal {onerilen} | 5GHz ag: {ag5} | 6GHz ag: {ag6}";
    }

    private static Brush KanalRenk(int deger)
    {
        var hex = deger switch
        {
            <= 0 => "#30363D",
            1 => "#238636",
            2 => "#2EA043",
            3 => "#D29922",
            _ => "#F85149",
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private void SupheliEvilTwinSinyalleriniGuncelle(List<WlanSonuc> sonuclar)
    {
        foreach (var grup in sonuclar.GroupBy(x => x.Ssid, StringComparer.OrdinalIgnoreCase))
        {
            var ssid = grup.Key;
            if (string.IsNullOrWhiteSpace(ssid)) continue;

            if (_wlanBilinenBssid.TryGetValue(ssid, out var bilinenler))
            {
                int sinyalEsigi = Math.Clamp(SettingsService.Yukle().EvilTwinSinyalEsigi, 50, 90);
                foreach (var s in grup)
                {
                    if (string.IsNullOrWhiteSpace(s.Bssid)) continue;
                    if (bilinenler.ContainsKey(s.Bssid)) continue;

                    SupheNedeniEkle(s, "Ayni SSID altinda beklenmeyen BSSID");
                    if (s.Signal >= sinyalEsigi)
                        SupheNedeniEkle(s, "Yuksek sinyal ile yeni BSSID");
                }
            }
        }

        foreach (var grup in sonuclar.GroupBy(x => x.Ssid, StringComparer.OrdinalIgnoreCase))
        {
            var ssid = grup.Key;
            if (string.IsNullOrWhiteSpace(ssid)) continue;

            var bssidler = _wlanBilinenBssid.GetOrAdd(ssid,
                _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

            foreach (var s in grup)
                if (!string.IsNullOrWhiteSpace(s.Bssid))
                    bssidler.TryAdd(s.Bssid, 0);
        }
    }

    private static void SupheNedeniEkle(WlanSonuc sonuc, string neden)
    {
        sonuc.SupheliEvilTwin = true;
        if (!sonuc.SupheNedenleri.Contains(neden, StringComparer.OrdinalIgnoreCase))
            sonuc.SupheNedenleri.Add(neden);
    }

    private int WlanYenilemeAraligiAl()
    {
        var sn = _ayarlar?.WlanAutoRefreshSeconds ?? 10;
        if (sn < 5) sn = 5;
        if (sn > 300) sn = 300;
        return sn;
    }

    private void WlanTaramaGecmiseYaz(List<WlanSonuc> sonuclar, int supheli, int coklu)
    {
        if (_gecmisdenCalistiriliyor)
        {
            _gecmisdenCalistiriliyor = false;
            return;
        }

        var satirlar = sonuclar
            .OrderByDescending(x => x.Signal)
            .Select(x => $"{x.Ssid} | {x.Bssid} | {x.Signal}% | Ch{x.Channel} | {x.Auth} | {x.Encryption} | {(x.SupheliEvilTwin ? "Supheli" : (x.CokluAp ? "Coklu AP" : "Normal"))}")
            .ToList();

        var payload = sonuclar.Select(x => new
        {
            SSID = x.Ssid,
            BSSID = x.Bssid,
            Sinyal = x.Signal,
            Kanal = x.Channel,
            Kimlik = x.Auth,
            Sifreleme = x.Encryption,
            EvilTwin = x.SupheliEvilTwin,
            Durum = x.SupheliEvilTwin ? "Supheli Evil-Twin" : (x.CokluAp ? "Coklu AP" : "Normal"),
            Nedenler = x.SupheNedenleri,
        }).ToList();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        HistoryService.Kaydet(
            "WIFI TARA",
            "Wi-Fi",
            $"{sonuclar.Count} ag bulundu, supheli: {supheli}, coklu AP: {coklu}",
            satirlar,
            new Dictionary<string, string>
            {
                ["AgiSayisi"] = sonuclar.Count.ToString(),
                ["SupheliSayisi"] = supheli.ToString(),
                ["CokluApSayisi"] = coklu.ToString(),
                ["WlanlarJson"] = json,
            });

        if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
    }
}

public sealed class WlanSatir
{
    public string Ssid { get; }
    public string Bssid { get; }
    public string Auth { get; }
    public string Encryption { get; }
    public int Signal { get; }
    public int Channel { get; }
    public string RadioType { get; }
    public string Band { get; }
    public bool CokluAp { get; }
    public bool SupheliEvilTwin { get; }
    public string DurumMetni { get; }
    public Brush DurumRenk { get; }

    public WlanSatir(WlanSonuc s)
    {
        Ssid = s.Ssid;
        Bssid = s.Bssid;
        Auth = s.Auth;
        Encryption = s.Encryption;
        Signal = s.Signal;
        Channel = s.Channel;
        RadioType = s.RadioType;
        Band = s.Band;
        CokluAp = s.CokluAp;
        SupheliEvilTwin = s.SupheliEvilTwin;

        if (s.SupheliEvilTwin)
        {
            var neden = s.SupheNedenleri.Count > 0 ? $" ({string.Join("; ", s.SupheNedenleri.Take(2))})" : "";
            DurumMetni = "Supheli Evil-Twin" + neden;
            DurumRenk = HexBrush("#F85149");
        }
        else if (s.CokluAp)
        {
            DurumMetni = "Coklu AP";
            DurumRenk = HexBrush("#E3B341");
        }
        else if (GuvenliMi(s))
        {
            DurumMetni = "Guvenli";
            DurumRenk = HexBrush("#3FB950");
        }
        else if (OrtaGuvenliMi(s))
        {
            DurumMetni = "Orta";
            DurumRenk = HexBrush("#E3B341");
        }
        else
        {
            DurumMetni = "Guvensiz";
            DurumRenk = HexBrush("#F85149");
        }
    }

    private static Brush HexBrush(string hex)
        => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private static bool GuvenliMi(WlanSonuc s)
        => s.Auth.Contains("WPA2", StringComparison.OrdinalIgnoreCase)
           || s.Auth.Contains("WPA3", StringComparison.OrdinalIgnoreCase)
           || s.Auth.Contains("WPA2-Personal", StringComparison.OrdinalIgnoreCase)
           || s.Auth.Contains("WPA2-Enterprise", StringComparison.OrdinalIgnoreCase);

    private static bool OrtaGuvenliMi(WlanSonuc s)
        => s.Auth.StartsWith("WPA", StringComparison.OrdinalIgnoreCase)
           && !s.Auth.Contains("WPA2", StringComparison.OrdinalIgnoreCase)
           && !s.Auth.Contains("WPA3", StringComparison.OrdinalIgnoreCase);
}
