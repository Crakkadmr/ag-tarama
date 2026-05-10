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
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
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

    // ─── Port tarama paneli ──────────────────────────────────────────
    private bool _portPanelAcik = false;
    private CancellationTokenSource? _portScanCts;

    // ─── Traceroute / DNS / WoL / SNMP panelleri ─────────────────────
    private bool _tracePanelAcik = false;
    private CancellationTokenSource? _traceCts;
    private bool _dnsPanelAcik   = false;
    private bool _wolPanelAcik   = false;
    private bool _snmpPanelAcik  = false;
    private string _snmpVersiyon = "v2c";

    // ─── Otomatik nokta (IP giriş kutuları) ──────────────────────────
    private bool _otomatikGuncelleniyor = false;
    private readonly Dictionary<TextBox, int> _oncekiUzunluk = new();

    private static readonly Dictionary<int, string> BilindikPortlar = new()
    {
        {21,"FTP"},{22,"SSH"},{23,"Telnet"},{25,"SMTP"},{53,"DNS"},
        {80,"HTTP"},{110,"POP3"},{135,"RPC"},{139,"NetBIOS"},{143,"IMAP"},
        {443,"HTTPS"},{445,"SMB"},{554,"RTSP"},{587,"SMTP-TLS"},
        {993,"IMAPS"},{995,"POP3S"},{1433,"MSSQL"},{3306,"MySQL"},
        {3389,"RDP"},{5900,"VNC"},{8000,"HTTP-Alt"},{8080,"HTTP-Alt"},
        {8443,"HTTPS-Alt"},{37777,"Hikvision-SDK"},
    };

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

    private static readonly string LogKlasor  = Path.Combine(AppBase, "logs");
    private static readonly string LogDosyasi = Path.Combine(AppBase, "logs", "log.txt");

    // ─── Başlangıç ───────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        MesajEkle("sistem", "Ağ Tarama Programı başlatıldı.");
        _ = BaslangicAsync();
    }

    private async Task BaslangicAsync()
    {
        LogOturumBaslat();
        if (!await NpcapKontrolVeKur()) return;
        MesajEkle("sonuc", "✔ Sistem hazır — sağ panelden taramayı başlatın.");
    }

    private void LogOturumBaslat()
    {
        try
        {
            Directory.CreateDirectory(LogKlasor);
            File.AppendAllText(LogDosyasi,
                $"\n=== OTURUM: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n",
                Encoding.UTF8);
        }
        catch { }
    }

    private void LogKaydet(string kategori, string hedef, IEnumerable<string> satirlar)
    {
        try
        {
            Directory.CreateDirectory(LogKlasor);
            var sb = new StringBuilder();
            sb.AppendLine($"\n[{DateTime.Now:HH:mm:ss}] [{kategori}] {hedef}");
            foreach (var s in satirlar)
                sb.AppendLine($"  {s}");
            File.AppendAllText(LogDosyasi, sb.ToString(), Encoding.UTF8);
        }
        catch { }
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

    private void TumYanPanelleriKapat()
    {
        if (_pingPanelAcik)  { _pingPanelAcik  = false; _pingCts?.Cancel();  }
        if (_portPanelAcik)  { _portPanelAcik  = false; _portScanCts?.Cancel(); PortPanel.Visibility  = Visibility.Collapsed; }
        if (_tracePanelAcik) { _tracePanelAcik = false; _traceCts?.Cancel(); TracePanel.Visibility = Visibility.Collapsed; }
        if (_dnsPanelAcik)   { _dnsPanelAcik   = false; DnsPanel.Visibility   = Visibility.Collapsed; }
        if (_wolPanelAcik)   { _wolPanelAcik   = false; WolPanel.Visibility   = Visibility.Collapsed; }
        if (_snmpPanelAcik)  { _snmpPanelAcik  = false; SnmpPanel.Visibility  = Visibility.Collapsed; }
        PingCol.Width = new GridLength(0);
    }

    private void YanPanelAc(ref bool flag, UIElement panel)
    {
        bool ayniPanel = flag;
        TumYanPanelleriKapat();
        if (ayniPanel) return;
        flag = true;
        panel.Visibility = Visibility.Visible;
        YanPanelAcAnimasyon();
    }

    private void YanPanelAcAnimasyon()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (s, _) =>
        {
            double mevcut = PingCol.Width.Value;
            double adim   = Math.Max((PingPanelGenisligi - mevcut) * 0.28, 2);
            double yeni   = mevcut + adim;
            if (yeni >= PingPanelGenisligi - 1) { PingCol.Width = new GridLength(PingPanelGenisligi); ((System.Windows.Threading.DispatcherTimer)s!).Stop(); }
            else PingCol.Width = new GridLength(yeni);
        };
        timer.Start();
    }

    private void YanPanelKapatAnimasyon(Action onBitti)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (s, _) =>
        {
            double mevcut = PingCol.Width.Value;
            double adim   = Math.Max(mevcut * 0.28, 2);
            double yeni   = mevcut - adim;
            if (yeni <= 1) { PingCol.Width = new GridLength(0); ((System.Windows.Threading.DispatcherTimer)s!).Stop(); onBitti(); }
            else PingCol.Width = new GridLength(yeni);
        };
        timer.Start();
    }

    private void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        if (_pingPanelAcik) { YanPanelKapatAnimasyon(() => { }); _pingPanelAcik = false; return; }
        YanPanelAc(ref _pingPanelAcik, PingPanel);
        PingIpBox.Focus();
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

    private void OtomatikNoktaUygula(TextBox kutu)
    {
        if (_otomatikGuncelleniyor) return;
        int eskiUzunluk = _oncekiUzunluk.TryGetValue(kutu, out int u) ? u : 0;
        int yeniUzunluk = kutu.Text.Length;
        _oncekiUzunluk[kutu] = yeniUzunluk;
        if (yeniUzunluk <= eskiUzunluk) return;         // silme/yapıştırma kısaltma → atla
        if (kutu.CaretIndex != yeniUzunluk) return;     // imleç ortada → atla
        var parcalar = kutu.Text.Split('.');
        if (parcalar.Length >= 4) return;               // 4. oktet → nokta ekleme
        var son = parcalar.Last();
        if (son.Length == 3 && son.All(char.IsDigit))
        {
            _otomatikGuncelleniyor = true;
            kutu.Text += ".";
            kutu.CaretIndex = kutu.Text.Length;
            _oncekiUzunluk[kutu] = kutu.Text.Length;
            _otomatikGuncelleniyor = false;
        }
    }

    // ─── Ping paneli event'leri ──────────────────────────────────────
    private void PingIpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(PingIpBox);
        var metin = PingIpBox.Text.Trim();
        PingPlaceholder.Visibility = string.IsNullOrEmpty(PingIpBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(metin))
        {
            PingValidasyonIkonu.Text       = "";
            PingBaslatBtn.IsEnabled        = false;
        }
        else if (GecerliIpv4Mu(metin))
        {
            PingValidasyonIkonu.Text       = "✓";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            PingBaslatBtn.IsEnabled        = true;
        }
        else if (GecerliHostnameMu(metin))
        {
            PingValidasyonIkonu.Text       = "~";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
            PingBaslatBtn.IsEnabled        = true;
        }
        else
        {
            PingValidasyonIkonu.Text       = "✗";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
            PingBaslatBtn.IsEnabled        = false;
        }
    }

    private void PingBaslatBtn_Click(object sender, RoutedEventArgs e)
    {
        var hedef = PingIpBox.Text.Trim();
        if (GecerliIpv4Mu(hedef) || GecerliHostnameMu(hedef))
            _ = PingBaslat(hedef);
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
        var logSatirlari = new List<string>();

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
                    var s = $"[{i}/4] {yanit.RoundtripTime} ms  TTL={yanit.Options?.Ttl ?? 0}";
                    PingKutucugaYaz(s, "#58A6FF");
                    logSatirlari.Add(s);
                }
                else
                {
                    var s = $"[{i}/4] {yanit.Status}";
                    PingKutucugaYaz(s, "#F85149");
                    logSatirlari.Add(s);
                }
            }
            catch (PingException px)
            {
                var s = $"[{i}/4] {px.InnerException?.Message ?? px.Message}";
                PingKutucugaYaz(s, "#F85149");
                logSatirlari.Add(s);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var s = $"[{i}/4] Hata: {ex.Message}";
                PingKutucugaYaz(s, "#F85149");
                logSatirlari.Add(s);
            }

            if (i < 4 && !token.IsCancellationRequested)
                try { await Task.Delay(700, token); } catch (OperationCanceledException) { }
        }

        if (!token.IsCancellationRequested)
        {
            var kayip = 4 - basarili;
            string ozet;
            if (basarili > 0)
            {
                PingKutucugaYaz("─────────────────────────", "#30363D");
                ozet = $"✔ {basarili}/4 başarılı — ort. {toplamMs / basarili} ms, {kayip} kayıp";
                PingKutucugaYaz(ozet, "#3FB950");
            }
            else
            {
                PingKutucugaYaz("─────────────────────────", "#30363D");
                ozet = "✖ yanıt yok (4/4 kayıp)";
                PingKutucugaYaz(ozet, "#F85149");
            }
            logSatirlari.Add(ozet);
            LogKaydet("PING", hedef, logSatirlari);
        }
    }

    private void BtnPortTara_Click(object sender, RoutedEventArgs e)
    {
        if (_portPanelAcik) { YanPanelKapatAnimasyon(() => { _portScanCts?.Cancel(); PortPanel.Visibility = Visibility.Collapsed; }); _portPanelAcik = false; return; }
        YanPanelAc(ref _portPanelAcik, PortPanel);
        PortIpBox.Focus();
    }

    private void BtnTrace_Click(object sender, RoutedEventArgs e)
    {
        if (_tracePanelAcik) { YanPanelKapatAnimasyon(() => { _traceCts?.Cancel(); TracePanel.Visibility = Visibility.Collapsed; }); _tracePanelAcik = false; return; }
        YanPanelAc(ref _tracePanelAcik, TracePanel);
        TraceHedefBox.Focus();
    }

    private void BtnDns_Click(object sender, RoutedEventArgs e)
    {
        if (_dnsPanelAcik) { YanPanelKapatAnimasyon(() => DnsPanel.Visibility = Visibility.Collapsed); _dnsPanelAcik = false; return; }
        YanPanelAc(ref _dnsPanelAcik, DnsPanel);
        DnsHedefBox.Focus();
    }

    private void BtnWol_Click(object sender, RoutedEventArgs e)
    {
        if (_wolPanelAcik) { YanPanelKapatAnimasyon(() => WolPanel.Visibility = Visibility.Collapsed); _wolPanelAcik = false; return; }
        YanPanelAc(ref _wolPanelAcik, WolPanel);
        WolMacBox.Focus();
    }

    private void BtnSnmp_Click(object sender, RoutedEventArgs e)
    {
        if (_snmpPanelAcik) { YanPanelKapatAnimasyon(() => SnmpPanel.Visibility = Visibility.Collapsed); _snmpPanelAcik = false; return; }
        YanPanelAc(ref _snmpPanelAcik, SnmpPanel);
        SnmpIpBox.Focus();
    }

    private void BtnArp_Click(object sender, RoutedEventArgs e)   => _ = ArpTablosuGoster();
    private void BtnAgBilgi_Click(object sender, RoutedEventArgs e) => AgAdaptorleriniGoster();

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

    // ─── Port tarama paneli animasyonları ────────────────────────────
    private void PortPanelAcAnimasyon()
    {
        _portPanelAcik = true;
        PortPanel.Visibility = Visibility.Visible;
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
                PortIpBox.Focus();
            }
            else
                PingCol.Width = new GridLength(yeni);
        };
        timer.Start();
    }

    private void PortPanelKapatAnimasyon()
    {
        _portPanelAcik = false;
        _portScanCts?.Cancel();
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
                PortPanel.Visibility = Visibility.Collapsed;
            }
            else
                PingCol.Width = new GridLength(yeni);
        };
        timer.Start();
    }

    // ─── Port paneli event'leri ───────────────────────────────────────
    private void PortIpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(PortIpBox);
        var metin = PortIpBox.Text.Trim();
        PortIpPlaceholder.Visibility = string.IsNullOrEmpty(PortIpBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(metin))
            PortIpValidasyon.Text = "";
        else if (GecerliIpv4Mu(metin))
        {
            PortIpValidasyon.Text       = "✓";
            PortIpValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
        }
        else if (GecerliHostnameMu(metin))
        {
            PortIpValidasyon.Text       = "~";
            PortIpValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
        }
        else
        {
            PortIpValidasyon.Text       = "✗";
            PortIpValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
        AktarButonDurumu();
    }

    private void PortIpBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !PortBaslatBtn.IsEnabled) return;
        _ = PortTaraBaslat(PortIpBox.Text.Trim(), PortlariParse(PortAralikBox.Text));
    }

    private void PortAralikBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PortAralikPlaceholder.Visibility = string.IsNullOrEmpty(PortAralikBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        AktarButonDurumu();
    }

    private void AktarButonDurumu()
    {
        var hedef = PortIpBox.Text.Trim();
        bool gecerliHedef = GecerliIpv4Mu(hedef) || GecerliHostnameMu(hedef);
        bool gecerliPort  = !string.IsNullOrWhiteSpace(PortAralikBox.Text)
                            && PortlariParse(PortAralikBox.Text).Length > 0;
        PortBaslatBtn.IsEnabled = gecerliHedef && gecerliPort;
    }

    private void PortHizliBtn_Click(object sender, RoutedEventArgs e)
    {
        PortAralikBox.Text = (string)((Button)sender).Tag;
    }

    private void PortBaslatBtn_Click(object sender, RoutedEventArgs e)
    {
        var hedef  = PortIpBox.Text.Trim();
        var portlar = PortlariParse(PortAralikBox.Text);
        if (portlar.Length == 0 || (!GecerliIpv4Mu(hedef) && !GecerliHostnameMu(hedef))) return;
        _ = PortTaraBaslat(hedef, portlar);
    }

    private void PortPanelKapat_Click(object sender, RoutedEventArgs e)
        => PortPanelKapatAnimasyon();

    // ─── Port tarama işlevi ───────────────────────────────────────────
    private void PortKutucugaYaz(string metin, string hex)
    {
        var satir = new TextBlock
        {
            Text         = metin,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        };
        PortResultPanel.Children.Add(satir);
        PortResultScroll.ScrollToEnd();
    }

    private static int[] PortlariParse(string giris)
    {
        var portlar = new HashSet<int>();
        foreach (var parca in giris.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = parca.Trim();
            if (t.Contains('-'))
            {
                var b = t.Split('-');
                if (b.Length == 2 &&
                    int.TryParse(b[0], out int bas) &&
                    int.TryParse(b[1], out int son))
                {
                    for (int p = Math.Clamp(bas, 1, 65535);
                             p <= Math.Clamp(son, 1, 65535); p++)
                        portlar.Add(p);
                }
            }
            else if (int.TryParse(t, out int p) && p is >= 1 and <= 65535)
                portlar.Add(p);
        }
        return portlar.OrderBy(x => x).ToArray();
    }

    private async Task PortTaraBaslat(string hedef, int[] portlar)
    {
        _portScanCts?.Cancel();
        _portScanCts = new CancellationTokenSource();
        var token = _portScanCts.Token;

        PortResultPanel.Children.Clear();
        PortResultBorder.Visibility = Visibility.Visible;
        PortBaslatBtn.IsEnabled = false;
        PortKutucugaYaz($"◆ {hedef} → {portlar.Length} port taranıyor...", "#8B949E");

        int acik = 0;
        var semaphore = new SemaphoreSlim(50);
        var acikPortlar = new System.Collections.Concurrent.ConcurrentBag<(int Port, string Satir)>();

        var gorevler = portlar.Select(async port =>
        {
            if (token.IsCancellationRequested) return;
            bool acquired = false;
            try
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                acquired = true;
                using var client = new TcpClient();
                var baglanti = client.ConnectAsync(hedef, port);
                var bitti    = await Task.WhenAny(baglanti, Task.Delay(1000, token)).ConfigureAwait(false);
                if (bitti == baglanti && baglanti.IsCompletedSuccessfully)
                {
                    Interlocked.Increment(ref acik);
                    var servis = BilindikPortlar.TryGetValue(port, out var s) ? $"  ({s})" : "";
                    var satir  = $"[AÇIK]  {port}{servis}";
                    acikPortlar.Add((port, satir));
                    await Dispatcher.InvokeAsync(() => PortKutucugaYaz(satir, "#3FB950"));
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally { if (acquired) semaphore.Release(); }
        });

        await Task.WhenAll(gorevler);

        if (!token.IsCancellationRequested)
        {
            PortKutucugaYaz("─────────────────────────", "#30363D");
            var ozet = acik > 0
                ? $"✔ {acik} açık port — {portlar.Length} taranan"
                : $"✖ Açık port bulunamadı — {portlar.Length} taranan";
            PortKutucugaYaz(ozet, acik > 0 ? "#3FB950" : "#F85149");

            var logSatirlari = acikPortlar.OrderBy(x => x.Port).Select(x => x.Satir).ToList();
            logSatirlari.Add(ozet);
            LogKaydet("PORT TARA", hedef, logSatirlari);
        }

        PortBaslatBtn.IsEnabled = true;
    }

    // ─── Traceroute ───────────────────────────────────────────────────
    private void TraceHedefBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(TraceHedefBox);
        var m = TraceHedefBox.Text.Trim();
        TraceHedefPlaceholder.Visibility = string.IsNullOrEmpty(TraceHedefBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(m)) { TraceHedefValidasyon.Text = ""; TraceBaslatBtn.IsEnabled = false; return; }
        if (GecerliIpv4Mu(m)) { TraceHedefValidasyon.Text = "✓"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63,185,80)); TraceBaslatBtn.IsEnabled = true; }
        else if (GecerliHostnameMu(m)) { TraceHedefValidasyon.Text = "~"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(210,153,34)); TraceBaslatBtn.IsEnabled = true; }
        else { TraceHedefValidasyon.Text = "✗"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248,81,73)); TraceBaslatBtn.IsEnabled = false; }
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
        _tracePanelAcik = false; _traceCts?.Cancel();
        YanPanelKapatAnimasyon(() => TracePanel.Visibility = Visibility.Collapsed);
    }

    private void TraceKutucugaYaz(string metin, string hex) =>
        TraceResultPanel.Children.Add(new TextBlock
        {
            Text = metin, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,1,0,1),
        });

    private async Task TracerouteBaslat(string hedef)
    {
        _traceCts?.Cancel();
        _traceCts = new CancellationTokenSource();
        var token = _traceCts.Token;

        TraceResultPanel.Children.Clear();
        TraceResultBorder.Visibility = Visibility.Visible;
        TraceBaslatBtn.IsEnabled = false;
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
            LogKaydet("TRACEROUTE", hedef, logSatirlari);
        }
    }

    // ─── DNS Lookup ───────────────────────────────────────────────────
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
        _dnsPanelAcik = false;
        YanPanelKapatAnimasyon(() => DnsPanel.Visibility = Visibility.Collapsed);
    }

    private void DnsKutucugaYaz(string metin, string hex) =>
        DnsResultPanel.Children.Add(new TextBlock
        {
            Text = metin, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,1,0,1),
        });

    private async Task DnsLookupBaslat(string hedef)
    {
        DnsResultPanel.Children.Clear();
        DnsResultBorder.Visibility = Visibility.Visible;
        DnsBaslatBtn.IsEnabled = false;
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
                    var s = $"  {(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "A   " : "AAAA")}  →  {ip}";
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
        LogKaydet("DNS", hedef, logSatirlari);
    }

    // ─── Wake-on-LAN ──────────────────────────────────────────────────
    private static readonly Regex _macRegex = new(
        @"^([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})[:\-]([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$",
        RegexOptions.Compiled);

    private void WolMacBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        WolMacPlaceholder.Visibility = string.IsNullOrEmpty(WolMacBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        var m = WolMacBox.Text.Trim();
        if (string.IsNullOrEmpty(m)) { WolMacValidasyon.Text = ""; WolGonderBtn.IsEnabled = false; return; }
        if (_macRegex.IsMatch(m)) { WolMacValidasyon.Text = "✓"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63,185,80)); WolGonderBtn.IsEnabled = true; }
        else { WolMacValidasyon.Text = "✗"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248,81,73)); WolGonderBtn.IsEnabled = false; }
    }

    private void WolMacBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && WolGonderBtn.IsEnabled) WolGonder(WolMacBox.Text.Trim());
    }

    private void WolGonderBtn_Click(object sender, RoutedEventArgs e) => WolGonder(WolMacBox.Text.Trim());
    private void WolPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _wolPanelAcik = false;
        YanPanelKapatAnimasyon(() => WolPanel.Visibility = Visibility.Collapsed);
    }

    private void WolKutucugaYaz(string metin, string hex) =>
        WolResultPanel.Children.Add(new TextBlock
        {
            Text = metin, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,1,0,1),
        });

    private void WolGonder(string mac)
    {
        WolResultPanel.Children.Clear();
        WolResultBorder.Visibility = Visibility.Visible;
        var logSatirlari = new List<string>();
        try
        {
            var temiz = mac.Replace(":", "").Replace("-", "");
            var macBytes = Enumerable.Range(0, 6).Select(i => Convert.ToByte(temiz.Substring(i * 2, 2), 16)).ToArray();
            var paket = new byte[102];
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
        LogKaydet("WAKE-ON-LAN", mac, logSatirlari);
    }

    // ─── SNMP Sorgusu ─────────────────────────────────────────────────
    private void SnmpIpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(SnmpIpBox);
        var metin = SnmpIpBox.Text.Trim();
        SnmpIpPlaceholder.Visibility = string.IsNullOrEmpty(SnmpIpBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(metin))
        {
            SnmpIpValidasyon.Text    = "";
            SnmpBaslatBtn.IsEnabled  = false;
        }
        else if (GecerliIpv4Mu(metin))
        {
            SnmpIpValidasyon.Text       = "✓";
            SnmpIpValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            SnmpBaslatBtn.IsEnabled     = true;
        }
        else
        {
            SnmpIpValidasyon.Text       = "✗";
            SnmpIpValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
            SnmpBaslatBtn.IsEnabled     = false;
        }
    }

    private void SnmpIpBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SnmpBaslatBtn.IsEnabled)
            _ = SnmpSorguBaslat(SnmpIpBox.Text.Trim());
    }

    private void SnmpCommunityBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SnmpCommunityPlaceholder.Visibility = string.IsNullOrEmpty(SnmpCommunityBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SnmpVersiyonBtn_Click(object sender, RoutedEventArgs e)
    {
        _snmpVersiyon = (string)((Button)sender).Tag;
        SnmpV2cBtn.Style = (Style)FindResource(_snmpVersiyon == "v2c" ? "SelectedChipButton" : "ChipButton");
        SnmpV1Btn.Style  = (Style)FindResource(_snmpVersiyon == "v1"  ? "SelectedChipButton" : "ChipButton");
    }

    private void SnmpBaslatBtn_Click(object sender, RoutedEventArgs e)
        => _ = SnmpSorguBaslat(SnmpIpBox.Text.Trim());

    private void SnmpPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _snmpPanelAcik = false;
        YanPanelKapatAnimasyon(() => SnmpPanel.Visibility = Visibility.Collapsed);
    }

    private void SnmpKutucugaYaz(string metin, string hex) =>
        SnmpResultPanel.Children.Add(new TextBlock
        {
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private async Task SnmpSorguBaslat(string ip)
    {
        SnmpResultPanel.Children.Clear();
        SnmpResultBorder.Visibility = Visibility.Visible;
        SnmpBaslatBtn.IsEnabled     = false;

        var community = string.IsNullOrWhiteSpace(SnmpCommunityBox.Text)
            ? "public" : SnmpCommunityBox.Text.Trim();
        var version   = _snmpVersiyon == "v1" ? VersionCode.V1 : VersionCode.V2;

        SnmpKutucugaYaz($"◆ {ip}  community={community}  {_snmpVersiyon}", "#8B949E");

        // Sorgulanacak MIB-II sysGroup OID'leri
        var oidler = new List<Variable>
        {
            new(new ObjectIdentifier("1.3.6.1.2.1.1.1.0")), // sysDescr
            new(new ObjectIdentifier("1.3.6.1.2.1.1.3.0")), // sysUpTime
            new(new ObjectIdentifier("1.3.6.1.2.1.1.4.0")), // sysContact
            new(new ObjectIdentifier("1.3.6.1.2.1.1.5.0")), // sysName
            new(new ObjectIdentifier("1.3.6.1.2.1.1.6.0")), // sysLocation
        };

        var logSatirlari = new List<string>
            { $"IP={ip}  community={community}  versiyon={_snmpVersiyon}" };

        try
        {
            var adres    = IPAddress.Parse(ip);
            var endpoint = new IPEndPoint(adres, 161);

            IList<Variable> sonuc = await Task.Run(() =>
                Messenger.Get(version, endpoint, new OctetString(community), oidler, 3000));

            var adlar = new Dictionary<string, string>
            {
                { "1.3.6.1.2.1.1.1.0", "Açıklama " },
                { "1.3.6.1.2.1.1.3.0", "Uptime   " },
                { "1.3.6.1.2.1.1.4.0", "İletişim " },
                { "1.3.6.1.2.1.1.5.0", "Ad       " },
                { "1.3.6.1.2.1.1.6.0", "Konum    " },
            };

            bool veriVar = false;
            foreach (var v in sonuc)
            {
                var oidStr = v.Id.ToString();
                string ad  = adlar.TryGetValue(oidStr, out var a) ? a : oidStr;
                string deger;
                string hex;

                if (v.Data is NoSuchObject or NoSuchInstance)
                {
                    deger = "(mevcut değil)";
                    hex   = "#484F58";
                }
                else
                {
                    deger    = SnmpDegerFormatla(v.Data, oidStr);
                    hex      = "#C9D1D9";
                    veriVar  = true;
                }

                SnmpKutucugaYaz($"  {ad}  →  {deger}", hex);
                logSatirlari.Add($"  {ad} → {deger}");
            }

            SnmpKutucugaYaz("─────────────────────────", "#30363D");
            if (veriVar)
            {
                SnmpKutucugaYaz($"✔ Sorgu tamamlandı — {_snmpVersiyon}", "#3FB950");
                logSatirlari.Add($"✔ Başarılı");
            }
            else
            {
                SnmpKutucugaYaz("✖ OID yanıtı boş (community hatalı olabilir)", "#F85149");
                logSatirlari.Add("✖ Boş yanıt");
            }
        }
        catch (Lextm.SharpSnmpLib.Messaging.TimeoutException)
        {
            SnmpKutucugaYaz("─────────────────────────", "#30363D");
            SnmpKutucugaYaz("✖ Yanıt yok — SNMP aktif değil veya community hatalı", "#F85149");
            logSatirlari.Add("✖ Timeout");
        }
        catch (Exception ex)
        {
            SnmpKutucugaYaz("─────────────────────────", "#30363D");
            SnmpKutucugaYaz($"✖ {ex.Message}", "#F85149");
            logSatirlari.Add($"✖ {ex.Message}");
        }

        LogKaydet("SNMP", ip, logSatirlari);
        SnmpResultScroll.ScrollToEnd();
        SnmpBaslatBtn.IsEnabled = true;
    }

    private static string SnmpDegerFormatla(ISnmpData veri, string oid)
    {
        if (oid == "1.3.6.1.2.1.1.3.0" && veri is TimeTicks t)
            return SnmpUptimeFormatla(t.ToUInt32());
        return veri switch
        {
            OctetString s => s.ToString(),
            Integer32   i => i.ToInt32().ToString(),
            Counter32   c => c.ToUInt32().ToString(),
            Gauge32     g => g.ToUInt32().ToString(),
            _             => veri.ToString() ?? "-",
        };
    }

    private static string SnmpUptimeFormatla(uint ticks)
    {
        var ts = TimeSpan.FromSeconds(ticks / 100.0);
        return $"{(int)ts.TotalDays}g {ts.Hours:00}s {ts.Minutes:00}d {ts.Seconds:00}sn";
    }

    // ─── Ağ Adaptörü Bilgileri ────────────────────────────────────────
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
        LogKaydet("AG BILGI", "yerel adaptörler", logSatirlari);
    }

    // ─── ARP Tablosu ──────────────────────────────────────────────────
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

            var regex = new Regex(@"(\d{1,3}(?:\.\d{1,3}){3})\s+([0-9a-fA-F]{2}(?:[:\-][0-9a-fA-F]{2}){5})\s+(\w+)");
            var esleseler = regex.Matches(cikti);
            if (esleseler.Count == 0) { MesajEkle("sistem", "ARP tablosunda kayıt bulunamadı."); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"ARP tablosu — {esleseler.Count} kayıt:");
            sb.AppendLine($"  {"IP Adresi",-18} {"MAC Adresi",-20} Tür");
            sb.AppendLine($"  {"─────────────────",-18} {"───────────────────",-20} ──────");
            foreach (Match m in esleseler)
                sb.AppendLine($"  {m.Groups[1].Value,-18} {m.Groups[2].Value,-20} {m.Groups[3].Value}");
            var metin = sb.ToString().TrimEnd();
            MesajEkle("sonuc", metin);
            LogKaydet("ARP", "arp -a", metin.Split('\n').Select(s => s.TrimEnd()));
        }
        catch (Exception ex) { MesajEkle("hata", $"ARP tablosu okunamadı: {ex.Message}"); }
    }

}
