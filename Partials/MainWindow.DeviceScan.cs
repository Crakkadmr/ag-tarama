using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;
using AgTarama.Services;
using AgTarama.Services.Ai;
using AgTarama.Services.Discovery;
using AgTarama.Services.Net;
using AgTarama.Services.Discovery.Classification;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

public partial class MainWindow
{
    // ─── 7. Cihaz Tarayıcı ───────────────────────────────────────────

    private sealed class CihazKimlik
    {
        public string  Marka   { get; set; } = "Bilinmiyor";
        public string? Model   { get; set; }
        public string  Tur     { get; set; } = "Cihaz";
        public string  TurIkon { get; set; } = "◈";
    }

    // ── Discovery engine ────────────────────────────────────────────────
    private readonly IDeviceDiscoveryEngine _engine = new DeviceDiscoveryEngine();

    // ── Kimlik sınıflandırması: ağırlıklı kanıt skoru ──
    private static CihazKimlik KimlikBelirle(DeviceInfo b) => KimlikBelirleV2(b);

    private static int GuvenSkoru(DeviceInfo b, CihazKimlik k)
    {
        if (b.KararIzi is { TurSiralama: { Count: > 0 } } iz)
        {
            int en = iz.TurSiralama[0].Skor;
            int markaBonus = b.KararIzi.MarkaSiralama.Count > 0
                ? Math.Min(15, b.KararIzi.MarkaSiralama[0].Skor / 4)
                : 0;
            return Math.Clamp(en + markaBonus, 0, 100);
        }
        int skor = 0;
        if (!string.IsNullOrWhiteSpace(b.UbntPlatform) || !string.IsNullOrWhiteSpace(b.UbntHostname)) skor += 35;
        if (!string.IsNullOrWhiteSpace(b.MikroTikBoard) || !string.IsNullOrWhiteSpace(b.MikroTikIdentity)) skor += 35;
        if (!string.IsNullOrWhiteSpace(b.HttpFpMarka)) skor += 30;
        if (!string.IsNullOrWhiteSpace(b.SnmpSysDescr)) skor += 25;
        if (b.OnvifBulundu) skor += 20;
        if (!string.IsNullOrEmpty(b.MdnsTur)) skor += 20;
        if (!string.IsNullOrWhiteSpace(b.WsdTipi)) skor += 15;
        if (b.SsdpBulundu) skor += 15;
        if (!string.IsNullOrWhiteSpace(b.NetbiosCihazAdi)) skor += 12;
        if (!string.IsNullOrWhiteSpace(b.MacAdresi) && !string.IsNullOrWhiteSpace(b.Uretici)) skor += 10;
        if (b.AcikPortlar.Count > 0) skor += Math.Min(10, b.AcikPortlar.Count * 2);
        if (k.Marka != "Bilinmiyor" && skor == 0) skor += 5;
        return Math.Min(100, skor);
    }

    private static string? CihazAdiSec(DeviceInfo b)
        => IlkDolu(
            b.NetbiosCihazAdi,
            b.SmbComputerName,
            KisaHostAdi(b.LlmnrHostname),
            KisaHostAdi(b.DnsAdi),
            KisaHostAdi(b.PingAdi),
            b.OnvifAdi,
            b.SsdpFriendlyName);

    private static string? IlkDolu(params string?[] degerler)
    {
        foreach (var deger in degerler)
        {
            var temiz = TemizKimlikMetni(deger);
            if (temiz != null) return temiz;
        }
        return null;
    }

    private static string? KisaHostAdi(string? ad)
    {
        var temiz = TemizKimlikMetni(ad);
        if (temiz == null) return null;
        var nokta = temiz.IndexOf('.');
        return nokta > 0 ? temiz[..nokta] : temiz;
    }

    private static string? AnlamliSayfaBasligi(string? baslik)
    {
        var temiz = TemizKimlikMetni(baslik);
        if (temiz == null) return null;
        var lower = temiz.ToLowerInvariant();
        if (lower is "login" or "index" or "web service" or "web service root" or "document") return null;
        if (lower.Contains("login page")) return null;
        return temiz;
    }

