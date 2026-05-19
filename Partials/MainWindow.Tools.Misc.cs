using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Tab geçiş + harici araç butonları ───
    private void BtnTrace_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabTrace;
        TraceHedefBox.Focus();
    }

    private void BtnDns_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabDns;
        DnsHedefBox.Focus();
    }

    private void BtnWol_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabWol;
        WolMacBox.Focus();
    }

    private void BtnArp_Click(object sender, RoutedEventArgs e)    => _ = ArpTablosuGoster();
    private void BtnAgBilgi_Click(object sender, RoutedEventArgs e) => AgAdaptorleriniGoster();

    private void BtnSadp_Click(object sender, RoutedEventArgs e)
        => HariciAracBaslat(Paths.SadpExe, "SADP aracı");

    private void BtnCihazlar_Click(object sender, RoutedEventArgs e)
        => HariciAracBaslat(Paths.IpScannerExe, "Advanced IP Scanner");

    private void HariciAracBaslat(string exe, string ad)
    {
        if (!File.Exists(exe))
        {
            HataBildir($"{ad} bulunamadı:\n{exe}");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute  = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            });
            MesajEkle("sistem", $"{ad} başlatıldı.");
        }
        catch (Exception ex)
        {
            HataBildir($"{ad} açılamadı", ex);
        }
    }

    private void BtnTemizle_Click(object sender, RoutedEventArgs e)
    {
        ChatPanel.Children.Clear();
        MesajEkle("sistem", "Ekran temizlendi.");
    }

    // ─── Traceroute ───
    private void TraceHedefBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(TraceHedefBox);
        var m = TraceHedefBox.Text.Trim();
        TraceHedefPlaceholder.Visibility = string.IsNullOrEmpty(TraceHedefBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(m)) { TraceHedefValidasyon.Text = ""; TraceBaslatBtn.IsEnabled = false; return; }
        if (GecerliIpv4Mu(m))        { TraceHedefValidasyon.Text = "✓"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63,  185, 80));  TraceBaslatBtn.IsEnabled = true; }
        else if (GecerliHostnameMu(m)){ TraceHedefValidasyon.Text = "~"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34)); TraceBaslatBtn.IsEnabled = true; }
        else                          { TraceHedefValidasyon.Text = "✗"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81,  73));  TraceBaslatBtn.IsEnabled = false; }
    }

    private void TraceHedefBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TraceBaslatBtn.IsEnabled) _ = TracerouteBaslat(TraceHedefBox.Text.Trim());
    }

    private void TraceBaslatBtn_Click(object sender, RoutedEventArgs e) => _ = TracerouteBaslat(TraceHedefBox.Text.Trim());
    private void TraceHizliBtn_Click(object sender, RoutedEventArgs e)
    {
        TraceHedefBox.Text = (string)((Button)sender).Tag;
        _ = TracerouteBaslat(TraceHedefBox.Text);
    }
    private void TracePanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _traceCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void TraceKutucugaYaz(string metin, string hex) =>
        TraceResultPanel.Children.Add(new TextBlock
        {
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private async Task TracerouteBaslat(string hedef)
    {
        _traceCts?.Cancel();
        _traceCts?.Dispose();
        _traceCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = _traceCts.Token;

        TraceResultPanel.Children.Clear();
        TraceResultBorder.Visibility = Visibility.Visible;
        TraceBaslatBtn.IsEnabled     = false;
        TraceKutucugaYaz($"◆ {hedef} → rota izleniyor...", "#8B949E");

        var logSatirlari = new List<string>();
        try
        {
            var psi = new ProcessStartInfo("tracert", $"-d -w 2000 {hedef}")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            while (!token.IsCancellationRequested)
            {
                var satir = await proc.StandardOutput.ReadLineAsync(token);
                if (satir == null) break;
                var t = satir.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                string hex;
                if (t.StartsWith("Tracing") || t.StartsWith("over a")) hex = "#8B949E";
                else if (t.Contains("* * *") || t.Contains("Request timed out")) hex = "#F85149";
                else if (t.Contains("Trace complete")) hex = "#3FB950";
                else hex = "#58A6FF";
                logSatirlari.Add(t);
                await Dispatcher.InvokeAsync(() => { TraceKutucugaYaz(t, hex); TraceResultScroll.ScrollToEnd(); });
            }
            if (!token.IsCancellationRequested) try { await proc.WaitForExitAsync(token); } catch { }
            else try { proc.Kill(true); } catch { }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { TraceKutucugaYaz($"✖ {ex.Message}", "#F85149"); }

        if (!token.IsCancellationRequested)
        {
            TraceBaslatBtn.IsEnabled = true;
            LogService.Kaydet("TRACEROUTE", hedef, logSatirlari);
        }
    }

    // ─── DNS Lookup ───
    private void DnsHedefBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(DnsHedefBox);
        DnsHedefPlaceholder.Visibility = string.IsNullOrEmpty(DnsHedefBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        var m = DnsHedefBox.Text.Trim();
        DnsBaslatBtn.IsEnabled = !string.IsNullOrEmpty(m) && (GecerliIpv4Mu(m) || GecerliHostnameMu(m));
    }

    private void DnsHedefBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DnsBaslatBtn.IsEnabled) _ = DnsLookupBaslat(DnsHedefBox.Text.Trim());
    }

    private void DnsBaslatBtn_Click(object sender, RoutedEventArgs e) => _ = DnsLookupBaslat(DnsHedefBox.Text.Trim());
    private void DnsPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void DnsKutucugaYaz(string metin, string hex) =>
        DnsResultPanel.Children.Add(new TextBlock
        {
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private async Task DnsLookupBaslat(string hedef)
    {
        DnsResultPanel.Children.Clear();
        DnsResultBorder.Visibility = Visibility.Visible;
        DnsBaslatBtn.IsEnabled     = false;
        DnsKutucugaYaz($"◆ {hedef} sorgulanıyor...", "#8B949E");
        var logSatirlari = new List<string>();
        try
        {
            if (GecerliIpv4Mu(hedef))
            {
                var entry = await Dns.GetHostEntryAsync(hedef);
                var s1 = $"PTR  →  {entry.HostName}";
                DnsKutucugaYaz(s1, "#58A6FF");
                logSatirlari.Add(s1);
                foreach (var ip in entry.AddressList)
                {
                    var s = $"       {ip}";
                    DnsKutucugaYaz(s, "#C9D1D9");
                    logSatirlari.Add(s);
                }
            }
            else
            {
                var entry = await Dns.GetHostEntryAsync(hedef);
                var s1 = $"HOST  →  {entry.HostName}";
                DnsKutucugaYaz(s1, "#58A6FF");
                logSatirlari.Add(s1);
                foreach (var ip in entry.AddressList)
                {
                    var s = $"  {(ip.AddressFamily == AddressFamily.InterNetwork ? "A   " : "AAAA")}  →  {ip}";
                    DnsKutucugaYaz(s, "#3FB950");
                    logSatirlari.Add(s);
                }
                if (entry.Aliases.Length > 0)
                    foreach (var a in entry.Aliases)
                    {
                        var s = $"CNAME →  {a}";
                        DnsKutucugaYaz(s, "#D2991E");
                        logSatirlari.Add(s);
                    }
            }
        }
        catch (Exception ex)
        {
            var s = $"✖ {ex.Message}";
            DnsKutucugaYaz(s, "#F85149");
            logSatirlari.Add(s);
        }
        DnsBaslatBtn.IsEnabled = true;
        DnsResultScroll.ScrollToEnd();
        LogService.Kaydet("DNS", hedef, logSatirlari);
    }

    // ─── Wake-on-LAN ───
    private static readonly Regex _macRegex = new(
        @"^([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$",
        RegexOptions.Compiled);

    private void WolMacBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        WolMacPlaceholder.Visibility = string.IsNullOrEmpty(WolMacBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        var m = WolMacBox.Text.Trim();
        if (string.IsNullOrEmpty(m)) { WolMacValidasyon.Text = ""; WolGonderBtn.IsEnabled = false; return; }
        if (_macRegex.IsMatch(m)) { WolMacValidasyon.Text = "✓"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)); WolGonderBtn.IsEnabled = true; }
        else                      { WolMacValidasyon.Text = "✗"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)); WolGonderBtn.IsEnabled = false; }
    }

    private void WolMacBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && WolGonderBtn.IsEnabled) WolGonder(WolMacBox.Text.Trim());
    }

    private void WolGonderBtn_Click(object sender, RoutedEventArgs e) => WolGonder(WolMacBox.Text.Trim());
    private void WolPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void WolKutucugaYaz(string metin, string hex) =>
        WolResultPanel.Children.Add(new TextBlock
        {
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private void WolGonder(string mac)
    {
        WolResultPanel.Children.Clear();
        WolResultBorder.Visibility = Visibility.Visible;
        var logSatirlari = new List<string>();
        try
        {
            var temiz    = mac.Replace(":", "").Replace("-", "");
            var macBytes = Enumerable.Range(0, 6).Select(i => Convert.ToByte(temiz.Substring(i * 2, 2), 16)).ToArray();
            var paket    = new byte[102];
            for (int i = 0; i < 6; i++) paket[i] = 0xFF;
            for (int k = 1; k <= 16; k++)
                for (int i = 0; i < 6; i++) paket[k * 6 + i] = macBytes[i];
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Send(paket, paket.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            udp.Send(paket, paket.Length, new IPEndPoint(IPAddress.Broadcast, 7));
            WolKutucugaYaz($"✔ Magic packet gönderildi → {mac}", "#3FB950");
            WolKutucugaYaz("  Port 9 + 7 (broadcast)", "#8B949E");
            logSatirlari.Add($"✔ Magic packet gönderildi → {mac}");
            logSatirlari.Add("  Port 9 + 7 (broadcast)");
        }
        catch (Exception ex)
        {
            var s = $"✖ {ex.Message}";
            WolKutucugaYaz(s, "#F85149");
            logSatirlari.Add(s);
        }
        LogService.Kaydet("WAKE-ON-LAN", mac, logSatirlari);
    }

    // ─── Ağ Adaptörü Bilgileri ───
    private void AgAdaptorleriniGoster()
    {
        var adaptorler = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                     && n.GetIPProperties().UnicastAddresses
                        .Any(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
            .ToList();

        if (adaptorler.Count == 0) { MesajEkle("sistem", "Aktif ağ adaptörü bulunamadı."); return; }

        var logSatirlari = new List<string>();
        foreach (var n in adaptorler)
        {
            var props = n.GetIPProperties();
            var sb = new StringBuilder();
            sb.AppendLine($"▶ {n.Name}  ({n.Description})");
            sb.AppendLine($"  MAC : {n.GetPhysicalAddress()}");
            foreach (var uni in props.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                sb.AppendLine($"  IPv4: {uni.Address}  /  {uni.IPv4Mask}");
            foreach (var gw in props.GatewayAddresses)
                sb.AppendLine($"  GW  : {gw.Address}");
            foreach (var dns in props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork))
                sb.AppendLine($"  DNS : {dns}");
            var metin = sb.ToString().TrimEnd();
            MesajEkle("sonuc", metin);
            logSatirlari.Add(metin);
        }
        LogService.Kaydet("AG BILGI", "yerel adaptörler", logSatirlari);
    }

    // ─── ARP Tablosu ───
    private async Task ArpTablosuGoster()
    {
        MesajEkle("sistem", "ARP tablosu okunuyor...");
        try
        {
            var psi = new ProcessStartInfo("arp", "-a")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            var cikti = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var regex    = new Regex(@"(\d{1,3}(?:\.\d{1,3}){3})\s+([0-9a-fA-F]{2}(?:[:\-][0-9a-fA-F]{2}){5})\s+(\w+)");
            var esleseler = regex.Matches(cikti);
            if (esleseler.Count == 0) { MesajEkle("sistem", "ARP tablosunda kayıt bulunamadı."); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"ARP tablosu — {esleseler.Count} kayıt:");
            sb.AppendLine($"  {"IP Adresi",-18} {"MAC Adresi",-20} {"Tür",-10} Üretici");
            sb.AppendLine($"  {"─────────────────",-18} {"───────────────────",-20} {"──────────",-10} ──────────────────");
            foreach (Match m in esleseler)
            {
                var uretici = OuiAra(m.Groups[2].Value);
                sb.AppendLine($"  {m.Groups[1].Value,-18} {m.Groups[2].Value,-20} {m.Groups[3].Value,-10} {uretici}");
            }
            var metin   = sb.ToString().TrimEnd();
            MesajEkle("sonuc", metin);
            var satirlar = metin.Split('\n').Select(s => s.TrimEnd()).ToList();
            LogService.Kaydet("ARP", "arp -a", satirlar);
            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("ARP", "arp -a", $"ARP tablosu — {esleseler.Count} kayıt", satirlar,
                    new Dictionary<string, string> { ["KayitSayisi"] = esleseler.Count.ToString() });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
        }
        catch (Exception ex) { MesajEkle("hata", $"ARP tablosu okunamadı: {ex.Message}"); }
    }
}
