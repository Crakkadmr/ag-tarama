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

    private sealed class TaramaSubneti
    {
        public string Prefix { get; init; } = "";
        public int HostStart { get; init; } = 1;
        public int HostEnd { get; init; } = 254;
        public string OriginalCidr { get; init; } = "";
        public string Cidr => string.IsNullOrEmpty(OriginalCidr)
            ? $"{Prefix}.0/24"
            : OriginalCidr;
        public int HostCount => HostEnd >= HostStart ? HostEnd - HostStart + 1 : 0;
    }

    private static List<string> YerelSubnetleriBul()
    {
        var sonuc = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))
                    sonuc.Add($"{b[0]}.{b[1]}.{b[2]}");
            }
        }
        return sonuc.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private sealed record NicSubneti(string Prefix, string NicAdi, string Tip, long Hiz);

    private static List<NicSubneti> YerelNicSubnetleriniBul()
    {
        var sonuc = new Dictionary<string, NicSubneti>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (!(b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))) continue;
                var prefix = $"{b[0]}.{b[1]}.{b[2]}";
                if (sonuc.ContainsKey(prefix)) continue;
                var tip = ni.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                    NetworkInterfaceType.Ethernet      => "Ethernet",
                    NetworkInterfaceType.GigabitEthernet => "Ethernet",
                    _ => ni.NetworkInterfaceType.ToString(),
                };
                sonuc[prefix] = new NicSubneti(prefix, ni.Name, tip, ni.Speed);
            }
        }
        return sonuc.Values.OrderBy(x => x.Prefix, StringComparer.Ordinal).ToList();
    }

    private static string? YerelSubnetiBul()
        => YerelSubnetleriBul().FirstOrDefault();

    private static bool SanalAdaptorMu(NetworkInterface ni)
    {
        var ad = $"{ni.Name} {ni.Description}".ToLowerInvariant();
        return ad.Contains("virtual")
            || ad.Contains("vmware")
            || ad.Contains("hyper-v")
            || ad.Contains("vbox")
            || ad.Contains("vpn")
            || ad.Contains("wireguard")
            || ad.Contains("loopback")
            || ad.Contains("tunnel")
            || ad.Contains("tap")
            || ad.Contains("miniport");
    }

    private static List<TaramaSubneti> SubnetGirdisiniCoz(string giris)
    {
        var list = new List<TaramaSubneti>();
        var tekiller = new HashSet<string>(StringComparer.Ordinal);
        var parcalar = giris.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var parca in parcalar)
        {
            var token = parca.Trim();

            var cidr = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})/(?<m>\d{1,2})$");
            if (cidr.Success)
            {
                if (!int.TryParse(cidr.Groups["a"].Value, out var a) || a is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["b"].Value, out var b) || b is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["c"].Value, out var c) || c is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["d"].Value, out var d) || d is < 0 or > 255) continue;
                if (!int.TryParse(cidr.Groups["m"].Value, out var mask) || mask is < 16 or > 32) continue;
                CidrAraligaCoz(a, b, c, d, mask, token, list, tekiller);
                continue;
            }

            var p3 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})$");
            var p4 = Regex.Match(token, @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})$");
            string? prefix = null;
            if (p3.Success)
                prefix = $"{p3.Groups["a"].Value}.{p3.Groups["b"].Value}.{p3.Groups["c"].Value}";
            else if (p4.Success)
                prefix = $"{p4.Groups["a"].Value}.{p4.Groups["b"].Value}.{p4.Groups["c"].Value}";

            if (prefix is null) continue;

            var oktetler = prefix.Split('.');
            if (oktetler.Length != 3) continue;
            if (!oktetler.All(x => int.TryParse(x, out var n) && n is >= 0 and <= 255)) continue;

            var key = $"{prefix}|1-254";
            if (tekiller.Add(key))
                list.Add(new TaramaSubneti { Prefix = prefix, HostStart = 1, HostEnd = 254 });
        }

        return list;
    }

    private static void CidrAraligaCoz(int a, int b, int c, int d, int mask, string orijinal,
                                       List<TaramaSubneti> list, HashSet<string> tekiller)
    {
        uint ipUint = ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | (uint)d;
        uint maskUint = mask == 0 ? 0u : 0xFFFFFFFFu << (32 - mask);
        uint network = ipUint & maskUint;
        uint broadcast = network | ~maskUint;

        if (mask >= 24)
        {
            int ho1 = (int)((network >> 24) & 0xFF);
            int ho2 = (int)((network >> 16) & 0xFF);
            int ho3 = (int)((network >> 8) & 0xFF);
            var prefix = $"{ho1}.{ho2}.{ho3}";

            int hostStart, hostEnd;
            if (mask == 24)
            {
                hostStart = 1;
                hostEnd = 254;
            }
            else if (mask == 31)
            {
                hostStart = (int)(network & 0xFF);
                hostEnd = (int)(broadcast & 0xFF);
            }
            else if (mask == 32)
            {
                hostStart = (int)(network & 0xFF);
                hostEnd = hostStart;
            }
            else
            {
                hostStart = (int)((network & 0xFF) + 1);
                hostEnd = (int)((broadcast & 0xFF) - 1);
            }

            if (hostEnd < hostStart) return;
            var key = $"{prefix}|{hostStart}-{hostEnd}";
            if (tekiller.Add(key))
                list.Add(new TaramaSubneti
                {
                    Prefix = prefix,
                    HostStart = hostStart,
                    HostEnd = hostEnd,
                    OriginalCidr = orijinal,
                });
        }
        else
        {
            ulong toplam = (ulong)broadcast - network + 1ul;
            ulong adetCidr = toplam / 256ul;
            if (adetCidr > 256) return;

            for (uint cur = network; cur <= broadcast && cur >= network; cur += 256)
            {
                int o1 = (int)((cur >> 24) & 0xFF);
                int o2 = (int)((cur >> 16) & 0xFF);
                int o3 = (int)((cur >> 8) & 0xFF);
                var p = $"{o1}.{o2}.{o3}";
                var key = $"{p}|1-254";
                if (tekiller.Add(key))
                    list.Add(new TaramaSubneti
                    {
                        Prefix = p,
                        HostStart = 1,
                        HostEnd = 254,
                        OriginalCidr = orijinal,
                    });
                if (cur > 0xFFFFFF00u) break;
            }
        }
    }

    private readonly ObservableCollection<KameraSatir> _kameraSatirlari = new();
    private readonly Dictionary<string, KameraSatir>   _kameraSatirlar  = new(StringComparer.Ordinal);
    private ICollectionView? _kameraSatirView;

    // ── 250ms UI throttle (UI thread'i her DeviceChanged'de boğmamak için) ──
    private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;
    private volatile bool _uiUpdatePending;
    private bool _dusukGuvenGoster;

    private bool _subnetBoxChipSenkronu;

    private void BtnKamera_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabCihazTara;
        if (KameraSubnetChips.Children.Count == 0)
            KameraNicChipleriniYenile(seciliVarsayilan: true);
    }

    private void KameraNicYenileBtn_Click(object sender, RoutedEventArgs e)
        => KameraNicChipleriniYenile(seciliVarsayilan: false);

    public void KameraNicChipleriniYenile(bool seciliVarsayilan)
    {
        var mevcutSecili = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.Ordinal);

        KameraSubnetChips.Children.Clear();

        var nicler = YerelNicSubnetleriniBul();
        if (nicler.Count == 0)
        {
            var bos = new TextBlock
            {
                Text = "Aktif ağ arayüzü bulunamadı",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(2, 6, 0, 0),
            };
            KameraSubnetChips.Children.Add(bos);
            return;
        }

        for (int i = 0; i < nicler.Count; i++)
        {
            var nic = nicler[i];
            var chip = KameraChipOlustur(nic);
            bool secili = mevcutSecili.Contains(nic.Prefix) ||
                          (mevcutSecili.Count == 0 && seciliVarsayilan && i == 0);
            chip.IsChecked = secili;
            KameraSubnetChips.Children.Add(chip);
        }

        KameraChipleriSenkronizeEt();
    }

    private System.Windows.Controls.Primitives.ToggleButton KameraChipOlustur(NicSubneti nic)
    {
        var icerik = new StackPanel { Orientation = Orientation.Horizontal };
        icerik.Children.Add(new TextBlock
        {
            Text = $"{nic.Prefix}.0/24",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        icerik.Children.Add(new TextBlock
        {
            Text = $"  ({nic.Tip})",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var chip = new System.Windows.Controls.Primitives.ToggleButton
        {
            Tag = nic.Prefix,
            Content = icerik,
            Style = (Style)FindResource("DarkChip"),
            ToolTip = $"{nic.NicAdi}\nTür: {nic.Tip}" +
                      (nic.Hiz > 0 ? $"\nHız: {nic.Hiz / 1_000_000} Mbps" : ""),
        };
        chip.Checked += KameraChipDegisti;
        chip.Unchecked += KameraChipDegisti;
        return chip;
    }

    private void KameraChipDegisti(object sender, RoutedEventArgs e)
        => KameraChipleriSenkronizeEt();

    private void KameraChipleriSenkronizeEt()
    {
        var prefixler = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        if (prefixler.Count == 0) return;
        _subnetBoxChipSenkronu = true;
        try { KameraSubnetBox.Text = string.Join(",", prefixler); }
        finally { _subnetBoxChipSenkronu = false; }
    }

    private void KameraPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _kameraCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void KameraSubnetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraSubnetPlaceholder.Visibility = string.IsNullOrEmpty(KameraSubnetBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (_subnetBoxChipSenkronu) return;

        var metin = KameraSubnetBox.Text ?? "";
        foreach (var tb in KameraSubnetChips.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>())
        {
            if (tb.IsChecked == true && tb.Tag is string p && !metin.Contains(p, StringComparison.Ordinal))
                tb.IsChecked = false;
        }
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

    private void KameraExportExcel_Click(object sender, RoutedEventArgs e) => KameraDisariAktar(KameraExportFormat.Excel);
    private void KameraExportPdf_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Pdf);
    private void KameraExportTxt_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Txt);
    private void KameraExportCsv_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Csv);
    private void KameraExportJson_Click(object sender, RoutedEventArgs e)  => KameraDisariAktar(KameraExportFormat.Json);

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

    private enum KameraExportFormat { Excel, Pdf, Txt, Csv, Json }

    private void KameraDisariAktar(KameraExportFormat format)
    {
        var satirlar = KameraGorunenSatirlariAl();
        if (satirlar.Count == 0)
        {
            ToastGoster("Dışa aktarılacak cihaz yok", hata: true);
            return;
        }

        var (filter, ext) = format switch
        {
            KameraExportFormat.Excel => ("Excel Dosyası (*.xlsx)|*.xlsx", "xlsx"),
            KameraExportFormat.Pdf   => ("PDF Raporu (*.pdf)|*.pdf", "pdf"),
            KameraExportFormat.Txt   => ("Metin Raporu (*.txt)|*.txt", "txt"),
            KameraExportFormat.Csv   => ("CSV Dosyası (*.csv)|*.csv", "csv"),
            KameraExportFormat.Json  => ("JSON Dosyası (*.json)|*.json", "json"),
            _                        => ("Rapor (*.*)|*.*", "txt"),
        };

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "Cihaz Tara Sonuçlarını Dışa Aktar",
            Filter      = filter,
            DefaultExt  = ext,
            AddExtension = true,
            FileName    = $"Cihaz_Tara_Raporu_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            switch (format)
            {
                case KameraExportFormat.Excel:
                    File.WriteAllBytes(dlg.FileName, KameraExcelXlsxOlustur(satirlar));
                    break;
                case KameraExportFormat.Pdf:
                    File.WriteAllBytes(dlg.FileName, KameraPdfQuestOlustur(satirlar));
                    break;
                case KameraExportFormat.Txt:
                    File.WriteAllText(dlg.FileName, KameraTxtOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Csv:
                    File.WriteAllText(dlg.FileName, KameraCsvOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Json:
                    File.WriteAllText(dlg.FileName, KameraJsonOlustur(satirlar), new UTF8Encoding(true));
                    break;
            }

            MesajEkle("sonuc", $"✔ Cihaz Tara raporu kaydedildi: {Path.GetFileName(dlg.FileName)}");
            ToastGoster($"Dışa aktarıldı: {Path.GetFileName(dlg.FileName)}");
            DisariAktarilanDosyayiAc(dlg.FileName);
        }
        catch (Exception ex)
        {
            HataBildir("Cihaz Tara dışa aktarma hatası", ex);
        }
    }

    private void DisariAktarilanDosyayiAc(string dosyaYolu)
    {
        try { Process.Start(new ProcessStartInfo(dosyaYolu) { UseShellExecute = true }); }
        catch (Exception ex) { HataBildir("Rapor kaydedildi ancak dosya otomatik açılamadı", ex); }
    }

    private List<KameraSatir> KameraGorunenSatirlariAl()
        => (_kameraSatirView?.Cast<object>().OfType<KameraSatir>().ToList() ?? _kameraSatirlari.ToList())
           .OrderBy(s => IpSiralamaAnahtari(s.Ip))
           .ThenBy(s => s.Ip, StringComparer.Ordinal)
           .ToList();

    private static long IpSiralamaAnahtari(string ip)
    {
        var parcalar = ip.Split('.');
        if (parcalar.Length != 4) return long.MaxValue;
        long sonuc = 0;
        foreach (var parca in parcalar)
        {
            if (!byte.TryParse(parca, out var b)) return long.MaxValue;
            sonuc = (sonuc << 8) + b;
        }
        return sonuc;
    }

    private static IEnumerable<string[]> KameraExportSatirlari(IEnumerable<KameraSatir> satirlar)
    {
        foreach (var s in satirlar)
            yield return new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
    }

    private static readonly string[] KameraExportBasliklari =
        { "IP", "Ad", "Tur", "Marka", "Model", "OS", "Durum", "Ping", "Portlar", "Kesif", "MAC", "Uretici", "Servis" };

    private static string KameraCsvOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", KameraExportBasliklari.Select(CsvHucre)));
        foreach (var row in KameraExportSatirlari(satirlar))
            sb.AppendLine(string.Join(";", row.Select(CsvHucre)));
        return sb.ToString();
    }

    private static string KameraJsonOlustur(List<KameraSatir> satirlar)
    {
        var veri = new
        {
            Uygulama = "AgTarama",
            Tur = "Cihaz Tara",
            OlusturmaTarihi = DateTimeOffset.Now,
            Toplam = satirlar.Count,
            Cihazlar = satirlar.Select(s => new
            {
                s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.SonGorulen,
                s.Ping, s.PingMs, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis, s.WebUrl,
            }).ToList(),
        };
        return System.Text.Json.JsonSerializer.Serialize(veri, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string CsvHucre(string? metin)
    {
        metin ??= "";
        metin = metin.Replace("\r", " ").Replace("\n", " ");
        return $"\"{metin.Replace("\"", "\"\"")}\"";
    }

    private static string KameraTxtOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NETWORK SNIFFER - CIHAZ TARA RAPORU");
        sb.AppendLine($"Tarih : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Cihaz : {satirlar.Count}");
        sb.AppendLine(new string('=', 110));
        foreach (var s in satirlar)
        {
            sb.AppendLine($"{s.Ip,-15}  {MetniKirp(s.Tur, 14),-14}  {MetniKirp(s.Marka, 16),-16}  {MetniKirp(s.Model, 34)}");
            sb.AppendLine($"  Ad      : {s.Ad}");
            sb.AppendLine($"  OS      : {s.Os}  Durum: {s.Durum}  Son Görülme: {s.SonGorulen}");
            sb.AppendLine($"  Ping    : {s.Ping}");
            sb.AppendLine($"  Portlar : {s.Portlar}");
            sb.AppendLine($"  Keşif   : {s.Kesif}");
            sb.AppendLine($"  MAC     : {s.Mac}  {s.Uretici}");
            sb.AppendLine($"  Servis  : {s.Servis}");
            sb.AppendLine(new string('-', 110));
        }
        return sb.ToString();
    }

    private static byte[] KameraExcelXlsxOlustur(List<KameraSatir> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cihaz Tara");

        for (int i = 0; i < KameraExportBasliklari.Length; i++)
            ws.Cell(1, i + 1).Value = KameraExportBasliklari[i];

        var headerRange = ws.Range(1, 1, 1, KameraExportBasliklari.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D3B66");
        headerRange.Style.Font.Bold            = true;
        headerRange.Style.Font.FontColor       = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        int row = 2;
        foreach (var s in satirlar)
        {
            var vals = new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
            for (int i = 0; i < vals.Length; i++)
                ws.Cell(row, i + 1).Value = vals[i];
            if (row % 2 == 0)
                ws.Range(row, 1, row, KameraExportBasliklari.Length)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#101722");
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] KameraPdfQuestOlustur(List<KameraSatir> satirlar)
    {
        var rows = satirlar.Select(s => new DeviceScanRow(
            s.Ip, s.Ad, s.Tur, s.Marka, s.Model,
            s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis)).ToList();
        return PdfReportService.GenerateDeviceScanReport(rows, new ReportMetadata());
    }

    private static string MetniKirp(string? metin, int uzunluk)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        metin = Regex.Replace(metin.Trim(), @"\s+", " ");
        return metin.Length <= uzunluk ? metin : metin[..Math.Max(0, uzunluk - 1)] + "…";
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

        var subnetler = SubnetGirdisiniCoz(giris);
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

    private KameraSatir KameraSatirOlustur(DeviceInfo bilgi)
    {
        var kim      = KimlikBelirle(bilgi);
        var cihazAdi = CihazAdiSec(bilgi) ?? "";

        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = bilgi.AcikPortlar.Order().ToList();

        List<string> servisler;
        lock (bilgi.ServisDetaylari)
            servisler = bilgi.ServisDetaylari.OrderBy(x => x.Key).Select(x => $"{x.Key}/{x.Value}").ToList();

        var kesifSet = new HashSet<string>(bilgi.KesifKaynaklari, StringComparer.OrdinalIgnoreCase);
        if (bilgi.OnvifBulundu) kesifSet.Add("ONVIF");
        if (bilgi.SsdpBulundu)  kesifSet.Add("UPnP");
        var kesifler = kesifSet.OrderBy(s => KesifSira(s)).ToList();

        var durum = bilgi.Online ? "Online" : "Offline";
        var sonGorulen = bilgi.LastSeen == default
            ? ""
            : bilgi.LastSeen.Date == DateTime.Today
                ? bilgi.LastSeen.ToString("HH:mm:ss")
                : bilgi.LastSeen.ToString("dd.MM HH:mm");

        return new KameraSatir
        {
            Ip        = bilgi.Ip,
            Ad        = cihazAdi,
            Tur       = kim.Tur,
            Marka     = kim.Marka == "Bilinmiyor" ? "" : kim.Marka,
            Model     = kim.Model ?? "",
            Os        = bilgi.Os ?? "",
            Durum     = durum,
            SonGorulen = sonGorulen,
            Online    = bilgi.Online,
            Ping      = bilgi.PingYanit ? $"{bilgi.PingMs} ms" : "",
            PingMs    = bilgi.PingYanit ? bilgi.PingMs : int.MaxValue,
            Portlar   = string.Join(", ", portlar),
            Kesif     = string.Join(", ", kesifler),
            Mac       = bilgi.MacAdresi ?? "",
            Uretici   = bilgi.Uretici ?? "",
            Servis    = string.Join(" | ", servisler.DefaultIfEmpty(IlkDolu(bilgi.SunucuBasligi, bilgi.SayfaBasligi, bilgi.RtspDurum) ?? "")),
            WebUrl    = KameraWebUrlSec(bilgi),
            Guven     = GuvenSkoru(bilgi, kim),
            KararIzi  = KararIziOzetle(bilgi.KararIzi),
        };
    }

    private static int KesifSira(string kaynak) => kaynak.ToUpperInvariant() switch
    {
        "UBIQUITI" => 0,
        "MNDP"     => 1,
        "ONVIF"    => 2,
        "WSD"      => 3,
        "UPNP"     => 4,
        "SSDP"     => 4,
        "MDNS"     => 5,
        "SNMP"     => 6,
        "HTTP-FP"  => 7,
        "NETBIOS"  => 8,
        "SMB"      => 8,
        "LLMNR"    => 9,
        "SSH"      => 9,
        "PORT"     => 10,
        "PING"     => 11,
        "ARP"      => 12,
        _          => 99,
    };

    private static string? KameraWebUrlSec(DeviceInfo bilgi)
    {
        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = [..bilgi.AcikPortlar];
        foreach (var (port, scheme) in new (int, string)[] { (80, "http"), (443, "https"), (8080, "http"), (8443, "https"), (9000, "http") })
        {
            if (!portlar.Contains(port)) continue;
            return port is 80 or 443 ? $"{scheme}://{bilgi.Ip}/" : $"{scheme}://{bilgi.Ip}:{port}/";
        }
        return null;
    }

    private void KameraFiltreleriUygula()
    {
        _kameraSatirView?.Refresh();
        int toplam  = _kameraSatirlari.Count;
        int gorunen = _kameraSatirView?.Cast<object>().Count() ?? toplam;
        KameraFiltreSayacText.Text = toplam == 0 ? "0 cihaz" : $"{gorunen}/{toplam} cihaz";
    }

    private bool KameraSatirFiltredenGecer(object obj)
    {
        if (obj is not KameraSatir satir) return false;
        // Düşük güven filtresi (toggle OFF iken güven < 12 ve offline satırlar gizli)
        if (!_dusukGuvenGoster && satir.Guven < 12 && !satir.Online) return false;
        var tur = (KameraTurFiltreBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Hepsi";
        if (string.Equals(tur, "Bilinmiyor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(satir.Tur, "Cihaz", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(satir.Tur))
                return false;
        }
        else if (!string.Equals(tur, "Hepsi", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(satir.Tur, tur, StringComparison.OrdinalIgnoreCase))
            return false;
        return Icerir(satir.Ip, KameraIpFiltreBox?.Text) &&
               Icerir($"{satir.Ad} {satir.Model}", KameraAdFiltreBox?.Text) &&
               Icerir($"{satir.Marka} {satir.Uretici}", KameraMarkaFiltreBox?.Text) &&
               Icerir($"{satir.Portlar} {satir.Servis} {satir.Kesif}", KameraPortFiltreBox?.Text) &&
               Icerir(satir.Mac, KameraMacFiltreBox?.Text);
    }

    private static bool Icerir(string? kaynak, string? filtre)
        => string.IsNullOrWhiteSpace(filtre) ||
           (kaynak?.Contains(filtre.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    private void KameraKutucugaYaz(string metin, string hex)
        => KameraIlerlemeText.Text = metin;

    // ─── KameraSatir (görünüm modeli) ────────────────────────────────

    public sealed class KameraSatir : INotifyPropertyChanged
    {
        private string  _ip        = "";
        private string  _ad        = "";
        private string  _tur       = "";
        private string  _marka     = "";
        private string  _model     = "";
        private string  _os        = "";
        private string  _durum     = "";
        private string  _sonGorulen = "";
        private bool    _online    = false;
        private string  _ping      = "";
        private int     _pingMs    = int.MaxValue;
        private string  _portlar   = "";
        private string  _kesif     = "";
        private string  _mac       = "";
        private string  _uretici   = "";
        private string  _servis    = "";
        private string? _webUrl;
        private int     _guven     = 0;
        private string  _kararIzi  = "";

        public string  Ip        { get => _ip;        set => Set(ref _ip,        value); }
        public string  Ad        { get => _ad;        set => Set(ref _ad,        value); }
        public string  Tur       { get => _tur;       set => Set(ref _tur,       value); }
        public string  Marka     { get => _marka;     set => Set(ref _marka,     value); }
        public string  Model     { get => _model;     set => Set(ref _model,     value); }
        public string  Os        { get => _os;        set => Set(ref _os,        value); }
        public string  Durum     { get => _durum;     set => Set(ref _durum,     value); }
        public string  SonGorulen { get => _sonGorulen; set => Set(ref _sonGorulen, value); }
        public bool    Online    { get => _online;    set => Set(ref _online,    value); }
        public string  Ping      { get => _ping;      set => Set(ref _ping,      value); }
        public int     PingMs    { get => _pingMs;    set => Set(ref _pingMs,    value); }
        public string  Portlar   { get => _portlar;   set => Set(ref _portlar,   value); }
        public string  Kesif     { get => _kesif;     set => Set(ref _kesif,     value); }
        public string  Mac       { get => _mac;       set => Set(ref _mac,       value); }
        public string  Uretici   { get => _uretici;   set => Set(ref _uretici,   value); }
        public string  Servis    { get => _servis;    set => Set(ref _servis,    value); }
        public string? WebUrl    { get => _webUrl;    set => Set(ref _webUrl,    value); }
        public int     Guven     { get => _guven;     set { if (Set(ref _guven, value)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GuvenRenk))); } }
        public string  KararIzi  { get => _kararIzi;  set => Set(ref _kararIzi,  value); }

        // Computed — renk kodu: Kirmizi / Sari / Yesil / boş
        public string GuvenRenk => _guven >= 70 ? "Yesil" : _guven >= 40 ? "Sari" : _guven > 0 ? "Kirmizi" : "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Kopyala(KameraSatir diger)
        {
            Ip         = diger.Ip;
            Ad         = diger.Ad;
            Tur        = diger.Tur;
            Marka      = diger.Marka;
            Model      = diger.Model;
            Os         = diger.Os;
            Durum      = diger.Durum;
            SonGorulen = diger.SonGorulen;
            Online     = diger.Online;
            Ping       = diger.Ping;
            PingMs     = diger.PingMs;
            Portlar    = diger.Portlar;
            Kesif      = diger.Kesif;
            Mac        = diger.Mac;
            Uretici    = diger.Uretici;
            Servis     = diger.Servis;
            WebUrl     = diger.WebUrl;
            Guven      = diger.Guven;
            KararIzi   = diger.KararIzi;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
