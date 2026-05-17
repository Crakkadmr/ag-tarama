using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Yakalama başlat / durdur ────────────────────────────────────
    private string _sonPcap = string.Empty;

    private async Task YakalamaBaslat()
    {
        if (_taramaDevamEdiyor) return;

        // Butonu hemen devre dışı bırak (test süresi boyunca da)
        BtnTaramaBaslat.IsEnabled = false;
        BtnTemizle.IsEnabled      = false;

        MesajEkle("sistem", "Ağ arayüzleri tespit ediliyor...");

        List<ArayuzBilgi> tumArayuzlar;
        try
        {
            tumArayuzlar = await InterfaceDiscoveryService.TumunuGetirAsync();
        }
        catch (Exception ex)
        {
            HataBildir("tshark -D başarısız", ex);
            BtnTaramaBaslat.IsEnabled = true;
            BtnTemizle.IsEnabled      = true;
            return;
        }

        if (tumArayuzlar.Count == 0)
        {
            HataBildir("Hiçbir ağ arayüzü bulunamadı. Npcap kurulu mu?");
            BtnTaramaBaslat.IsEnabled = true;
            BtnTemizle.IsEnabled      = true;
            return;
        }

        MesajEkle("sistem", $"{tumArayuzlar.Count} arayüz bulundu — {TestSuresiSn}s trafik testi yapılıyor...");
        var sayilar  = await Task.WhenAll(
            tumArayuzlar.Select(a => InterfaceDiscoveryService.PaketSayisiAsync(a.No, TestSuresiSn)));
        var aktifler = tumArayuzlar
            .Zip(sayilar)
            .Where(x => x.Second > 0)
            .Select(x => (Bilgi: x.First, Paket: x.Second))
            .ToList();

        if (aktifler.Count == 0)
        {
            HataBildir("Hiçbir arayüzde trafik algılanamadı. Ağ bağlantısı aktif mi?");
            BtnTaramaBaslat.IsEnabled = true;
            BtnTemizle.IsEnabled      = true;
            return;
        }

        MesajEkle("sonuc", $"✔ {aktifler.Count}/{tumArayuzlar.Count} aktif arayüz tespit edildi:");

        // Kullanıcı seçimini bekle
        var secilenNolar = await ArayuzSecimAsync(aktifler);
        if (secilenNolar.Count == 0)
        {
            BtnTaramaBaslat.IsEnabled = true;
            BtnTemizle.IsEnabled      = true;
            return;
        }

        Directory.CreateDirectory(Paths.CapturesKlasor);
        var dosyaAdi  = $"analiz_{DateTime.Now:ddMMyyyy_HH_mm}.pcap";
        var pcapDosya = Path.Combine(Paths.CapturesKlasor, dosyaAdi);
        _sonPcap      = pcapDosya;

        TaramaDurumunuAyarla(true);
        // MasterCts'e bağlı — LisansIptalEt() çağrıldığında yakalama da iptal edilir
        _taramaCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);

        var (kart, kartGuncelle, kartTamamla, kartDurdur) = YakalamaKartiOlustur(dosyaAdi);
        ChatPanel.Children.Add(kart);
        if (ChatSondaMi())
            Dispatcher.InvokeAsync(
                () => ChatScrollViewer.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Loaded);

        void Ilerleme(double mb, int paket, TimeSpan sure)
        {
            kartGuncelle(mb, paket, sure);
            StatusText.Text = $"● Yakalanıyor... {mb:F2} / {HedefMB} MB";
        }

        try
        {
            await _captureService.YakalaAsync(
                secilenNolar, pcapDosya, HedefKB, Ilerleme, _taramaCts.Token);

            if (!_taramaCts.IsCancellationRequested)
            {
                var boyutMB = File.Exists(pcapDosya)
                    ? new FileInfo(pcapDosya).Length / (1024.0 * 1024.0) : 0;
                kartTamamla(boyutMB, _captureService.PaketSayisi);
                HistoryService.Kaydet(
                    "YAKALAMA",
                    string.Join(",", secilenNolar),
                    $"{_captureService.PaketSayisi} paket, {boyutMB:F2} MB",
                    new[] { pcapDosya },
                    new Dictionary<string, string>
                    {
                        ["Dosya"]   = pcapDosya,
                        ["Paket"]   = _captureService.PaketSayisi.ToString(),
                        ["BoyutMB"] = boyutMB.ToString("F2"),
                    });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
                TaramaDurumunuAyarla(false);
            }
            else
            {
                kartDurdur();
            }
        }
        catch (Exception ex)
        {
            HataBildir("Yakalama hatası", ex);
            TaramaDurumunuAyarla(false);
        }
    }

    // ─── Görsel yakalama kartı ───────────────────────────────────────
    private (Border Kart,
             Action<double, int, TimeSpan> Guncelle,
             Action<double, int> Tamamla,
             Action Durdur)
        YakalamaKartiOlustur(string dosyaAdi)
    {
        // ── Renkler ──
        var maviBrush  = new SolidColorBrush(Color.FromRgb(31,  111, 235));
        var acikMavi   = new SolidColorBrush(Color.FromRgb(88,  166, 255));
        var yesil      = new SolidColorBrush(Color.FromRgb(63,  185,  80));
        var koyu       = new SolidColorBrush(Color.FromRgb(10,   18,  32));
        var gri        = new SolidColorBrush(Color.FromRgb(139, 148, 158));
        var cizgiBrush = new SolidColorBrush(Color.FromRgb(22,   30,  40));
        var kirmizi    = new SolidColorBrush(Color.FromRgb(248,  81,  73));

        // ── Ana kart ──
        var kart = new Border
        {
            Background      = koyu,
            BorderBrush     = maviBrush,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(16, 12, 16, 14),
            Margin          = new Thickness(0, 4, 0, 8),
        };
        var stack = new StackPanel();

        // ── Başlık satırı ──
        var baslikPanel = new Grid();
        baslikPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        baslikPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var baslik = new TextBlock
        {
            Text       = "  YAKALAMA",
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 12,
            FontWeight = FontWeights.Bold,
            Foreground = acikMavi,
        };
        Grid.SetColumn(baslik, 0);

        var hedefText = new TextBlock
        {
            Text       = $"Hedef: {HedefMB} MB",
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 11,
            Foreground = gri,
        };
        Grid.SetColumn(hedefText, 1);

        baslikPanel.Children.Add(baslik);
        baslikPanel.Children.Add(hedefText);
        stack.Children.Add(baslikPanel);

        // ── Dosya adı ──
        stack.Children.Add(new TextBlock
        {
            Text       = $"  {dosyaAdi}",
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
            Margin     = new Thickness(0, 2, 0, 10),
        });

        // ── Progress bar ──
        var barKonteyner = new Border
        {
            Background   = cizgiBrush,
            CornerRadius = new CornerRadius(5),
            Height       = 16,
            Margin       = new Thickness(0, 0, 0, 10),
        };
        var barGrid = new Grid();
        var dolCol  = new ColumnDefinition { Width = new GridLength(0,   GridUnitType.Star) };
        var bosCol  = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
        barGrid.ColumnDefinitions.Add(dolCol);
        barGrid.ColumnDefinitions.Add(bosCol);

        var dolgu = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(31, 111, 235),
                Color.FromRgb(88, 166, 255),
                new System.Windows.Point(0, 0.5),
                new System.Windows.Point(1, 0.5)),
            CornerRadius = new CornerRadius(5, 0, 0, 5),
        };
        Grid.SetColumn(dolgu, 0);
        barGrid.Children.Add(dolgu);

        var yuzdeText = new TextBlock
        {
            Text                = "0%",
            FontFamily          = new FontFamily("Consolas"),
            FontSize            = 10,
            FontWeight          = FontWeights.Bold,
            Foreground          = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        Grid.SetColumnSpan(yuzdeText, 2);
        barGrid.Children.Add(yuzdeText);

        barKonteyner.Child = barGrid;
        stack.Children.Add(barKonteyner);

        // ── İstatistik satırı ──
        var statsPanel = new Grid();
        statsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock StatBlok(string etiket, string deger, int sutun)
        {
            var p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            p.Children.Add(new TextBlock { Text = etiket, FontFamily = new FontFamily("Consolas"), FontSize = 9,  Foreground = gri,     HorizontalAlignment = HorizontalAlignment.Center });
            var val = new TextBlock     { Text = deger,   FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = acikMavi, HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeights.Bold };
            p.Children.Add(val);
            var b = new Border { Child = p, BorderBrush = cizgiBrush, BorderThickness = sutun == 1 ? new Thickness(1, 0, 1, 0) : new Thickness(0), Padding = new Thickness(0, 4, 0, 0) };
            Grid.SetColumn(b, sutun);
            statsPanel.Children.Add(b);
            return val;
        }

        var mbVal    = StatBlok("BOYUT", "0.00 MB", 0);
        var paketVal = StatBlok("PAKET", "0",       1);
        var sureVal  = StatBlok("SÜRE",  "00:00",   2);

        stack.Children.Add(statsPanel);
        kart.Child = stack;

        // ── Güncelleme delegeleri ──
        void Guncelle(double mb, int paket, TimeSpan sure)
        {
            double yuzde = Math.Min(mb / HedefMB * 100.0, 100.0);
            dolCol.Width    = new GridLength(yuzde,       GridUnitType.Star);
            bosCol.Width    = new GridLength(100 - yuzde, GridUnitType.Star);
            dolgu.CornerRadius = yuzde >= 99.5
                ? new CornerRadius(5)
                : new CornerRadius(5, 0, 0, 5);
            yuzdeText.Text  = $"{yuzde:F0}%";
            mbVal.Text      = $"{mb:F2} MB";
            paketVal.Text   = paket.ToString("N0");
            sureVal.Text    = sure.ToString(@"mm\:ss");
        }

        void Tamamla(double mb, int paket)
        {
            baslik.Text           = "  YAKALAMA TAMAMLANDI";
            baslik.Foreground     = yesil;
            kart.BorderBrush      = yesil;
            dolgu.Background      = new LinearGradientBrush(Color.FromRgb(26, 127, 55), Color.FromRgb(63, 185, 80),
                                        new System.Windows.Point(0, 0.5), new System.Windows.Point(1, 0.5));
            dolCol.Width          = new GridLength(100, GridUnitType.Star);
            bosCol.Width          = new GridLength(0,   GridUnitType.Star);
            dolgu.CornerRadius    = new CornerRadius(5);
            yuzdeText.Text        = "100%";
            mbVal.Text            = $"{mb:F2} MB";
            paketVal.Text         = paket.ToString("N0");
            mbVal.Foreground      = yesil;
            paketVal.Foreground   = yesil;
            StatusText.Text       = "● Hazır";

            var wsBtn = new Button
            {
                Content         = "⬡  Wireshark'ta Aç",
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 11,
                Padding         = new Thickness(12, 7, 12, 7),
                Margin          = new Thickness(0, 10, 0, 0),
                Background      = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground      = acikMavi,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand,
            };
            UygulaButonSablon(wsBtn);
            wsBtn.Click += (_, _) => WiresharkIleAc(_sonPcap);
            stack.Children.Add(wsBtn);
        }

        void Durdur()
        {
            baslik.Text       = "  YAKALAMA DURDURULDU";
            baslik.Foreground = kirmizi;
            kart.BorderBrush  = kirmizi;
        }

        return (kart, Guncelle, Tamamla, Durdur);
    }

    private void YakalamaDurdur()
    {
        _taramaCts?.Cancel();
        _captureService.Durdur();
        MesajEkle("hata", "Tarama kullanıcı tarafından durduruldu.");
        TaramaDurumunuAyarla(false);
    }

    private void WiresharkIleAc(string pcap)
    {
        try
        {
            if (!File.Exists(pcap)) { HataBildir("Dosya henüz oluşmadı."); return; }
            if (!File.Exists(Paths.WiresharkPortableExe))
            { HataBildir($"WiresharkPortable64.exe bulunamadı:\n{Paths.WiresharkPortableExe}"); return; }

            Process.Start(new ProcessStartInfo(Paths.WiresharkPortableExe, $"\"{pcap}\"")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            HataBildir("Wireshark Portable açılamadı", ex);
        }
    }
}