    private static string? TemizKimlikMetni(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        var temiz = WebUtility.HtmlDecode(metin).Trim();
        temiz = Regex.Replace(temiz, @"\s+", " ");
        temiz = temiz.Trim('-', '_', '.', ' ');
        return string.IsNullOrWhiteSpace(temiz) ? null : temiz;
    }

    private readonly ObservableCollection<KameraSatir> _kameraSatirlari = new();
    private readonly Dictionary<string, KameraSatir>   _kameraSatirlar  = new(StringComparer.Ordinal);
    private ICollectionView? _kameraSatirView;

    // ── 250ms UI throttle (UI thread'i her DeviceChanged'de boğmamak için) ──
    private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;
    private volatile bool _uiUpdatePending;
    private bool _dusukGuvenGoster;

    private void KameraPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _kameraCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void KameraKolonFiltre_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraIpFiltrePlaceholder.Visibility    = string.IsNullOrWhiteSpace(KameraIpFiltreBox.Text)    ? Visibility.Visible : Visibility.Collapsed;
        KameraAdFiltrePlaceholder.Visibility    = string.IsNullOrWhiteSpace(KameraAdFiltreBox.Text)    ? Visibility.Visible : Visibility.Collapsed;
        KameraMarkaFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraMarkaFiltreBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        KameraPortFiltrePlaceholder.Visibility  = string.IsNullOrWhiteSpace(KameraPortFiltreBox.Text)  ? Visibility.Visible : Visibility.Collapsed;
        KameraMacFiltrePlaceholder.Visibility   = string.IsNullOrWhiteSpace(KameraMacFiltreBox.Text)   ? Visibility.Visible : Visibility.Collapsed;
        KameraFiltreleriUygula();
    }

    private void KameraTurFiltreDegisti(object sender, SelectionChangedEventArgs e)
        => KameraFiltreleriUygula();

    private void KameraFiltreTemizle_Click(object sender, RoutedEventArgs e)
    {
        KameraIpFiltreBox.Clear();
        KameraAdFiltreBox.Clear();
        KameraMarkaFiltreBox.Clear();
        KameraPortFiltreBox.Clear();
        KameraMacFiltreBox.Clear();
        KameraTurFiltreBox.SelectedIndex = 0;
        KameraFiltreleriUygula();
    }

    private void KameraDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (KameraDataGrid.SelectedItem is not KameraSatir satir) return;
        KameraWebArayuzunuAc(satir);
    }

    private void KameraDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = UstOgeBul<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null) return;
        row.IsSelected = true;
        KameraDataGrid.SelectedItem = row.Item;
    }

    private void KameraMenuWeb_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is { } satir)
            KameraWebArayuzunuAc(satir);
    }

    private void KameraMenuYenidenTara_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        _ = TekIpTaraAsync(satir.Ip);
    }

    private async Task TekIpTaraAsync(string ip)
    {
        if (_kameraCts is { IsCancellationRequested: false })
        {
            ToastGoster("Devam eden tarama sırasında tekil tarama yapılamaz", hata: true);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = cts.Token;

        var bilgi = _engine.Store.GetOrAdd(ip);

        KameraIlerlemeText.Text = $"{ip} yeniden taranıyor…";

        try
        {
            var acik = new List<int>();
            foreach (var port in ScanOptions.DefaultPorts)
            {
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
                    linked.CancelAfter(800);
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(ip, port, linked.Token);
                    acik.Add(port);
                }
                catch { }
            }

            lock (bilgi.AcikPortlar)
            {
                bilgi.AcikPortlar.Clear();
                bilgi.AcikPortlar.AddRange(acik);
            }

            if (acik.Contains(554))
                bilgi.RtspDurum = await RtspHizliKontrol(ip, 554, token);

            await ServisDetaylariniGuncelleAsync(ip, bilgi, acik, token);

            foreach (var hp in new[] { 80, 8080, 8443, 443, 9000 })
            {
                if (!acik.Contains(hp)) continue;
                var (sunucu, baslik) = await HttpBannerOku(ip, hp, token);
                bilgi.SunucuBasligi = sunucu;
                bilgi.SayfaBasligi = baslik;
                break;
            }

            var httpFpPort = new[] { 80, 8080, 443, 8443 }.FirstOrDefault(p => acik.Contains(p));
            if (httpFpPort != 0)
            {
                var fp = await HttpFingerprintService.ProbeAsync(ip, httpFpPort, token);
                if (fp is not null)
                {
                    bilgi.HttpFpMarka = fp.Marka;
                    bilgi.HttpFpTur = fp.Tur;
                    bilgi.HttpFpModel = fp.Model;
                    bilgi.KesifKaynaklari.Add("HTTP-FP");
                }
            }

            var snmpDescr = await SnmpFingerprintService.SysDescrAsync(ip, token);
            if (!string.IsNullOrWhiteSpace(snmpDescr))
            {
                bilgi.SnmpSysDescr = snmpDescr;
                bilgi.KesifKaynaklari.Add("SNMP");
            }

            var netbiosDenenenler = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            var loglar = new ConcurrentBag<string>();
            using var netbiosSem = new SemaphoreSlim(1);
            await NetbiosBilgileriniGuncelleAsync(ip, bilgi, netbiosDenenenler, loglar, netbiosSem, token);

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    bilgi.PingYanit = true;
                    bilgi.PingMs    = (int)reply.RoundtripTime;
                    bilgi.PingTtl   = reply.Options?.Ttl ?? 0;
                    bilgi.Online    = true;
                }
            }
            catch { }

            bilgi.LastSeen = DateTime.Now;
            _engine.Store.NotifyChanged(bilgi);

            await Dispatcher.InvokeAsync(() =>
            {
                KameraKartEkleVeyaGuncelle(bilgi);
                KameraIlerlemeText.Text = $"{ip} yeniden tarandı";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            KameraIlerlemeText.Text = $"Hata: {ex.Message}";
        }
    }

    private void KameraMenuPing_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabPing;
        PingIpBox.Text = satir.Ip;
        _ = PingBaslat(satir.Ip);
    }

    private void KameraMenuPort_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabPort;
        PortIpBox.Text     = satir.Ip;
        PortAralikBox.Text = "21,22,23,53,80,139,443,445,554,8000,8080,8443,9000,34567,37777";
        _ = PortTaraBaslat(satir.Ip, PortScanService.Parse(PortAralikBox.Text));
    }

    private void KameraMenuTrace_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabTrace;
        TraceHedefBox.Text = satir.Ip;
        _ = TracerouteBaslat(satir.Ip);
    }

    private void KameraMenuDns_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        MainTabControl.SelectedIndex = TabDns;
        DnsHedefBox.Text = satir.Ip;
        _ = DnsLookupBaslat(satir.Ip);
    }

    private void KameraMenuIpKopyala_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        Clipboard.SetText(satir.Ip);
        ToastGoster($"IP kopyalandı: {satir.Ip}");
    }

    private void KameraMenuFavoriEkle_Click(object sender, RoutedEventArgs e)
    {
        if (SeciliKameraSatiri() is not { } satir) return;
        bool eklendi = FavoriService.Ekle(satir.Ip);
        FavoriChipleriniYenile();
        FavorilerPanelGuncelle();
        ToastGoster(eklendi ? $"★ Favoriye eklendi: {satir.Ip}" : $"Zaten favoride: {satir.Ip}", hata: !eklendi);
    }

    private KameraSatir? SeciliKameraSatiri()
        => KameraDataGrid.SelectedItem as KameraSatir;

    private void KameraWebArayuzunuAc(KameraSatir satir)
    {
        var url = string.IsNullOrWhiteSpace(satir.WebUrl) ? $"http://{satir.Ip}/" : satir.WebUrl;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static T? UstOgeBul<T>(DependencyObject? baslangic) where T : DependencyObject
    {
        while (baslangic is not null)
        {
            if (baslangic is T hedef) return hedef;
            baslangic = VisualTreeHelper.GetParent(baslangic);
        }
        return null;
    }

    private void KameraBaslatBtn_Click(object sender, RoutedEventArgs e) => _ = KameraTaramaBaslat();
    private void KameraDurdurBtn_Click(object sender, RoutedEventArgs e) => _kameraCts?.Cancel();

    private void KameraDusukGuvenCheck_Changed(object sender, RoutedEventArgs e)
    {
        _dusukGuvenGoster = KameraDusukGuvenCheck?.IsChecked == true;
        KameraFiltreleriUygula();
    }

    private void KameraAiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_kameraSatirlari.Count == 0)
        {
            ToastGoster("Analiz için önce cihaz taraması yapın.", hata: true);
            return;
        }

        if (!_ayarlar.AiEnabled)
        {
            ToastGoster("AI özellikleri Ayarlar > AI bölümünden kapalı.", hata: true);
            return;
        }

        var cihazlar = _kameraSatirlari
            .Select(s => new CihazDto(
                s.Ip, s.Ad, s.Tur, s.Marka, s.Model,
                s.Ping, s.Portlar, s.Kesif, s.Mac,
                s.Uretici, s.Servis, s.Guven))
            .ToList();

        var win = new AiDeviceReportWindow(cihazlar, _ayarlar, async ips =>
        {
            foreach (var ip in ips)
                await TekIpTaraAsync(ip);
        });
        win.Owner = this;
        win.Show();
    }

    private async Task NetbiosBilgileriniGuncelleAsync(
        string ip, DeviceInfo bilgi,
        ConcurrentDictionary<string, byte> denenenler,
        ConcurrentBag<string> logSatirlari,
        SemaphoreSlim netbiosSem,
        CancellationToken token)
    {
        if (!denenenler.TryAdd(ip, 0)) return;
        await netbiosSem.WaitAsync(token);
        try
        {
            var netbios = await NetbiosService.SorgulaAsync(ip, token);
            if (netbios is null) return;
            bilgi.NetbiosCihazAdi = netbios.NetbiosAdi;
            bilgi.NetbiosGrupAdi  = netbios.GrupAdi;
            bilgi.DnsAdi          = netbios.DnsAdi;
            bilgi.PingAdi         = netbios.PingAdi;
            bilgi.KesifKaynaklari.Add("NetBIOS");
        }
        finally { netbiosSem.Release(); }
    }

    private async Task KameraTaramaBaslat()
    {
        var giris = KameraSubnetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(giris))
        {
            var otomatik = YerelSubnetleriBul();
            giris = string.Join(",", otomatik);
            KameraSubnetBox.Text = giris;
        }

        var subnetler = CidrParser.Parse(giris);
        if (subnetler.Count == 0)
        {
            KameraKutucugaYaz("Gecerli subnet/CIDR bulunamadi. Ornek: 192.168.1 veya 192.168.1.0/24", "#F85149");
            return;
        }

        _kameraSatirlari.Clear();
        _kameraSatirlar.Clear();
        KameraFiltreSayacText.Text = "0 cihaz";
        KameraResultBorder.Visibility = Visibility.Visible;
        KameraIlerlemeText.Visibility = Visibility.Visible;
        KameraBaslatBtn.IsEnabled  = false;
        KameraDurdurBtn.Visibility = Visibility.Visible;
        KameraAiBtn.IsEnabled      = false;
        KameraIlerlemeText.Text    = "Baslatiliyor...";

        bool derinTara = KameraDerinTaraCheck?.IsChecked == true;
        bool liveMode  = KameraLiveCheck?.IsChecked == true;

        _kameraCts?.Cancel();
        _kameraCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = _kameraCts.Token;

        var options = new ScanOptions { DeepScan = derinTara, LiveMode = liveMode };
        var subnets = subnetler.Select(s => (s.Prefix, s.HostStart, s.HostEnd)).ToList<(string, int, int)>();

        KameraKutucugaYaz($"Hedef: {string.Join(", ", subnetler.Select(x => x.Cidr))}", "#8B949E");
        KameraKutucugaYaz(derinTara
            ? "Kaynak: Tüm protokoller (derin tarama aktif)"
            : "Kaynak: Standart protokoller", "#484F58");
        if (liveMode) KameraKutucugaYaz("Sürekli izleme modu — Durdur butonu ile durdurun", "#3FB950");
        KameraKutucugaYaz("─────────────────────────", "#30363D");

        _dusukGuvenGoster = KameraDusukGuvenCheck?.IsChecked == true;
        _engine.Store.DeviceChanged += OnEngineDeviceChanged;

        _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(250) };
        _uiUpdateTimer.Tick += (_, _) =>
        {
            if (_uiUpdatePending)
            {
                _uiUpdatePending = false;
                KameraFiltreleriUygula();
            }
        };
        _uiUpdateTimer.Start();

        var progress = new Progress<ScanProgress>(p =>
            Dispatcher.BeginInvoke(() =>
            {
                KameraIlerlemeText.Text    = p.AsamaMetni;
                KameraFiltreSayacText.Text = $"{p.BulunanCihaz} cihaz";
            }));

        try
        {
            if (liveMode)
            {
                await _engine.StartLiveAsync(subnets, options, token);
            }
            else
            {
                await _engine.StartScanAsync(subnets, options, progress, token);

                await Dispatcher.InvokeAsync(() =>
                {
                    foreach (var dev in _engine.Store.All)
                        KameraKartEkleVeyaGuncelle(dev);
                    KameraFiltreleriUygula();
                });
            }

            var sonuc = token.IsCancellationRequested
                ? $"Durduruldu — {_engine.Store.Count} cihaz"
                : $"Tamamlandı — {_engine.Store.Count} cihaz";
            KameraKutucugaYaz("─────────────────────────", "#30363D");
            KameraKutucugaYaz(sonuc, _engine.Store.Count > 0 ? "#3FB950" : "#D29922");
            KameraIlerlemeText.Text = sonuc;
            if (!token.IsCancellationRequested)
                ToastGoster($"Cihaz Tara tamamlandı — {_engine.Store.Count} cihaz bulundu");

            var cihazlar = KameraGorunenSatirlariAl();
            var hedefCidr = string.Join(",", subnetler.Select(x => x.Cidr));
            var loglar = new List<string>();
            LogService.Kaydet("CİHAZ TARA", hedefCidr, loglar);

            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("CİHAZ TARA", hedefCidr, sonuc, loglar,
                    new Dictionary<string, string>
                    {
                        ["Subnet"]      = string.Join(",", subnetler.Select(x => x.Prefix)),
                        ["SubnetInput"] = string.Join(",", subnetler.Select(x => x.Cidr)),
                        ["CihazSayisi"] = _engine.Store.Count.ToString(),
                        ["CihazlarJson"] = KameraJsonOlustur(cihazlar),
                    });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
        }
        catch (OperationCanceledException)
        {
            KameraIlerlemeText.Text = "Tarama durduruldu.";
        }
        catch (Exception ex)
        {
            KameraKutucugaYaz($"Hata: {ex.Message}", "#F85149");
        }
        finally
        {
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer = null;
            _engine.Store.DeviceChanged -= OnEngineDeviceChanged;
            KameraBaslatBtn.IsEnabled  = true;
            KameraDurdurBtn.Visibility = Visibility.Collapsed;
            KameraAiBtn.IsEnabled      = _kameraSatirlari.Count > 0;
        }
    }

    private void OnEngineDeviceChanged(object? sender, DeviceInfo dev)
    {
        // Güven eşiği kontrolü: düşük güven satırları toggle OFF iken ekleme
        if (!_dusukGuvenGoster)
        {
            var kim = KimlikBelirle(dev);
            if (GuvenSkoru(dev, kim) < 12 && !dev.Online) return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            KameraKartEkleGuncelleNoRefresh(dev);
            _uiUpdatePending = true;
        });
    }

    private void KameraKartEkleGuncelleNoRefresh(DeviceInfo bilgi)
    {
        var satir = KameraSatirOlustur(bilgi);
        if (_kameraSatirlar.TryGetValue(bilgi.Ip, out var mevcut))
            mevcut.Kopyala(satir);
        else
        {
            _kameraSatirlar[bilgi.Ip] = satir;
            _kameraSatirlari.Add(satir);
        }
    }

    private static async Task<(string? Sunucu, string? Baslik)> HttpBannerOku(string ip, int port, CancellationToken token)
    {
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2500);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2500;
            var req = Encoding.ASCII.GetBytes($"GET / HTTP/1.0\r\nHost: {ip}\r\nUser-Agent: AgTarama/1.0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(req, cts.Token);
            var buf = new byte[4096];
            int n   = await stream.ReadAsync(buf, cts.Token);
            var resp = Encoding.UTF8.GetString(buf, 0, n);
            if (!resp.StartsWith("HTTP/", StringComparison.Ordinal) ||
                !resp.Split('\n')[0].Contains("200", StringComparison.Ordinal))
                return (null, null);
            string? sunucu = null, baslik = null;
            foreach (var line in resp.Split('\n'))
            {
                var l = line.Trim();
                if (l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase))
                    sunucu = l[7..].Trim();
            }
            var tm = Regex.Match(resp, @"<title[^>]*>([^<]{1,80})</title>", RegexOptions.IgnoreCase);
            if (tm.Success) baslik = tm.Groups[1].Value.Trim();
            return (sunucu, baslik);
        }
        catch { return (null, null); }
    }

    private static async Task ServisDetaylariniGuncelleAsync(string ip, DeviceInfo bilgi, IEnumerable<int> acikPortlar, CancellationToken token)
    {
        foreach (var port in acikPortlar)
        {
            if (!BilindikPortlar.TryGetValue(port, out var servis)) servis = "Bilinmeyen";
            var banner = await PortBannerOku(ip, port, token);
            var detay  = banner == null ? servis : $"{servis} - {banner}";
            lock (bilgi.ServisDetaylari) bilgi.ServisDetaylari[port] = detay;
        }
    }

    private static async Task<string?> PortBannerOku(string ip, int port, CancellationToken token)
    {
        if (port is 80 or 8080 or 8000 or 9000)
        {
            var (sunucu, baslik) = await HttpBannerOku(ip, port, token);
            return IlkDolu(sunucu, AnlamliSayfaBasligi(baslik));
        }
        if (port == 554) return await RtspHizliKontrol(ip, port, token);
        if (port is 443 or 8443 or 445 or 3389) return null;
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(1200);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 1200;
            if (port is 23 or 21 or 22 or 25 or 110 or 143)
            {
                var buf = new byte[256];
                int n   = await stream.ReadAsync(buf, cts.Token);
                return BannerTemizle(Encoding.ASCII.GetString(buf, 0, n));
            }
        }
        catch { }
        return null;
    }

    private static string? BannerTemizle(string? banner)
    {
        if (string.IsNullOrWhiteSpace(banner)) return null;
        var temiz = Regex.Replace(banner, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", " ");
        temiz = Regex.Replace(temiz, @"\s+", " ").Trim();
        return temiz.Length > 90 ? temiz[..90] : temiz;
    }

    private static async Task<string?> RtspHizliKontrol(string ip, int port, CancellationToken token)
    {
        try
        {
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2000);
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2000;
            var req = Encoding.ASCII.GetBytes($"DESCRIBE rtsp://{ip}:{port}/ RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: AgTarama/1.0\r\n\r\n");
            await stream.WriteAsync(req, cts.Token);
            var buf = new byte[256];
            int n   = await stream.ReadAsync(buf, cts.Token);
            var first = Encoding.ASCII.GetString(buf, 0, n).Split('\n')[0].Trim();
            return first.Length > 9 ? first[9..] : first;
        }
        catch { return null; }
    }

    private void KameraKartEkleVeyaGuncelle(DeviceInfo bilgi)
    {
        var satir = KameraSatirOlustur(bilgi);
        if (_kameraSatirlar.TryGetValue(bilgi.Ip, out var mevcut))
            mevcut.Kopyala(satir);
        else
        {
            _kameraSatirlar[bilgi.Ip] = satir;
            _kameraSatirlari.Add(satir);
        }
        KameraFiltreleriUygula();
    }

}
