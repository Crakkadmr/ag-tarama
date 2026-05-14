using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Text.Json;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Geçmiş Paneli ───────────────────────────────────────────────

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
        if (dosyalar.Count == 0) { ToastGoster("Temizlenecek geçmiş yok"); return; }
        if (MessageBox.Show($"{dosyalar.Count} kayıt kalıcı olarak silinecek. Emin misiniz?",
                "Geçmişi Temizle", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        foreach (var f in dosyalar)
            try { File.Delete(f); } catch { }
        _gecmisFiltreTur = "";
        GecmisPanelGuncelle();
        ToastGoster($"{dosyalar.Count} kayıt silindi");
    }

    private void GecmisPanelGuncelle()
    {
        GecmisListePanel.Children.Clear();
        _gecmisKayitlari = HistoryService.SonKayitlariYukle();

        var liste = string.IsNullOrEmpty(_gecmisFiltreTur)
            ? _gecmisKayitlari
            : _gecmisKayitlari.Where(k => string.Equals(k.Type, _gecmisFiltreTur, StringComparison.OrdinalIgnoreCase)).ToList();

        GecmisFiltreTumu.FontWeight     = string.IsNullOrEmpty(_gecmisFiltreTur) ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreYakalama.FontWeight = _gecmisFiltreTur == "YAKALAMA"   ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreKamera.FontWeight   = _gecmisFiltreTur == "CİHAZ TARA" ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltrePing.FontWeight     = _gecmisFiltreTur == "PING"        ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltrePort.FontWeight     = _gecmisFiltreTur == "PORT TARA"   ? FontWeights.Bold : FontWeights.Normal;
        GecmisFiltreArp.FontWeight      = _gecmisFiltreTur == "ARP"         ? FontWeights.Bold : FontWeights.Normal;

        if (liste.Count == 0)
        {
            GecmisListePanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(_gecmisFiltreTur)
                    ? "Henüz geçmiş kaydı yok. Ping, Port Tara, Cihaz Tara, ARP veya paket yakalama çalıştırın."
                    : $"«{_gecmisFiltreTur}» türünde geçmiş kaydı yok.",
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
        var ac = new Button { Content = "JSON Aç", Style = (Style)FindResource("ChipButton"), Tag = kayit.Id };
        ac.Click += (_, _) => GecmisKaydiAc(kayit);
        actions.Children.Add(ac);

        var tekrar = new Button { Content = "Tekrar Çalıştır", Style = (Style)FindResource("ChipButton"), Tag = kayit.Id };
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

    private void GecmisKaydiTekrarCalistir(HistoryRecord kayit)
    {
        _gecmisdenCalistiriliyor = true;
        switch (kayit.Type.ToUpperInvariant())
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
            case "CİHAZ TARA":
            case "CIHAZ TARA":
                MainTabControl.SelectedIndex = TabCihazTara;
                if (kayit.Metadata.TryGetValue("Subnet", out var subnet)) KameraSubnetBox.Text = subnet;
                _ = KameraTaramaBaslat();
                break;
            case "ARP":
                MainTabControl.SelectedIndex = TabChatbot;
                _ = ArpTablosuGoster();
                break;
            default:
                _gecmisdenCalistiriliyor = false;
                ToastGoster("Bu kayıt türü tekrar çalıştırılamıyor", hata: true);
                break;
        }
    }

    private void GecmisKarsilastir_Click(object sender, RoutedEventArgs e)
    {
        var cihazKayitlari = HistoryService.SonKayitlariYukle(200)
            .Where(k => k.Type.Equals("CİHAZ TARA", StringComparison.OrdinalIgnoreCase) ||
                        k.Type.Equals("CIHAZ TARA", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (cihazKayitlari.Count < 2)
        {
            ToastGoster("Karşılaştırma için en az iki Cihaz Tara kaydı gerekli", hata: true);
            return;
        }

        var yeni = GecmisCihazIpSeti(cihazKayitlari[0]);
        var eski = GecmisCihazIpSeti(cihazKayitlari[1]);
        var eklenen  = yeni.Except(eski).OrderBy(IpSiralamaAnahtari).ThenBy(x => x).ToList();
        var kaybolan = eski.Except(yeni).OrderBy(IpSiralamaAnahtari).ThenBy(x => x).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Cihaz Tara karşılaştırması");
        sb.AppendLine($"Yeni : {cihazKayitlari[0].CreatedAt:yyyy-MM-dd HH:mm:ss} ({yeni.Count} cihaz)");
        sb.AppendLine($"Eski : {cihazKayitlari[1].CreatedAt:yyyy-MM-dd HH:mm:ss} ({eski.Count} cihaz)");
        sb.AppendLine();
        sb.AppendLine($"Yeni gelen ({eklenen.Count}): {(eklenen.Count == 0 ? "-" : string.Join(", ", eklenen))}");
        sb.AppendLine($"Kaybolan ({kaybolan.Count}): {(kaybolan.Count == 0 ? "-" : string.Join(", ", kaybolan))}");
        MesajEkle("sonuc", sb.ToString().TrimEnd());
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private static HashSet<string> GecmisCihazIpSeti(HistoryRecord kayit)
    {
        if (!kayit.Metadata.TryGetValue("CihazlarJson", out var json) || string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.Ordinal);

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("Cihazlar").EnumerateArray()
                .Select(e => e.TryGetProperty("Ip", out var ip) ? ip.GetString() : null)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .ToHashSet(StringComparer.Ordinal)!;
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
