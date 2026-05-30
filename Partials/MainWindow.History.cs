using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    private void GecmisYenile_Click(object sender, RoutedEventArgs e) => GecmisPanelGuncelle();

    private void GecmisKlasorAc_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Paths.HistoryKlasor);
        Process.Start(new ProcessStartInfo(Paths.HistoryKlasor) { UseShellExecute = true });
    }

    private void GecmisFiltreBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _gecmisFiltreTur = btn.Tag as string ?? "";
        GecmisPanelGuncelle();
    }

    private void GecmisKaydiSil(HistoryRecord kayit)
    {
        var path = HistoryService.KayitYolu(kayit.Id);
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
        GecmisPanelGuncelle();
    }

    private void GecmisTumunuTemizle_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Paths.HistoryKlasor)) return;
        var dosyalar = Directory.EnumerateFiles(Paths.HistoryKlasor, "*.json").ToList();
        if (dosyalar.Count == 0) { ToastGoster("Temizlenecek gecmis yok"); return; }
        if (MessageBox.Show($"{dosyalar.Count} kayit kalici olarak silinecek. Emin misiniz?",
                "Gecmisi Temizle", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        foreach (var f in dosyalar)
            try { File.Delete(f); } catch { }
        _gecmisFiltreTur = "";
        GecmisPanelGuncelle();
        ToastGoster($"{dosyalar.Count} kayit silindi");
    }

    private void GecmisPanelGuncelle()
    {
        GecmisListePanel.Children.Clear();
        _gecmisKayitlari = HistoryService.SonKayitlariYukle();

        var liste = string.IsNullOrEmpty(_gecmisFiltreTur)
            ? _gecmisKayitlari
            : _gecmisKayitlari.Where(k => string.Equals(k.Type, _gecmisFiltreTur, StringComparison.OrdinalIgnoreCase)).ToList();

        GecmisFiltreTumu.FontWeight = string.IsNullOrEmpty(_gecmisFiltreTur) ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreYakalama.FontWeight = _gecmisFiltreTur == "YAKALAMA" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreKamera.FontWeight = _gecmisFiltreTur is "CİHAZ TARA" or "CIHAZ TARA" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltrePing.FontWeight = _gecmisFiltreTur == "PING" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltrePort.FontWeight = _gecmisFiltreTur == "PORT TARA" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreArp.FontWeight = _gecmisFiltreTur == "ARP" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreWifi.FontWeight = _gecmisFiltreTur == "WIFI TARA" ? FontWeights.Bold : FontWeights.Normal;

        if (liste.Count == 0)
        {
            GecmisListePanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(_gecmisFiltreTur)
                    ? "Henuz gecmis kaydi yok. Ping, Port Tara, Cihaz Tara, Wi-Fi Tara, ARP veya paket yakalama calistirin."
                    : $"\"{_gecmisFiltreTur}\" turunde gecmis kaydi yok.",
                Foreground = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4),
            });
            return;
        }

        foreach (var kayit in liste)
            GecmisListePanel.Children.Add(GecmisKartiOlustur(kayit));
    }

    private Border GecmisKartiOlustur(HistoryRecord kayit)
    {
        var kart = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = $"{kayit.CreatedAt:yyyy-MM-dd HH:mm:ss}  [{kayit.Type}]  {kayit.Target}",
            Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        sp.Children.Add(new TextBlock
        {
            Text = kayit.Summary,
            Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 6),
        });

        var actions = new WrapPanel();
        var ac = new Button { Content = "JSON Ac", Style = (Style)FindResource("ChipButton"), Tag = kayit.Id };
        ac.Click += (_, _) => GecmisKaydiAc(kayit);
        actions.Children.Add(ac);

        var tekrar = new Button { Content = "Tekrar Calistir", Style = (Style)FindResource("ChipButton"), Tag = kayit.Id };
        tekrar.Click += (_, _) => GecmisKaydiTekrarCalistir(kayit);
        actions.Children.Add(tekrar);

        var sil = new Button
        {
            Content = "Sil",
            Style = (Style)FindResource("ChipButton"),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)),
        };
        sil.Click += (_, _) => GecmisKaydiSil(kayit);
        actions.Children.Add(sil);

        sp.Children.Add(actions);
        kart.Child = sp;
        return kart;
    }

    private void GecmisKaydiAc(HistoryRecord kayit)
    {
        var path = HistoryService.KayitYolu(kayit.Id);
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string NormalizeTip(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // Türkçe büyük harf farkını giderir: İ→I, ı→I, vd.
        return s.ToUpperInvariant().Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
    }

    private void GecmisKaydiTekrarCalistir(HistoryRecord kayit)
    {
        _gecmisdenCalistiriliyor = true;

        switch (NormalizeTip(kayit.Type))
        {
            case "PING":
                MainTabControl.SelectedIndex = TabPing;
                PingIpBox.Text = kayit.Target;
                _ = PingBaslat(kayit.Target);
                break;

            case "PORT TARA":
                MainTabControl.SelectedIndex = TabPort;
                PortIpBox.Text = kayit.Target;
                if (kayit.Metadata.TryGetValue("TarananPortlar", out var portlar) && !string.IsNullOrWhiteSpace(portlar))
                    PortAralikBox.Text = portlar;
                _ = PortTaraBaslat(kayit.Target, PortScanService.Parse(portlar ?? PortAralikBox.Text));
                break;

            case "CIHAZ TARA":
                MainTabControl.SelectedIndex = TabCihazTara;
                if (kayit.Metadata.TryGetValue("SubnetInput", out var subnetInput) && !string.IsNullOrWhiteSpace(subnetInput))
                    KameraSubnetBox.Text = subnetInput;
                else if (kayit.Metadata.TryGetValue("Subnet", out var subnet) && !string.IsNullOrWhiteSpace(subnet))
                    KameraSubnetBox.Text = subnet;
                _ = KameraTaramaBaslat();
                break;

            case "ARP":
                MainTabControl.SelectedIndex = TabChatbot;
                _ = ArpTablosuGoster();
                break;

            case "WIFI TARA":
                MainTabControl.SelectedIndex = TabWlan;
                _ = WlanTaramaBaslat();
                break;

            default:
                _gecmisdenCalistiriliyor = false;
                ToastGoster("Bu kayit turu tekrar calistirilamiyor", hata: true);
                break;
        }
    }

    private void GecmisKarsilastir_Click(object sender, RoutedEventArgs e)
    {
        var cihazKayitlari = HistoryService.SonKayitlariYukle(200)
            .Where(k => k.Type.Equals("CİHAZ TARA", StringComparison.OrdinalIgnoreCase)
                     || k.Type.Equals("CIHAZ TARA", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        if (cihazKayitlari.Count < 2)
        {
            ToastGoster("Karsilastirma icin en az iki Cihaz Tara kaydi gerekli", hata: true);
            return;
        }

        var yeni = GecmisCihazHaritasi(cihazKayitlari[0]);
        var eski = GecmisCihazHaritasi(cihazKayitlari[1]);
        var yeniIps = yeni.Keys.ToHashSet(StringComparer.Ordinal);
        var eskiIps = eski.Keys.ToHashSet(StringComparer.Ordinal);

        var eklenen = yeniIps.Except(eskiIps).OrderBy(IpSiralamaAnahtari).ThenBy(x => x).ToList();
        var kaybolan = eskiIps.Except(yeniIps).OrderBy(IpSiralamaAnahtari).ThenBy(x => x).ToList();

        var ortak = yeniIps.Intersect(eskiIps).OrderBy(IpSiralamaAnahtari).ThenBy(x => x).ToList();
        var portDegisen = new List<string>();
        var macDegisen = new List<string>();
        var vendorModelDegisen = new List<string>();
        var adDegisen = new List<string>();
        var yeniServis = new List<string>();

        foreach (var ip in ortak)
        {
            var y = yeni[ip];
            var eskiDetay = eski[ip];

            if (!string.Equals(NormalizeList(y.Portlar), NormalizeList(eskiDetay.Portlar), StringComparison.OrdinalIgnoreCase))
                portDegisen.Add(ip);
            if (!string.Equals(y.Mac, eskiDetay.Mac, StringComparison.OrdinalIgnoreCase))
                macDegisen.Add(ip);
            if (!string.Equals($"{y.Uretici}|{y.Marka}|{y.Model}", $"{eskiDetay.Uretici}|{eskiDetay.Marka}|{eskiDetay.Model}", StringComparison.OrdinalIgnoreCase))
                vendorModelDegisen.Add(ip);
            if (!string.Equals(y.Ad, eskiDetay.Ad, StringComparison.OrdinalIgnoreCase))
                adDegisen.Add(ip);
            if (ServisArtti(eskiDetay.Servis, y.Servis))
                yeniServis.Add(ip);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Cihaz Tara karsilastirmasi");
        sb.AppendLine($"Yeni : {cihazKayitlari[0].CreatedAt:yyyy-MM-dd HH:mm:ss} ({yeniIps.Count} cihaz)");
        sb.AppendLine($"Eski : {cihazKayitlari[1].CreatedAt:yyyy-MM-dd HH:mm:ss} ({eskiIps.Count} cihaz)");
        sb.AppendLine();
        sb.AppendLine($"Yeni gelen ({eklenen.Count}): {(eklenen.Count == 0 ? "-" : string.Join(", ", eklenen))}");
        sb.AppendLine($"Kaybolan ({kaybolan.Count}): {(kaybolan.Count == 0 ? "-" : string.Join(", ", kaybolan))}");
        sb.AppendLine($"Port degisen ({portDegisen.Count}): {(portDegisen.Count == 0 ? "-" : string.Join(", ", portDegisen))}");
        sb.AppendLine($"MAC degisen ({macDegisen.Count}): {(macDegisen.Count == 0 ? "-" : string.Join(", ", macDegisen))}");
        sb.AppendLine($"Vendor/Model degisen ({vendorModelDegisen.Count}): {(vendorModelDegisen.Count == 0 ? "-" : string.Join(", ", vendorModelDegisen))}");
        sb.AppendLine($"Cihaz adi degisen ({adDegisen.Count}): {(adDegisen.Count == 0 ? "-" : string.Join(", ", adDegisen))}");
        sb.AppendLine($"Yeni servis bulunan ({yeniServis.Count}): {(yeniServis.Count == 0 ? "-" : string.Join(", ", yeniServis))}");

        MesajEkle("sonuc", sb.ToString().TrimEnd());
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private sealed class GecmisCihazDetay
    {
        public string Ip { get; init; } = "";
        public string Ad { get; init; } = "";
        public string Portlar { get; init; } = "";
        public string Mac { get; init; } = "";
        public string Uretici { get; init; } = "";
        public string Marka { get; init; } = "";
        public string Model { get; init; } = "";
        public string Servis { get; init; } = "";
    }

    private static Dictionary<string, GecmisCihazDetay> GecmisCihazHaritasi(HistoryRecord kayit)
    {
        if (!kayit.Metadata.TryGetValue("CihazlarJson", out var json) || string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, GecmisCihazDetay>(StringComparer.Ordinal);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var map = new Dictionary<string, GecmisCihazDetay>(StringComparer.Ordinal);
            foreach (var e in doc.RootElement.GetProperty("Cihazlar").EnumerateArray())
            {
                var ip = PropStr(e, "Ip");
                if (string.IsNullOrWhiteSpace(ip)) continue;
                map[ip] = new GecmisCihazDetay
                {
                    Ip = ip,
                    Ad = PropStr(e, "Ad"),
                    Portlar = PropStr(e, "Portlar"),
                    Mac = PropStr(e, "Mac"),
                    Uretici = PropStr(e, "Uretici"),
                    Marka = PropStr(e, "Marka"),
                    Model = PropStr(e, "Model"),
                    Servis = PropStr(e, "Servis"),
                };
            }
            return map;
        }
        catch
        {
            return new Dictionary<string, GecmisCihazDetay>(StringComparer.Ordinal);
        }
    }

    private static string PropStr(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var p)) return "";
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "") : p.ToString();
    }

    private static string NormalizeList(string input)
        => string.Join(",", (input ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static bool ServisArtti(string eski, string yeni)
    {
        var eskiSet = SplitServis(eski);
        var yeniSet = SplitServis(yeni);
        return yeniSet.Any(x => !eskiSet.Contains(x));
    }

    private static HashSet<string> SplitServis(string value)
        => value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
