using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace AgTarama;

public partial class MainWindow : Window
{
    // ─── Durum ───────────────────────────────────────────────────────
    private bool _taramaDevamEdiyor = false;
    private CancellationTokenSource? _taramaCts;
    private Process? _tsharkProc;

    // ─── Ping paneli ─────────────────────────────────────────────────
    private bool _pingPanelAcik = false;
    private CancellationTokenSource? _pingCts;
    private const double PingPanelGenisligi = 340;

    // ─── Sabit yollar (AppBase = exe'nin bulunduğu klasör) ───────────
    // Single-file publish'te AppDomain.BaseDirectory geçici klasörü gösterir;
    // ProcessPath her zaman gerçek exe konumunu verir.
    private static readonly string AppBase =
        Path.GetDirectoryName(Environment.ProcessPath)
        ?? AppDomain.CurrentDomain.BaseDirectory;

    private static readonly string NpcapInstaller =
        Path.Combine(AppBase, "Req", "npcap-1.88.exe");

    private static readonly string TsharkExe =
        Path.Combine(AppBase, "tools", "WiresharkPortable64", "App", "Wireshark", "tshark.exe");

    private static readonly string WiresharkPortableExe =
        Path.Combine(AppBase, "tools", "WiresharkPortable64", "WiresharkPortable64.exe");

    // ─── Başlangıç ───────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        MesajEkle("sistem", "Ağ Tarama Programı başlatıldı.");
        _ = BaslangicAsync();
    }

    private async Task BaslangicAsync()
    {
        if (!await NpcapKontrolVeKur()) return;
        MesajEkle("sonuc", "✔ Sistem hazır — sağ panelden taramayı başlatın.");
    }

    // ─── Npcap kontrol ve sessiz kurulum ─────────────────────────────
    private static bool NpcapKurulumu()
    {
        using var k32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Npcap");
        using var k64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Npcap");
        return k32 != null || k64 != null;
    }

    private async Task<bool> NpcapKontrolVeKur()
    {
        if (NpcapKurulumu())
        {
            MesajEkle("sonuc", "✔ Npcap kurulu.");
            return true;
        }

        if (!File.Exists(NpcapInstaller))
        {
            MesajEkle("hata", $"Npcap kurulu değil ve installer bulunamadı:\n{NpcapInstaller}");
            return false;
        }

        MesajEkle("sistem", "Npcap kurulu değil — sessiz kurulum başlatılıyor (yönetici izni istenebilir)...");

        try
        {
            // /S = NSIS silent flag; yönetici hakları için Verb = "runas"
            var psi = new ProcessStartInfo(NpcapInstaller, "/S")
            {
                UseShellExecute = true,
                Verb            = "runas",
            };
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();

            if (NpcapKurulumu())
            {
                MesajEkle("sonuc", "✔ Npcap başarıyla kuruldu.");
                return true;
            }
            else
            {
                MesajEkle("hata", "Npcap kurulumu tamamlandı ama kayıt defterinde bulunamadı. Bilgisayarı yeniden başlatın.");
                return false;
            }
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"Npcap kurulum hatası: {ex.Message}");
            return false;
        }
    }

    // ─── Tüm arayüzleri al (numara + kısa ad) ───────────────────────
    private record ArayuzBilgi(string No, string Ad);

    private async Task<List<ArayuzBilgi>> TumArayuzlariGetirAsync()
    {
        var liste = new List<ArayuzBilgi>();
        try
        {
            var psi = new ProcessStartInfo(TsharkExe, "-D")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi)!;
            var cikti = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            // "1. \Device\NPF_{GUID} (Wi-Fi)" → No="1", Ad="Wi-Fi"
            foreach (var satir in cikti.Split('\n'))
            {
                var t = satir.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                var noktaIdx = t.IndexOf('.');
                if (noktaIdx < 0) continue;
                var no = t[..noktaIdx].Trim();
                var parcaIdx = t.IndexOf('(');
                var ad = parcaIdx >= 0
                    ? t[(parcaIdx + 1)..].TrimEnd(')', ' ')
                    : t[(noktaIdx + 2)..].Trim();
                if (ad.Length > 32) ad = ad[..29] + "…";
                liste.Add(new ArayuzBilgi(no, ad));
            }
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"tshark -D başarısız: {ex.Message}");
        }
        return liste;
    }

    // Seçili arayüzleri toggle butonlarla göster; kullanıcı onaylayana kadar bekle
    private Task<List<string>> ArayuzSecimAsync(List<(ArayuzBilgi Bilgi, int Paket)> aktifler)
    {
        var tcs     = new TaskCompletionSource<List<string>>();
        var secili  = new HashSet<string>();   // başta hiçbiri seçili değil

        var karti = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(10, 18, 32)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(14, 12, 14, 12),
            Margin          = new Thickness(0, 4, 0, 8),
        };
        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text       = "Dinlenecek arayüzleri seçin:",
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
        });

        var satirPanel = new WrapPanel { Orientation = Orientation.Horizontal };

        // Başlat butonu — önceden oluştur, sonra listeye ekle
        var baslat = new Button
        {
            Content         = "▶  Dinlemeyi Başlat",
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 12,
            FontWeight      = FontWeights.Bold,
            Padding         = new Thickness(16, 8, 16, 8),
            Margin          = new Thickness(0, 10, 0, 0),
            Background      = new SolidColorBrush(Color.FromRgb(26, 74, 46)),
            Foreground      = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(35, 134, 54)),
            BorderThickness = new Thickness(1),
            Cursor          = Cursors.Hand,
            IsEnabled       = false,   // en az 1 seçilene kadar pasif
        };
        UygulaButonSablon(baslat);

        foreach (var (bilgi, paket) in aktifler)
        {
            var no  = bilgi.No;
            var btn = new Button
            {
                Tag             = no,
                Content         = $"  {bilgi.Ad}  ({paket} pkt)",
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 12,
                Padding         = new Thickness(12, 7, 12, 7),
                Margin          = new Thickness(0, 0, 8, 8),
                Background      = new SolidColorBrush(Color.FromRgb(22, 27, 34)),   // pasif
                Foreground      = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand,
            };
            UygulaButonSablon(btn);

            btn.Click += (_, _) =>
            {
                if (secili.Remove(no))
                {   // seçimden çıkar → pasif
                    btn.Background  = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                    btn.Foreground  = new SolidColorBrush(Color.FromRgb(72, 79, 88));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61));
                }
                else
                {   // seçili yap → mavi
                    secili.Add(no);
                    btn.Background  = new SolidColorBrush(Color.FromRgb(13, 59, 102));
                    btn.Foreground  = new SolidColorBrush(Color.FromRgb(88, 166, 255));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(31, 111, 235));
                }
                baslat.IsEnabled = secili.Count > 0;
            };
            satirPanel.Children.Add(btn);
        }

        baslat.Click += (_, _) =>
        {
            karti.IsEnabled = false;
            tcs.TrySetResult(secili.ToList());
        };

        stack.Children.Add(satirPanel);
        stack.Children.Add(baslat);
        karti.Child = stack;
        ChatPanel.Children.Add(karti);
        ChatScrollViewer.ScrollToEnd();

        return tcs.Task;
    }

    // Buton köşe yuvarlama şablonu
    private static void UygulaButonSablon(Button btn)
    {
        var t    = new ControlTemplate(typeof(Button));
        var bd   = new FrameworkElementFactory(typeof(Border));
        bd.SetBinding(Border.BackgroundProperty,      new System.Windows.Data.Binding("Background")      { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.BorderBrushProperty,     new System.Windows.Data.Binding("BorderBrush")     { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.PaddingProperty,         new System.Windows.Data.Binding("Padding")         { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
        bd.AppendChild(cp);
        t.VisualTree = bd;
        btn.Template = t;
    }

    // 2 saniyelik test; tshark son satırda "N packets captured" yazar
    private static async Task<int> ArayuzPaketSayisiAsync(string no)
    {
        try
        {
            var psi = new ProcessStartInfo(
                TsharkExe,
                $"-i {no} -a duration:2 -q")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi)!;
            // tshark özet istatistiği stderr'e yazar
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            // Örnek satır: "4 packets captured"
            foreach (var satir in stderr.Split('\n'))
            {
                var s = satir.Trim();
                if (s.EndsWith("packets captured", StringComparison.OrdinalIgnoreCase))
                {
                    var parca = s.Split(' ')[0];
                    if (int.TryParse(parca, out int n)) return n;
                }
            }
        }
        catch { /* arayüz erişilemez */ }
        return 0;
    }

    // ─── Yakalama başlat / durdur ────────────────────────────────────
    private const int TestSuresiSn = 2;
    private const int HedefMB      = 16;
    private const int HedefKB      = HedefMB * 1024;

    private int    _paketSayisi = 0;
    private string _sonPcap     = string.Empty;

    private async Task YakalamaBaslat()
    {
        if (_taramaDevamEdiyor) return;

        // Butonu hemen devre dışı bırak (test süresi boyunca da)
        BtnTaramaBaslat.IsEnabled = false;

        MesajEkle("sistem", "Ağ arayüzleri tespit ediliyor...");
        var tumArayuzlar = await TumArayuzlariGetirAsync();

        if (tumArayuzlar.Count == 0)
        {
            MesajEkle("hata", "Hiçbir ağ arayüzü bulunamadı. Npcap kurulu mu?");
            BtnTaramaBaslat.IsEnabled = true;
            return;
        }

        MesajEkle("sistem", $"{tumArayuzlar.Count} arayüz bulundu — {TestSuresiSn}s trafik testi yapılıyor...");
        var sayilar  = await Task.WhenAll(tumArayuzlar.Select(a => ArayuzPaketSayisiAsync(a.No)));
        var aktifler = tumArayuzlar
            .Zip(sayilar)
            .Where(x => x.Second > 0)
            .Select(x => (Bilgi: x.First, Paket: x.Second))
            .ToList();

        if (aktifler.Count == 0)
        {
            MesajEkle("hata", "Hiçbir arayüzde trafik algılanamadı. Ağ bağlantısı aktif mi?");
            BtnTaramaBaslat.IsEnabled = true;
            return;
        }

        MesajEkle("sonuc", $"✔ {aktifler.Count}/{tumArayuzlar.Count} aktif arayüz tespit edildi:");

        // Kullanıcı seçimini bekle
        var secilenNolar = await ArayuzSecimAsync(aktifler);
        if (secilenNolar.Count == 0)
        {
            BtnTaramaBaslat.IsEnabled = true;
            return;
        }

        var pcapKlasor = Path.Combine(AppBase, "captures");
        Directory.CreateDirectory(pcapKlasor);
        var dosyaAdi  = $"analiz_{DateTime.Now:ddMMyyyy_HH_mm}.pcap";
        var pcapDosya = Path.Combine(pcapKlasor, dosyaAdi);
        _sonPcap      = pcapDosya;
        var iArgs     = string.Join(" ", secilenNolar.Select(n => $"-i {n}"));

        TaramaDurumunuAyarla(true);   // BtnTaramaBaslat.IsEnabled = false zaten buradan gelir
        _taramaCts   = new CancellationTokenSource();
        _paketSayisi = 0;

        var (kart, kartGuncelle, kartTamamla, kartDurdur) = YakalamaKartiOlustur(dosyaAdi);
        ChatPanel.Children.Add(kart);
        ChatScrollViewer.ScrollToEnd();

        try
        {
            var psi = new ProcessStartInfo(
                TsharkExe,
                $"{iArgs} -w \"{pcapDosya}\" -a filesize:{HedefKB} -P")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            _tsharkProc = Process.Start(psi);
            if (_tsharkProc == null)
            {
                MesajEkle("hata", "tshark başlatılamadı.");
                TaramaDurumunuAyarla(false);
                return;
            }

            _ = Task.Run(async () =>
            {
                while (!_tsharkProc.StandardOutput.EndOfStream)
                {
                    await _tsharkProc.StandardOutput.ReadLineAsync();
                    Interlocked.Increment(ref _paketSayisi);
                }
            });

            await DosyaIzleAsync(pcapDosya, kartGuncelle, _taramaCts.Token);

            if (!_taramaCts.IsCancellationRequested)
            {
                try { await _tsharkProc.WaitForExitAsync(); } catch { }
                var boyutMB = File.Exists(pcapDosya) ? new FileInfo(pcapDosya).Length / (1024.0 * 1024.0) : 0;
                kartTamamla(boyutMB, _paketSayisi);
                TaramaDurumunuAyarla(false);
            }
            else
            {
                kartDurdur();
            }
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"Yakalama hatası: {ex.Message}");
            TaramaDurumunuAyarla(false);
        }
    }

    // Her 500 ms dosya boyutunu ölçer, kartı günceller
    private async Task DosyaIzleAsync(string pcap, Action<double, int, TimeSpan> guncelle, CancellationToken token)
    {
        var baslangic = DateTime.Now;
        while (!token.IsCancellationRequested && (_tsharkProc?.HasExited == false))
        {
            try
            {
                double mb   = File.Exists(pcap) ? new FileInfo(pcap).Length / (1024.0 * 1024.0) : 0;
                var    sure = DateTime.Now - baslangic;
                guncelle(mb, _paketSayisi, sure);
                StatusText.Text = $"● Yakalanıyor... {mb:F2} / {HedefMB} MB";
            }
            catch { }
            try { await Task.Delay(500, token); } catch (TaskCanceledException) { break; }
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
        var maviBrush    = new SolidColorBrush(Color.FromRgb(31,  111, 235));
        var acikMavi     = new SolidColorBrush(Color.FromRgb(88,  166, 255));
        var yesil        = new SolidColorBrush(Color.FromRgb(63,  185,  80));
        var koyu         = new SolidColorBrush(Color.FromRgb(10,   18,  32));
        var gri          = new SolidColorBrush(Color.FromRgb(139, 148, 158));
        var cizgiBrush   = new SolidColorBrush(Color.FromRgb(22,   30,  40));
        var kirmizi      = new SolidColorBrush(Color.FromRgb(248,  81,  73));

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
            Background      = cizgiBrush,
            CornerRadius    = new CornerRadius(5),
            Height          = 16,
            Margin          = new Thickness(0, 0, 0, 10),
        };
        var barGrid = new Grid();
        var dolCol  = new ColumnDefinition { Width = new GridLength(0,   GridUnitType.Star) };
        var bosCol  = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
        barGrid.ColumnDefinitions.Add(dolCol);
        barGrid.ColumnDefinitions.Add(bosCol);

        var dolgu = new Border
        {
            Background   = new LinearGradientBrush(
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
            p.Children.Add(new TextBlock { Text = etiket, FontFamily = new FontFamily("Consolas"), FontSize = 9,  Foreground = gri,      HorizontalAlignment = HorizontalAlignment.Center });
            var val = new TextBlock     { Text = deger,   FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = acikMavi,  HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeights.Bold };
            p.Children.Add(val);
            var b = new Border { Child = p, BorderBrush = cizgiBrush, BorderThickness = sutun == 1 ? new Thickness(1, 0, 1, 0) : new Thickness(0), Padding = new Thickness(0, 4, 0, 0) };
            Grid.SetColumn(b, sutun);
            statsPanel.Children.Add(b);
            return val;
        }

        var mbVal     = StatBlok("BOYUT",   "0.00 MB", 0);
        var paketVal  = StatBlok("PAKET",   "0",       1);
        var sureVal   = StatBlok("SÜRE",    "00:00",   2);

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
        try { _tsharkProc?.Kill(entireProcessTree: true); } catch { }
        _tsharkProc = null;
        MesajEkle("hata", "Tarama kullanıcı tarafından durduruldu.");
        TaramaDurumunuAyarla(false);
    }

    private void WiresharkIleAc(string pcap)
    {
        try
        {
            if (!File.Exists(pcap)) { MesajEkle("hata", "Dosya henüz oluşmadı."); return; }
            if (!File.Exists(WiresharkPortableExe))
            { MesajEkle("hata", $"WiresharkPortable64.exe bulunamadı:\n{WiresharkPortableExe}"); return; }

            // WiresharkPortable64.exe pcap dosyasını argüman olarak alır
            Process.Start(new ProcessStartInfo(WiresharkPortableExe, $"\"{pcap}\"")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"Wireshark Portable açılamadı: {ex.Message}");
        }
    }

    // ─── UI yardımcıları ──────────────────────────────────────────────

    // Mesaj türleri: "sistem" | "kullanici" | "sonuc" | "hata"
    public void MesajEkle(string tur, string metin)
    {
        var satir = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding      = new Thickness(12, 8, 12, 8),
            Margin       = new Thickness(0, 3, 0, 3),
        };

        var txt = new TextBlock
        {
            Text        = metin,
            FontFamily  = new FontFamily("Consolas"),
            FontSize    = 13,
            TextWrapping = TextWrapping.Wrap,
        };

        switch (tur)
        {
            case "kullanici":
                satir.Background        = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                satir.BorderBrush       = new SolidColorBrush(Color.FromRgb(48, 54, 61));
                satir.BorderThickness   = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Right;
                satir.MaxWidth          = 500;
                txt.Foreground          = new SolidColorBrush(Color.FromRgb(201, 209, 217));
                txt.Text                = "› " + metin;
                break;

            case "sonuc":
                satir.Background        = new SolidColorBrush(Color.FromRgb(13, 59, 102));
                satir.BorderBrush       = new SolidColorBrush(Color.FromRgb(31, 111, 235));
                satir.BorderThickness   = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground          = new SolidColorBrush(Color.FromRgb(88, 166, 255));
                break;

            case "hata":
                satir.Background        = new SolidColorBrush(Color.FromRgb(61, 26, 26));
                satir.BorderBrush       = new SolidColorBrush(Color.FromRgb(139, 26, 26));
                satir.BorderThickness   = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground          = new SolidColorBrush(Color.FromRgb(248, 81, 73));
                txt.Text                = "✖ " + metin;
                break;

            default: // sistem
                satir.Background        = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                satir.BorderBrush       = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                satir.BorderThickness   = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground          = new SolidColorBrush(Color.FromRgb(139, 148, 158));
                txt.Text                = "◆ " + metin;
                break;
        }

        satir.Child = txt;
        ChatPanel.Children.Add(satir);

        var zaman = new TextBlock
        {
            Text      = DateTime.Now.ToString("HH:mm:ss"),
            FontFamily = new FontFamily("Consolas"),
            FontSize  = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
            HorizontalAlignment = tur == "kullanici"
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left,
            Margin = new Thickness(4, 0, 4, 4),
        };
        ChatPanel.Children.Add(zaman);
        ChatScrollViewer.ScrollToEnd();
    }

    private void TaramaDurumunuAyarla(bool devamEdiyor)
    {
        _taramaDevamEdiyor = devamEdiyor;
        BtnTaramaBaslat.IsEnabled = !devamEdiyor;
        BtnTaramaDurdur.IsEnabled  = devamEdiyor;
        BtnTemizle.IsEnabled       = !devamEdiyor;
        StatusText.Text     = devamEdiyor ? "● Yakalanıyor..." : "● Hazır";
        StatusText.Foreground = devamEdiyor
            ? new SolidColorBrush(Color.FromRgb(210, 153, 34))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
    }

    // ─── Sağ panel buton olayları ────────────────────────────────────
    private void BtnTaramaBaslat_Click(object sender, RoutedEventArgs e)
        => _ = YakalamaBaslat();

    private void BtnTaramaDurdur_Click(object sender, RoutedEventArgs e)
        => YakalamaDurdur();

    private void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        if (_pingPanelAcik)
            PingPanelKapatAnimasyon();
        else
            PingPanelAcAnimasyon();
    }

    // ─── Ping paneli animasyonları ───────────────────────────────────
    private void PingPanelAcAnimasyon()
    {
        _pingPanelAcik = true;
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (s, _) =>
        {
            double mevcut = PingCol.Width.Value;
            double kalan  = PingPanelGenisligi - mevcut;
            double adim   = Math.Max(kalan * 0.28, 2);
            double yeni   = mevcut + adim;
            if (yeni >= PingPanelGenisligi - 1)
            {
                PingCol.Width = new GridLength(PingPanelGenisligi);
                ((System.Windows.Threading.DispatcherTimer)s!).Stop();
                PingIpBox.Focus();
            }
            else
            {
                PingCol.Width = new GridLength(yeni);
            }
        };
        timer.Start();
    }

    private void PingPanelKapatAnimasyon()
    {
        _pingPanelAcik = false;
        _pingCts?.Cancel();
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (s, _) =>
        {
            double mevcut = PingCol.Width.Value;
            double adim   = Math.Max(mevcut * 0.28, 2);
            double yeni   = mevcut - adim;
            if (yeni <= 1)
            {
                PingCol.Width = new GridLength(0);
                ((System.Windows.Threading.DispatcherTimer)s!).Stop();
            }
            else
            {
                PingCol.Width = new GridLength(yeni);
            }
        };
        timer.Start();
    }

    // ─── IP / hostname doğrulama ─────────────────────────────────────
    private static bool GecerliIpv4Mu(string s)
    {
        var parcalar = s.Split('.');
        if (parcalar.Length != 4) return false;
        foreach (var p in parcalar)
        {
            if (p.Length == 0 || p.Length > 3) return false;
            if (!int.TryParse(p, out int v) || v < 0 || v > 255) return false;
        }
        return true;
    }

    private static bool GecerliHostnameMu(string s)
        => s.Length > 0 && Regex.IsMatch(s, @"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$");

    // ─── Ping paneli event'leri ──────────────────────────────────────
    private void PingIpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var metin = PingIpBox.Text.Trim();
        PingPlaceholder.Visibility = string.IsNullOrEmpty(PingIpBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(metin))
        {
            PingValidasyonIkonu.Text       = "";
        }
        else if (GecerliIpv4Mu(metin))
        {
            PingValidasyonIkonu.Text       = "✓";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
        }
        else if (GecerliHostnameMu(metin))
        {
            PingValidasyonIkonu.Text       = "~";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
        }
        else
        {
            PingValidasyonIkonu.Text       = "✗";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
    }

    private void PingIpBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var hedef = PingIpBox.Text.Trim();
        if (GecerliIpv4Mu(hedef) || GecerliHostnameMu(hedef))
            _ = PingBaslat(hedef);
        else
            MesajEkle("hata", $"Geçersiz adres: \"{hedef}\" — IPv4 (örn: 192.168.1.1) veya hostname girin.");
    }

    private void PingHizliBtn_Click(object sender, RoutedEventArgs e)
    {
        var hedef = (string)((Button)sender).Tag;
        PingIpBox.Text = hedef;
        _ = PingBaslat(hedef);
    }

    private void PingPanelKapat_Click(object sender, RoutedEventArgs e)
        => PingPanelKapatAnimasyon();

    // ─── Ping işlemi ────────────────────────────────────────────────
    private void PingKutucugaYaz(string metin, string hex)
    {
        var satir = new TextBlock
        {
            Text            = metin,
            Foreground      = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 12,
            TextWrapping    = TextWrapping.Wrap,
            Margin          = new Thickness(0, 1, 0, 1),
        };
        PingResultPanel.Children.Add(satir);
        PingResultScroll.ScrollToEnd();
    }

    private async Task PingBaslat(string hedef)
    {
        _pingCts?.Cancel();
        _pingCts = new CancellationTokenSource();
        var token = _pingCts.Token;

        PingResultPanel.Children.Clear();
        PingResultBorder.Visibility = Visibility.Visible;
        PingKutucugaYaz($"◆ {hedef} → 4 paket gönderiliyor...", "#8B949E");

        int basarili = 0;
        long toplamMs = 0;

        using var ping = new Ping();
        for (int i = 1; i <= 4 && !token.IsCancellationRequested; i++)
        {
            try
            {
                var yanit = await ping.SendPingAsync(hedef, 2000);
                if (yanit.Status == IPStatus.Success)
                {
                    basarili++;
                    toplamMs += yanit.RoundtripTime;
                    PingKutucugaYaz($"[{i}/4] {yanit.RoundtripTime} ms  TTL={yanit.Options?.Ttl ?? 0}", "#58A6FF");
                }
                else
                {
                    PingKutucugaYaz($"[{i}/4] {yanit.Status}", "#F85149");
                }
            }
            catch (PingException px)
            {
                PingKutucugaYaz($"[{i}/4] {px.InnerException?.Message ?? px.Message}", "#F85149");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                PingKutucugaYaz($"[{i}/4] Hata: {ex.Message}", "#F85149");
            }

            if (i < 4 && !token.IsCancellationRequested)
                try { await Task.Delay(700, token); } catch (OperationCanceledException) { }
        }

        if (!token.IsCancellationRequested)
        {
            var kayip = 4 - basarili;
            if (basarili > 0)
            {
                PingKutucugaYaz("─────────────────────────", "#30363D");
                PingKutucugaYaz($"✔ {basarili}/4 başarılı — ort. {toplamMs / basarili} ms, {kayip} kayıp", "#3FB950");
            }
            else
            {
                PingKutucugaYaz("─────────────────────────", "#30363D");
                PingKutucugaYaz($"✖ yanıt yok (4/4 kayıp)", "#F85149");
            }
        }
    }

    private void BtnPortTara_Click(object sender, RoutedEventArgs e)
    {
        MesajEkle("sistem", "Port taraması — yakında eklenecek.");
        // TODO: Port tarama implementasyonu
    }

    private void BtnSadp_Click(object sender, RoutedEventArgs e)
    {
        var exe = Path.Combine(AppBase, "tools", "sadp", "sadptool.exe");
        if (!File.Exists(exe))
        {
            MesajEkle("hata", $"SADP aracı bulunamadı:\n{exe}");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute  = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            });
            MesajEkle("sistem", "SADP aracı başlatıldı.");
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"SADP açılamadı: {ex.Message}");
        }
    }

    private void BtnCihazlar_Click(object sender, RoutedEventArgs e)
    {
        var exe = Path.Combine(AppBase, "tools", "Ip_Scanner", "advanced_ip_scanner.exe");
        if (!File.Exists(exe))
        {
            MesajEkle("hata", $"Advanced IP Scanner bulunamadı:\n{exe}");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute  = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            });
            MesajEkle("sistem", "Advanced IP Scanner başlatıldı.");
        }
        catch (Exception ex)
        {
            MesajEkle("hata", $"Advanced IP Scanner açılamadı: {ex.Message}");
        }
    }

    private void BtnTemizle_Click(object sender, RoutedEventArgs e)
    {
        ChatPanel.Children.Clear();
        MesajEkle("sistem", "Ekran temizlendi.");
    }
}
