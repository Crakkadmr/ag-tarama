using System;
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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow : Window
{
    // ─── Ayarlar ─────────────────────────────────────────────────────
    private AppSettings _ayarlar = SettingsService.Yukle();
    private int HedefMB    => _ayarlar.HedefMB;
    private int HedefKB    => _ayarlar.HedefMB * 1024;
    private int TestSuresiSn => _ayarlar.TestSuresiSn;

    // ─── Durum ───────────────────────────────────────────────────────
    private bool _taramaDevamEdiyor = false;
    private CancellationTokenSource? _taramaCts;

    // ─── Sekme indeksleri ────────────────────────────────────────────
    private const int TabChatbot   = 0;
    private const int TabCihazTara = 1;
    private const int TabPing      = 2;
    private const int TabPort      = 3;
    private const int TabTrace     = 4;
    private const int TabDns       = 5;
    private const int TabWol       = 6;
    private const int TabFavoriler = 7;
    private const int TabBant      = 8;

    // ─── Ping paneli ─────────────────────────────────────────────────
    private CancellationTokenSource? _pingCts;

    // ─── Port tarama paneli ──────────────────────────────────────────
    private CancellationTokenSource? _portScanCts;

    // ─── Traceroute / Kamera ─────────────────────────────────────────
    private CancellationTokenSource? _traceCts;
    private CancellationTokenSource? _kameraCts;

    // ─── Bant Genişliği paneli ────────────────────────────────────────
    private System.Windows.Threading.DispatcherTimer? _bantTimer;
    private readonly Dictionary<string, (long RxBytes, long TxBytes, long Timestamp)> _bantOnceki = new();

    // ─── Toast ────────────────────────────────────────────────────────
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    // ─── Mesaj geçmişi (HTML rapor için) ─────────────────────────────
    private readonly List<(string Tur, string Metin, string Zaman)> _mesajGecmisi = new();

    // ─── Servisler ───────────────────────────────────────────────────
    private readonly CaptureService _captureService = new();

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

    private static readonly Dictionary<string, string> OuiTablosu = new(StringComparer.OrdinalIgnoreCase)
    {
        // Hikvision
        {"4C1FCC","Hikvision"},{"3C58C2","Hikvision"},{"D867D9","Hikvision"},{"B4A3B1","Hikvision"},
        {"24B20E","Hikvision"},{"C0F0B4","Hikvision"},{"ACCC8E","Hikvision"},
        // Dahua
        {"9CFBD5","Dahua"},{"E0626E","Dahua"},{"108CCF","Dahua"},{"A00A7E","Dahua"},
        // Axis
        {"B8A44E","Axis"},{"00408C","Axis"},
        // Reolink
        {"EC71DB","Reolink"},{"B4530F","Reolink"},
        // TP-Link
        {"F4F26D","TP-Link"},{"50C7BF","TP-Link"},{"B0487A","TP-Link"},{"B4B024","TP-Link"},
        {"3C52A1","TP-Link"},{"FC3FDB","TP-Link"},{"689427","TP-Link"},{"A40249","TP-Link"},
        {"98DAFF","TP-Link"},{"C46E1F","TP-Link"},
        // Cisco
        {"001A2F","Cisco"},{"0019E7","Cisco"},{"00178C","Cisco"},{"000BFD","Cisco"},
        {"485B39","Cisco"},{"3C0E23","Cisco"},{"70697F","Cisco"},
        // MikroTik
        {"4C5E0C","MikroTik"},{"6C3B6B","MikroTik"},{"D4CA6D","MikroTik"},
        {"DC2C6E","MikroTik"},{"B8690E","MikroTik"},{"E48D8C","MikroTik"},
        // Ubiquiti
        {"788A20","Ubiquiti"},{"FCECD2","Ubiquiti"},{"E063DA","Ubiquiti"},
        {"00272E","Ubiquiti"},{"44D9E7","Ubiquiti"},{"802AA8","Ubiquiti"},{"24A43C","Ubiquiti"},
        // ASUS
        {"107B44","ASUS"},{"1C872C","ASUS"},{"2C56DC","ASUS"},{"30D3D8","ASUS"},
        {"50465D","ASUS"},{"AC9E17","ASUS"},
        // D-Link
        {"14D64D","D-Link"},{"1CBE69","D-Link"},{"28107B","D-Link"},{"34088A","D-Link"},
        {"BCAEC5","D-Link"},{"F07D68","D-Link"},{"B8A386","D-Link"},
        // NETGEAR
        {"6CB0CE","NETGEAR"},{"A040A0","NETGEAR"},{"C03F0E","NETGEAR"},{"E091F5","NETGEAR"},
        {"20E52A","NETGEAR"},{"9C3DCF","NETGEAR"},
        // Huawei
        {"0022A1","Huawei"},{"086070","Huawei"},{"1022BC","Huawei"},{"286ED4","Huawei"},
        {"48A472","Huawei"},{"5422F8","Huawei"},{"9C28EF","Huawei"},{"F4CBE6","Huawei"},
        // Apple
        {"3C0754","Apple"},{"A45E60","Apple"},{"98014A","Apple"},{"7CD1C3","Apple"},
        {"90B21F","Apple"},{"E0F847","Apple"},{"A8BE27","Apple"},{"3C15C2","Apple"},
        // Samsung
        {"00E0D0","Samsung"},{"1CEB43","Samsung"},{"2C0E3D","Samsung"},{"6C2069","Samsung"},
        {"84A466","Samsung"},{"8838B4","Samsung"},{"549B12","Samsung"},
        // Intel (NIC / Wi-Fi)
        {"001B21","Intel"},{"A0A4C5","Intel"},{"94659C","Intel"},{"F8599C","Intel"},
        {"8C8D28","Intel"},{"3C970E","Intel"},
        // Realtek
        {"E09185","Realtek"},{"00E04C","Realtek"},{"001D60","Realtek"},
        // ZyXEL
        {"001349","ZyXEL"},{"B0B2DC","ZyXEL"},{"E84DD0","ZyXEL"},{"F0B429","ZyXEL"},
        // Synology
        {"001132","Synology"},{"0011324","Synology"},
        // QNAP
        {"246E96","QNAP"},{"00089B","QNAP"},
        // Tenda
        {"C83A35","Tenda"},{"00D0F8","Tenda"},{"1880BE","Tenda"},
        // VMware (virtual)
        {"000C29","VMware"},{"005056","VMware"},{"000569","VMware"},
        // Raspberry Pi
        {"B827EB","Raspberry Pi"},{"DC4F22","Raspberry Pi"},{"E45F01","Raspberry Pi"},
    };

    private static string OuiAra(string mac)
    {
        var temiz = mac.Replace(":", "").Replace("-", "").Replace(".", "").ToUpperInvariant();
        if (temiz.Length < 6) return "";
        return OuiTablosu.TryGetValue(temiz[..6], out var marka) ? marka : "";
    }

    // ─── Başlangıç ───────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        _kameraSatirView = CollectionViewSource.GetDefaultView(_kameraSatirlari);
        _kameraSatirView.Filter = KameraSatirFiltredenGecer;
        KameraDataGrid.ItemsSource = _kameraSatirView;
        MesajEkle("sistem", "Network Sniffer başlatıldı — made by demircan.");
        _ = BaslangicAsync();
    }

    private async Task BaslangicAsync()
    {
        LogService.OturumBaslat();
        FavoriChipleriniYenile();
        if (!await NpcapKontrolVeKur()) return;
        MesajEkle("sonuc", "✔ Sistem hazır — sağ panelden taramayı başlatın.");
    }

    // Tüm hata yolları buradan geçer: chat'e kırmızı mesaj + log dosyasına yazar.
    private void HataBildir(string mesaj, Exception? ex = null)
    {
        MesajEkle("hata", ex == null ? mesaj : $"{mesaj}: {ex.Message}");
        LogService.Hata(mesaj, ex);
    }

    // ─── Npcap kontrol ve kurulum ─────────────────────────────────────
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

        if (!File.Exists(Paths.NpcapInstaller))
        {
            HataBildir($"Npcap kurulu değil ve installer bulunamadı:\n{Paths.NpcapInstaller}");
            return false;
        }

        MesajEkle("sistem", "⚠ Npcap kurulu değil — kurulum penceresi açılıyor...");
        MesajEkle("kullanici", "Npcap kurulumunu tamamlayınız.");

        try
        {
            var psi = new ProcessStartInfo(Paths.NpcapInstaller)
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
                MesajEkle("hata", "Npcap kurulumu tamamlanamadı veya iptal edildi. Bilgisayarı yeniden başlatmanız gerekebilir.");
                return false;
            }
        }
        catch (Exception ex)
        {
            HataBildir("Npcap kurulum hatası", ex);
            return false;
        }
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
        _taramaCts = new CancellationTokenSource();

        var (kart, kartGuncelle, kartTamamla, kartDurdur) = YakalamaKartiOlustur(dosyaAdi);
        ChatPanel.Children.Add(kart);
        ChatScrollViewer.ScrollToEnd();

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

            // WiresharkPortable64.exe pcap dosyasını argüman olarak alır
            Process.Start(new ProcessStartInfo(Paths.WiresharkPortableExe, $"\"{pcap}\"")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            HataBildir("Wireshark Portable açılamadı", ex);
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
        _mesajGecmisi.Add((tur, metin, DateTime.Now.ToString("HH:mm:ss")));

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

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int sel = MainTabControl.SelectedIndex;
        if (sel == TabBant)
            BantIzlemeBaslat();
        else
            _bantTimer?.Stop();
        if (sel == TabFavoriler)
            FavorilerPanelGuncelle();
        if (sel == TabCihazTara && string.IsNullOrEmpty(KameraSubnetBox.Text))
            KameraSubnetBox.Text = YerelSubnetiBul() ?? "";
    }

    private void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabPing;
        PingIpBox.Focus();
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
            PingFavoriEkleBtn.IsEnabled    = false;
        }
        else if (GecerliIpv4Mu(metin))
        {
            PingValidasyonIkonu.Text       = "✓";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            PingBaslatBtn.IsEnabled        = true;
            PingFavoriEkleBtn.IsEnabled    = true;
        }
        else if (GecerliHostnameMu(metin))
        {
            PingValidasyonIkonu.Text       = "~";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
            PingBaslatBtn.IsEnabled        = true;
            PingFavoriEkleBtn.IsEnabled    = true;
        }
        else
        {
            PingValidasyonIkonu.Text       = "✗";
            PingValidasyonIkonu.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
            PingBaslatBtn.IsEnabled        = false;
            PingFavoriEkleBtn.IsEnabled    = false;
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
    {
        _pingCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

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

        await foreach (var r in PingService.PingleAsync(hedef, sayi: 4, timeoutMs: _ayarlar.PingTimeoutMs, token: token))
        {
            string s; string hex;
            if (r.Hata != null)             { s = $"[{r.Sira}/{r.Toplam}] {r.Hata}";          hex = "#F85149"; }
            else if (r.Durum == IPStatus.Success)
            {
                basarili++; toplamMs += r.RtMs;
                s = $"[{r.Sira}/{r.Toplam}] {r.RtMs} ms  TTL={r.Ttl}"; hex = "#58A6FF";
            }
            else                            { s = $"[{r.Sira}/{r.Toplam}] {r.Durum}";         hex = "#F85149"; }
            PingKutucugaYaz(s, hex);
            logSatirlari.Add(s);
        }

        if (!token.IsCancellationRequested)
        {
            PingKutucugaYaz("─────────────────────────", "#30363D");
            string ozet = basarili > 0
                ? $"✔ {basarili}/4 başarılı — ort. {toplamMs / basarili} ms, {4 - basarili} kayıp"
                : "✖ yanıt yok (4/4 kayıp)";
            PingKutucugaYaz(ozet, basarili > 0 ? "#3FB950" : "#F85149");
            logSatirlari.Add(ozet);
            LogService.Kaydet("PING", hedef, logSatirlari);
            BildirimCal(hata: basarili == 0);
            ToastGoster(basarili > 0 ? $"Ping tamamlandı — {hedef}" : $"Ping başarısız — {hedef}", hata: basarili == 0);
        }
    }

    private void BtnPortTara_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabPort;
        PortIpBox.Focus();
    }

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

    private void BtnArp_Click(object sender, RoutedEventArgs e)   => _ = ArpTablosuGoster();
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

    // ─── (Eski animasyon — artık kullanılmıyor) ──────────────────────
    private void PortPanelAcAnimasyon() { PortIpBox.Focus(); }
    private void PortPanelKapatAnimasyon() { _portScanCts?.Cancel(); }

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
        _ = PortTaraBaslat(PortIpBox.Text.Trim(), PortScanService.Parse(PortAralikBox.Text));
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
                            && PortScanService.Parse(PortAralikBox.Text).Length > 0;
        PortBaslatBtn.IsEnabled     = gecerliHedef && gecerliPort;
        PortFavoriEkleBtn.IsEnabled = gecerliHedef;
    }

    private void PortHizliBtn_Click(object sender, RoutedEventArgs e)
    {
        PortAralikBox.Text = (string)((Button)sender).Tag;
    }

    private void PortBaslatBtn_Click(object sender, RoutedEventArgs e)
    {
        var hedef  = PortIpBox.Text.Trim();
        var portlar = PortScanService.Parse(PortAralikBox.Text);
        if (portlar.Length == 0 || (!GecerliIpv4Mu(hedef) && !GecerliHostnameMu(hedef))) return;
        _ = PortTaraBaslat(hedef, portlar);
    }

    private void PortPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _portScanCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

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

    private async Task PortTaraBaslat(string hedef, int[] portlar)
    {
        _portScanCts?.Cancel();
        _portScanCts = new CancellationTokenSource();
        var token = _portScanCts.Token;

        PortResultPanel.Children.Clear();
        PortResultBorder.Visibility = Visibility.Visible;
        PortBaslatBtn.IsEnabled = false;
        PortKutucugaYaz($"◆ {hedef} → {portlar.Length} port taranıyor...", "#8B949E");

        var acikPortlar = new System.Collections.Concurrent.ConcurrentBag<(int Port, string Satir)>();

        Task PortAcikCallback(int port)
        {
            var servis = BilindikPortlar.TryGetValue(port, out var s) ? $"  ({s})" : "";
            var satir  = $"[AÇIK]  {port}{servis}";
            acikPortlar.Add((port, satir));
            return Dispatcher.InvokeAsync(() => PortKutucugaYaz(satir, "#3FB950")).Task;
        }

        int acik = await PortScanService.TaraAsync(hedef, portlar, PortAcikCallback, token,
            eszamanli: _ayarlar.PortTaramaConcurrency, timeoutMs: _ayarlar.PortTaramaTimeoutMs);

        if (!token.IsCancellationRequested)
        {
            PortKutucugaYaz("─────────────────────────", "#30363D");
            var ozet = acik > 0
                ? $"✔ {acik} açık port — {portlar.Length} taranan"
                : $"✖ Açık port bulunamadı — {portlar.Length} taranan";
            PortKutucugaYaz(ozet, acik > 0 ? "#3FB950" : "#F85149");

            var logSatirlari = acikPortlar.OrderBy(x => x.Port).Select(x => x.Satir).ToList();
            logSatirlari.Add(ozet);
            LogService.Kaydet("PORT TARA", hedef, logSatirlari);
            BildirimCal(hata: acik == 0);
            ToastGoster(acik > 0 ? $"{acik} açık port bulundu — {hedef}" : $"Açık port yok — {hedef}", hata: acik == 0);
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
        _traceCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
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
            LogService.Kaydet("TRACEROUTE", hedef, logSatirlari);
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
        MainTabControl.SelectedIndex = TabChatbot;
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
        LogService.Kaydet("DNS", hedef, logSatirlari);
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
        MainTabControl.SelectedIndex = TabChatbot;
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
        LogService.Kaydet("WAKE-ON-LAN", mac, logSatirlari);
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
        LogService.Kaydet("AG BILGI", "yerel adaptörler", logSatirlari);
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
            sb.AppendLine($"  {"IP Adresi",-18} {"MAC Adresi",-20} {"Tür",-10} Üretici");
            sb.AppendLine($"  {"─────────────────",-18} {"───────────────────",-20} {"──────────",-10} ──────────────────");
            foreach (Match m in esleseler)
            {
                var uretici = OuiAra(m.Groups[2].Value);
                sb.AppendLine($"  {m.Groups[1].Value,-18} {m.Groups[2].Value,-20} {m.Groups[3].Value,-10} {uretici}");
            }
            var metin = sb.ToString().TrimEnd();
            MesajEkle("sonuc", metin);
            LogService.Kaydet("ARP", "arp -a", metin.Split('\n').Select(s => s.TrimEnd()));
        }
        catch (Exception ex) { MesajEkle("hata", $"ARP tablosu okunamadı: {ex.Message}"); }
    }

    // ─── 4.1 Bant Genişliği Monitörü ─────────────────────────────────

    private void BtnBant_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabBant;
    }

    private void BantPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _bantTimer?.Stop();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void BantIzlemeBaslat()
    {
        _bantOnceki.Clear();
        BantAdaptorPanel.Children.Clear();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = ni.GetIPv4Statistics();
            _bantOnceki[ni.Id] = (stats.BytesReceived, stats.BytesSent, Environment.TickCount64);
        }
        _bantTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _bantTimer.Tick += BantTimerTick;
        _bantTimer.Start();
        BantTimerTick(null, EventArgs.Empty);
    }

    private void BantTimerTick(object? sender, EventArgs e)
    {
        BantAdaptorPanel.Children.Clear();
        var now = Environment.TickCount64;

        var adaptorler = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                      && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        if (adaptorler.Count == 0) { BantDurumText.Text = "Aktif adaptör bulunamadı."; return; }

        BantDurumText.Text = $"{adaptorler.Count} adaptör — {DateTime.Now:HH:mm:ss}";

        foreach (var ni in adaptorler)
        {
            var stats = ni.GetIPv4Statistics();
            long rxNow = stats.BytesReceived;
            long txNow = stats.BytesSent;
            long rxHiz = 0, txHiz = 0;

            if (_bantOnceki.TryGetValue(ni.Id, out var prev))
            {
                double sn = Math.Max((now - prev.Timestamp) / 1000.0, 0.001);
                rxHiz = Math.Max((long)((rxNow - prev.RxBytes) / sn), 0);
                txHiz = Math.Max((long)((txNow - prev.TxBytes) / sn), 0);
            }
            _bantOnceki[ni.Id] = (rxNow, txNow, now);

            bool aktif = rxHiz > 0 || txHiz > 0;
            var kart = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                BorderBrush     = new SolidColorBrush(aktif ? Color.FromRgb(35, 134, 54) : Color.FromRgb(33, 38, 45)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(0, 0, 0, 6),
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text         = ni.Name.Length > 30 ? ni.Name[..30] + "…" : ni.Name,
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 11,
                FontWeight   = FontWeights.Bold,
                Foreground   = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                TextWrapping = TextWrapping.Wrap,
            });
            sp.Children.Add(new TextBlock
            {
                Text       = $"  ↓ {BantHizFormatla(rxHiz),10}   ↑ {BantHizFormatla(txHiz)}",
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(aktif ? Color.FromRgb(63, 185, 80) : Color.FromRgb(72, 79, 88)),
                Margin     = new Thickness(0, 3, 0, 0),
            });
            kart.Child = sp;
            BantAdaptorPanel.Children.Add(kart);
        }
    }

    private static string BantHizFormatla(long bytesPerSec)
    {
        if (bytesPerSec >= 1_000_000) return $"{bytesPerSec / 1_000_000.0:0.0} MB/s";
        if (bytesPerSec >= 1_000)     return $"{bytesPerSec / 1_000.0:0.0} KB/s";
        return $"{bytesPerSec} B/s";
    }

    // ─── 4.2 Favori IP Listesi ───────────────────────────────────────

    private void BtnFavoriler_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabFavoriler;
    }

    private void FavorilerPanelKapat_Click(object sender, RoutedEventArgs e) => FavorilerPanelKapat();

    private void FavorilerPanelKapat()
    {
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void PingFavoriEkle_Click(object sender, RoutedEventArgs e)
    {
        var ip = PingIpBox.Text.Trim();
        if (string.IsNullOrEmpty(ip)) return;
        bool eklendi = FavoriService.Ekle(ip);
        FavoriChipleriniYenile();
        ToastGoster(eklendi ? $"★ Favoriye eklendi: {ip}" : $"Zaten favoride: {ip}", hata: !eklendi);
    }

    private void PortFavoriEkle_Click(object sender, RoutedEventArgs e)
    {
        var ip = PortIpBox.Text.Trim();
        if (string.IsNullOrEmpty(ip)) return;
        bool eklendi = FavoriService.Ekle(ip);
        FavoriChipleriniYenile();
        ToastGoster(eklendi ? $"★ Favoriye eklendi: {ip}" : $"Zaten favoride: {ip}", hata: !eklendi);
    }

    private void FavoriChipleriniYenile()
    {
        var favoriler = FavoriService.YukleHepsi();
        PingFavorilerPanel.Children.Clear();
        PortFavorilerPanel.Children.Clear();

        foreach (var ip in favoriler)
        {
            var capturedIp = ip;
            var pingChip = new Button { Content = $"★ {ip}", Style = (Style)FindResource("ChipButton"), Tag = ip };
            pingChip.Click += (_, _) => { PingIpBox.Text = capturedIp; _ = PingBaslat(capturedIp); };
            PingFavorilerPanel.Children.Add(pingChip);

            var portChip = new Button { Content = $"★ {ip}", Style = (Style)FindResource("ChipButton"), Tag = ip };
            portChip.Click += (_, _) => { PortIpBox.Text = capturedIp; };
            PortFavorilerPanel.Children.Add(portChip);
        }
    }

    private void FavorilerPanelGuncelle()
    {
        FavorilerListePanel.Children.Clear();
        var favoriler = FavoriService.YukleHepsi();

        if (favoriler.Count == 0)
        {
            FavorilerListePanel.Children.Add(new TextBlock
            {
                Text = "Henüz favori eklenmedi.\nPing veya Port Tara panelindeki ★ ile ekleyin.",
                Foreground   = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 4, 0, 4),
            });
            return;
        }

        foreach (var ip in favoriler)
        {
            var capturedIp = ip;
            var satir = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            satir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            satir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ipBtn = new Button
            {
                Content             = ip,
                FontFamily          = new FontFamily("Consolas"),
                FontSize            = 12,
                Foreground          = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                Background          = Brushes.Transparent,
                BorderThickness     = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor              = Cursors.Hand,
                Padding             = new Thickness(0),
            };
            ipBtn.Click += (_, _) =>
            {
                MainTabControl.SelectedIndex = TabPing;
                PingIpBox.Text = capturedIp;
                _ = PingBaslat(capturedIp);
            };
            Grid.SetColumn(ipBtn, 0);

            var silBtn = new Button
            {
                Content         = "✕",
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 11,
                Foreground      = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor          = Cursors.Hand,
                Padding         = new Thickness(6, 0, 0, 0),
                ToolTip         = "Favoriden kaldır",
            };
            silBtn.Click += (_, _) =>
            {
                FavoriService.Sil(capturedIp);
                FavoriChipleriniYenile();
                FavorilerPanelGuncelle();
            };
            Grid.SetColumn(silBtn, 1);

            satir.Children.Add(ipBtn);
            satir.Children.Add(silBtn);
            FavorilerListePanel.Children.Add(satir);
        }
    }

    // ─── 4.3 Ayarlar Paneli ──────────────────────────────────────────

    private void BtnAyarlar_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_ayarlar) { Owner = this };
        if (win.ShowDialog() == true)
            _ayarlar = win.Ayarlar;
    }

    // ─── 4.4 HTML Rapor Çıktısı ──────────────────────────────────────

    private void BtnRapor_Click(object sender, RoutedEventArgs e) => RaporKaydet();

    private void RaporKaydet()
    {
        if (_mesajGecmisi.Count == 0) { MesajEkle("sistem", "Henüz kaydedilecek mesaj yok."); return; }

        var dlg = new SaveFileDialog
        {
            Title    = "Raporu Kaydet",
            Filter   = "HTML Rapor (*.html)|*.html|Metin Dosyası (*.txt)|*.txt",
            FileName = $"AgTarama_Rapor_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        if (dlg.FilterIndex == 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AG TARAMA PROGRAMI — Rapor");
            sb.AppendLine($"Oluşturulma: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('─', 60));
            foreach (var (tur, metin, zaman) in _mesajGecmisi)
                sb.AppendLine($"[{zaman}] [{tur.ToUpper(),-8}] {metin}");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }
        else
        {
            static string Enc(string s) => s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Ağ Tarama Raporu</title><style>");
            sb.AppendLine("body{background:#0D1117;color:#E6EDF3;font-family:Consolas,monospace;padding:24px;margin:0}");
            sb.AppendLine("h1{color:#58A6FF;margin-bottom:4px}.meta{color:#484F58;font-size:11px;margin-bottom:20px}");
            sb.AppendLine(".msg{margin:3px 0;padding:8px 12px;border-radius:6px;border:1px solid;white-space:pre-wrap;word-break:break-all}");
            sb.AppendLine(".sistem{background:#161B22;border-color:#21262D;color:#8B949E}");
            sb.AppendLine(".sonuc{background:#0D3B66;border-color:#1F6FEB;color:#58A6FF}");
            sb.AppendLine(".hata{background:#3D1A1A;border-color:#8B1A1A;color:#F85149}");
            sb.AppendLine(".kullanici{background:#161B22;border-color:#30363D;color:#C9D1D9;text-align:right}");
            sb.AppendLine(".zaman{font-size:10px;color:#484F58;margin:0 4px 6px}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>AG TARAMA PROGRAMI</h1>");
            sb.AppendLine($"<div class=\"meta\">Rapor tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &nbsp;|&nbsp; {_mesajGecmisi.Count} mesaj</div>");
            foreach (var (tur, metin, zaman) in _mesajGecmisi)
            {
                sb.AppendLine($"<div class=\"msg {tur}\">{Enc(metin)}</div>");
                sb.AppendLine($"<div class=\"zaman\">{zaman}</div>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        MesajEkle("sonuc", $"✔ Rapor kaydedildi: {Path.GetFileName(dlg.FileName)}");
        ToastGoster($"Rapor kaydedildi: {Path.GetFileName(dlg.FileName)}");
    }

    // ─── 5.3 Sürükle-Bırak pcap Açma ────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var dosyalar = (string[])e.Data.GetData(DataFormats.FileDrop);
            e.Effects = dosyalar.Any(f =>
                f.EndsWith(".pcap",   StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
                ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var dosyalar = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pcap = dosyalar.FirstOrDefault(f =>
            f.EndsWith(".pcap",   StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase));
        if (pcap == null) return;
        MesajEkle("sistem", $"pcap sürüklendi → Wireshark'ta açılıyor: {Path.GetFileName(pcap)}");
        WiresharkIleAc(pcap);
    }

    // ─── 5.4 Bildirim Sesleri / Toast ────────────────────────────────

    private void ToastGoster(string mesaj, bool hata = false)
    {
        if (!_ayarlar.ToastAcik) return;
        ToastMetin.Text       = mesaj;
        ToastIkon.Text        = hata ? "✖" : "✔";
        ToastIkon.Foreground  = hata
            ? new SolidColorBrush(Color.FromRgb(248, 81, 73))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
        ToastBildirim.BorderBrush = hata
            ? new SolidColorBrush(Color.FromRgb(248, 81, 73))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
        ToastBildirim.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (s, _) => { ToastBildirim.Visibility = Visibility.Collapsed; ((System.Windows.Threading.DispatcherTimer)s!).Stop(); _toastTimer = null; };
        _toastTimer = t;
        t.Start();
    }

    private void BildirimCal(bool hata = false)
    {
        if (!_ayarlar.SesAcik) return;
        try
        {
            if (hata) System.Media.SystemSounds.Hand.Play();
            else      System.Media.SystemSounds.Asterisk.Play();
        }
        catch { }
    }

    // ─── 7. Cihaz Tarayıcı ───────────────────────────────────────────

    private sealed class KameraBilgi
    {
        public string    Ip             { get; init; } = "";
        public List<int> AcikPortlar    { get; } = new();
        public bool      OnvifBulundu   { get; set; }
        public bool      SsdpBulundu    { get; set; }
        public string?   OnvifServisUrl { get; set; }
        public string?   OnvifAdi       { get; set; }
        public string?   OnvifHardware  { get; set; }
        public string?   OnvifKonum     { get; set; }
        public string?   RtspDurum      { get; set; }
        public string?   SunucuBasligi  { get; set; }
        public string?   SayfaBasligi   { get; set; }
        public string?   NetbiosCihazAdi { get; set; }
        public string?   NetbiosGrupAdi  { get; set; }
        public string?   DnsAdi          { get; set; }
        public string?   PingAdi         { get; set; }
        public string?   SsdpLocation    { get; set; }
        public string?   SsdpSunucu      { get; set; }
        public string?   SsdpFriendlyName { get; set; }
        public string?   SsdpManufacturer { get; set; }
        public string?   SsdpModelName    { get; set; }
        public string?   SsdpModelNumber  { get; set; }
        public string?   MacAdresi        { get; set; }
        public string?   Uretici          { get; set; }
        public string?   AdvancedScannerAdi { get; set; }
        public string?   AdvancedScannerServisler { get; set; }
        public Dictionary<int, string> ServisDetaylari { get; } = new();
        public bool      PingYanit      { get; set; }
        public int       PingMs         { get; set; }
        public int       PingTtl        { get; set; }
        public string    MdnsMarka      { get; set; } = "";
        public string    MdnsTur        { get; set; } = "";
    }

    private sealed class CihazKimlik
    {
        public string  Marka   { get; set; } = "Bilinmiyor";
        public string? Model   { get; set; }
        public string  Tur     { get; set; } = "Cihaz";
        public string  TurIkon { get; set; } = "◈";
    }

    private static readonly int[] KameraPorts = { 554, 8000, 8080, 37777, 80, 8443, 22, 23, 139, 443, 445, 3389, 9000, 34567 };

    private static readonly (string Anahtar, string Marka, string Tur)[] MarkaTablosu =
    {
        ("hikvision",        "Hikvision",    "Kamera"),
        ("cross web server", "Dahua",        "Kamera"),
        ("dahua",            "Dahua",        "Kamera"),
        ("axis",             "Axis",         "Kamera"),
        ("reolink",          "Reolink",      "Kamera"),
        ("bosch",            "Bosch",        "Kamera"),
        ("hanwha",           "Hanwha",       "Kamera"),
        ("samsung techwin",  "Hanwha",       "Kamera"),
        ("vivotek",          "Vivotek",      "Kamera"),
        ("pelco",            "Pelco",        "Kamera"),
        ("flir",             "FLIR",         "Kamera"),
        ("uniview",          "Uniview",      "Kamera"),
        ("goahead-webs",     "IP Kamera",    "Kamera"),
        ("mini_httpd",       "IP Kamera",    "Kamera"),
        ("ubiquiti",         "Ubiquiti",     "Erişim Noktası"),
        ("unifi",            "Ubiquiti",     "Erişim Noktası"),
        ("airos",            "Ubiquiti",     "Erişim Noktası"),
        ("ubnt",             "Ubiquiti",     "Erişim Noktası"),
        ("mikrotik",         "MikroTik",     "Router/AP"),
        ("routeros",         "MikroTik",     "Router/AP"),
        ("tp-link",          "TP-Link",      "Switch/AP"),
        ("tplink",           "TP-Link",      "Switch/AP"),
        ("cisco",            "Cisco",        "Switch"),
        ("d-link",           "D-Link",       "Switch/AP"),
        ("dlink",            "D-Link",       "Switch/AP"),
        ("netgear",          "NETGEAR",      "Switch/AP"),
        ("zyxel",            "ZyXEL",        "Switch/AP"),
        ("tenda",            "Tenda",        "Switch/AP"),
        ("huawei",           "Huawei",       "Switch/AP"),
        ("h3c",              "H3C",          "Switch"),
        ("ruijie",           "Ruijie",       "Switch"),
        ("asus",             "ASUS",         "Router/AP"),
        ("witek",            "Witek",        "Kamera"),
        ("ttec",             "TTEC",         "Kamera"),
        ("stonet",           "Stonet",       "Kamera"),
        ("vstarcam",         "VStarCam",     "Kamera"),
        ("annke",            "ANNKE",        "Kamera"),
        ("amcrest",          "Amcrest",      "Kamera"),
        ("xmeye",            "XMeye",        "NVR/DVR"),
        ("web service root", "XMeye",        "NVR/DVR"),
        ("nvr",              "NVR",          "NVR/DVR"),
        ("synology",         "Synology",     "NAS"),
        ("qnap",             "QNAP",         "NAS"),
        ("mycloud",          "WD",           "NAS"),
        ("wd my",            "WD",           "NAS"),
        ("asustor",          "Asustor",      "NAS"),
        // Telefon / mobil
        ("android",          "Android",      "Telefon"),
        ("miui",             "Xiaomi",       "Telefon"),
        ("iphone",           "Apple",        "Telefon"),
        ("ipad",             "Apple",        "Tablet"),
        ("oneplus",          "OnePlus",      "Telefon"),
        ("oppo",             "OPPO",         "Telefon"),
        ("vivo ",            "Vivo",         "Telefon"),
        // Bilgisayar
        ("microsoft",        "Windows",      "Bilgisayar"),
        ("iis",              "Windows/IIS",  "Bilgisayar"),
        ("openwrt",          "OpenWrt",      "Router/AP"),
        ("dd-wrt",           "DD-WRT",       "Router/AP"),
        ("pfsense",          "pfSense",      "Güvenlik Duvarı"),
        ("fortinet",         "Fortinet",     "Güvenlik Duvarı"),
        ("fortigate",        "Fortinet",     "Güvenlik Duvarı"),
        ("sonicwall",        "SonicWall",    "Güvenlik Duvarı"),
        ("aruba",            "Aruba",        "Switch/AP"),
        ("juniper",          "Juniper",      "Switch"),
        ("procurve",         "HP ProCurve",  "Switch"),
        ("hpe",              "HPE",          "Switch"),
        ("hp laserjet",      "HP",           "Yazıcı"),
        ("laserjet",         "HP",           "Yazıcı"),
        ("hewlett packard",  "HP",           "Yazıcı"),
        ("seiko epson",      "Epson",        "Yazıcı"),
        ("epson",            "Epson",        "Yazıcı"),
        ("canon printer",    "Canon",        "Yazıcı"),
        ("brother",          "Brother",      "Yazıcı"),
        ("xerox",            "Xerox",        "Yazıcı"),
        ("kyocera",          "Kyocera",      "Yazıcı"),
    };

    private static CihazKimlik KimlikBelirle(KameraBilgi b)
    {
        var k      = new CihazKimlik();
        var birles = $"{b.SunucuBasligi} {b.SayfaBasligi} {b.OnvifAdi} {b.OnvifHardware} {b.SsdpFriendlyName} {b.SsdpManufacturer} {b.SsdpModelName} {b.SsdpSunucu} {b.Uretici} {b.AdvancedScannerAdi}".ToLowerInvariant();
        var kayitCihazi = KayitCihaziIpuclariVar(birles, b.AcikPortlar);
        var yazici = YaziciIpuclariVar(birles, b.AcikPortlar);

        // mDNS en güvenilir kaynak — önce uygula
        if (!string.IsNullOrEmpty(b.MdnsTur))
        {
            k.Tur = b.MdnsTur;
            if (!string.IsNullOrEmpty(b.MdnsMarka)) k.Marka = b.MdnsMarka;
        }

        if (yazici)
        {
            k.Tur = "Yazıcı";
            if (k.Marka == "Bilinmiyor")
            {
                if (birles.Contains("epson")) k.Marka = "Epson";
                else if (birles.Contains("hewlett packard") || birles.Contains("laserjet") || Regex.IsMatch(birles, @"\bhp\b")) k.Marka = "HP";
                else if (birles.Contains("canon")) k.Marka = "Canon";
                else if (birles.Contains("brother")) k.Marka = "Brother";
                else if (birles.Contains("xerox")) k.Marka = "Xerox";
                else if (birles.Contains("kyocera")) k.Marka = "Kyocera";
            }
        }

        // XVR/NVR/DVR ipuçları marka eşleşmesinden önce uygulanmalı; aksi halde
        // "Hikvision" veya "Dahua" başlığı cihazı yanlışlıkla kamera yapabilir.
        if (kayitCihazi && !yazici)
        {
            k.Tur = "NVR/DVR";
            if (birles.Contains("xmeye")) k.Marka = "XMeye";
        }

        // Kamera/ağ ekipmanı marka tespiti (mDNS'e rağmen daha spesifik olabilir)
        if (k.Tur == "Cihaz")
        {
            foreach (var (anahtar, marka, tur) in MarkaTablosu)
            {
                if (!birles.Contains(anahtar)) continue;
                k.Marka = marka; k.Tur = tur; break;
            }
        }
        else
        {
            // mDNS türü var ama marka bilinmiyorsa MarkaTablosu'dan marka al
            foreach (var (anahtar, marka, _) in MarkaTablosu)
            {
                if (!birles.Contains(anahtar)) continue;
                if (k.Marka == "Bilinmiyor") k.Marka = marka;
                break;
            }
        }

        // Port bazlı fallback
        if (kayitCihazi && !yazici && k.Marka == "Bilinmiyor")
        {
            if (birles.Contains("hikvision"))     k.Marka = "Hikvision";
            else if (birles.Contains("dahua"))    k.Marka = "Dahua";
            else if (birles.Contains("uniview"))  k.Marka = "Uniview";
        }

        if (k.Marka == "Bilinmiyor")
        {
            if (b.AcikPortlar.Contains(34567))                                        { k.Marka = "XMeye";     k.Tur = "NVR/DVR"; }
            else if (b.AcikPortlar.Contains(9000) && b.AcikPortlar.Contains(554))    {                         k.Tur = "NVR/DVR"; }
            else if (b.AcikPortlar.Contains(37777))                                   { k.Marka = "Dahua";     k.Tur = kayitCihazi ? "NVR/DVR" : "Kamera"; }
            else if (b.AcikPortlar.Contains(8000) && b.AcikPortlar.Contains(554))    { k.Marka = "Hikvision"; k.Tur = kayitCihazi ? "NVR/DVR" : "Kamera"; }
            else if (b.AcikPortlar.Contains(554))                                     {                         k.Tur = "Kamera"; }
            else if (!string.IsNullOrWhiteSpace(b.NetbiosCihazAdi))                  { k.Marka = "NetBIOS";   k.Tur = "Bilgisayar"; }
            else if (!string.IsNullOrWhiteSpace(b.DnsAdi) || !string.IsNullOrWhiteSpace(b.PingAdi))            {                         k.Tur = "Bilgisayar"; }
            else if (b.AcikPortlar.Contains(445) || b.AcikPortlar.Contains(3389))    {                         k.Tur = "Bilgisayar"; }
            else if (b.AcikPortlar.Contains(23))                                      {                         k.Tur = "Router/Switch"; }
        }

        // TTL tabanlı işletim sistemi tahmini (yalnızca hâlâ "Cihaz" olanlar için)
        if (k.Tur == "Cihaz" && b.PingYanit && b.PingTtl > 0)
        {
            if (b.PingTtl >= 120 && b.PingTtl <= 128)       k.Tur = "Bilgisayar"; // Windows TTL=128
            else if (b.PingTtl >= 250)                       k.Tur = "Router/Switch"; // Cisco/Juniper TTL=255
        }

        // Hostname tabanlı telefon tespiti (router DHCP'den gelen isimler)
        if (k.Tur is "Cihaz" or "Bilgisayar")
        {
            var adlar = $"{b.DnsAdi} {b.PingAdi} {b.AdvancedScannerAdi} {b.SsdpFriendlyName}".ToLowerInvariant();
            if (adlar.Contains("iphone"))                                              { k.Marka = "Apple";   k.Tur = "Telefon"; }
            else if (adlar.Contains("ipad"))                                           { k.Marka = "Apple";   k.Tur = "Tablet"; }
            else if (adlar.Contains("android-") || adlar.Contains("android_"))        {                       k.Tur = "Telefon"; }
            else if (adlar.Contains("galaxy"))                                         { k.Marka = "Samsung"; k.Tur = "Telefon"; }
            else if (adlar.Contains("redmi") || adlar.Contains("xiaomi") ||
                     adlar.Contains("poco"))                                           { k.Marka = "Xiaomi";  k.Tur = "Telefon"; }
            else if (adlar.Contains("pixel"))                                          { k.Marka = "Google";  k.Tur = "Telefon"; }
        }

        // OUI tabanlı telefon heuristiği: mobil üretici + sunucu portu yok
        if (k.Tur == "Cihaz" && !string.IsNullOrEmpty(b.Uretici))
        {
            var ureticiKucuk = b.Uretici.ToLowerInvariant();
            bool mobil = ureticiKucuk.Contains("apple") || ureticiKucuk.Contains("samsung") ||
                         ureticiKucuk.Contains("huawei") || ureticiKucuk.Contains("xiaomi") ||
                         ureticiKucuk.Contains("oneplus") || ureticiKucuk.Contains("oppo") ||
                         ureticiKucuk.Contains("vivo") || ureticiKucuk.Contains("realme") ||
                         ureticiKucuk.Contains("google") || ureticiKucuk.Contains("motorola") ||
                         ureticiKucuk.Contains("nokia") || ureticiKucuk.Contains("sony mobile") ||
                         ureticiKucuk.Contains("honor");
            bool sunucuPortuYok = !b.AcikPortlar.Any(p => p is 22 or 80 or 443 or 445 or 554 or 3389 or 8080 or 8443 or 8000);
            if (mobil && sunucuPortuYok)
            {
                k.Tur = "Telefon";
                if (k.Marka == "Bilinmiyor")
                {
                    if (ureticiKucuk.Contains("samsung"))      k.Marka = "Samsung";
                    else if (ureticiKucuk.Contains("apple"))   k.Marka = "Apple";
                    else if (ureticiKucuk.Contains("huawei"))  k.Marka = "Huawei";
                    else if (ureticiKucuk.Contains("xiaomi"))  k.Marka = "Xiaomi";
                    else if (ureticiKucuk.Contains("google"))  k.Marka = "Google";
                    else if (ureticiKucuk.Contains("motorola")) k.Marka = "Motorola";
                    else if (ureticiKucuk.Contains("honor"))   k.Marka = "Honor";
                    else                                        k.Marka = b.Uretici;
                }
            }
        }

        k.TurIkon = k.Tur switch
        {
            "Kamera"           => "◎",
            "NVR/DVR"          => "▣",
            "Bilgisayar"       => "▢",
            "NAS"              => "▦",
            "Sunucu"           => "▤",
            "Güvenlik Duvarı"  => "⊞",
            "Erişim Noktası"   => "⊛",
            "Router/AP"        => "⊛",
            "Router/Switch"    => "⊛",
            "Switch/AP"        => "◫",
            "Switch"           => "◫",
            "Telefon"          => "⊡",
            "Tablet"           => "▭",
            "Yazıcı"           => "▤",
            "Akıllı TV"        => "▣",
            "Apple TV"         => "▣",
            _                  => "◈",
        };

        // Model: UPnP/ONVIF model > sayfa başlığı (kısa ve anlamlı ise)
        k.Model = IlkDolu(
            b.SsdpModelName,
            b.SsdpModelNumber,
            b.OnvifHardware,
            AnlamliSayfaBasligi(b.SayfaBasligi));

        return k;
    }

    private static bool KayitCihaziIpuclariVar(string metin, ICollection<int> acikPortlar)
    {
        if (Regex.IsMatch(metin, @"(^|[^a-z0-9])(xvr|nvr|dvr)[a-z0-9-]*", RegexOptions.IgnoreCase)) return true;
        if (metin.Contains("network video recorder") || metin.Contains("digital video recorder")) return true;
        if (metin.Contains("hybrid video recorder") || metin.Contains("video recorder")) return true;
        if (Regex.IsMatch(metin, @"\b(ds-|dh-).*(xvr|nvr|dvr|ni|hghi|hqhi|huhi|ht)", RegexOptions.IgnoreCase)) return true;

        return acikPortlar.Contains(34567) ||
               (acikPortlar.Contains(9000) && acikPortlar.Contains(554));
    }

    private static bool YaziciIpuclariVar(string metin, ICollection<int> acikPortlar)
    {
        if (metin.Contains("laserjet") || metin.Contains("hewlett packard")) return true;
        if (metin.Contains("seiko epson") || metin.Contains("epson")) return true;
        if (metin.Contains("canon printer") || metin.Contains("brother") || metin.Contains("xerox") || metin.Contains("kyocera")) return true;
        if (metin.Contains("printer") || metin.Contains("multifunction") || metin.Contains("mfp")) return true;

        return acikPortlar.Contains(9100) || acikPortlar.Contains(515) || acikPortlar.Contains(631);
    }

    private static string? CihazAdiSec(KameraBilgi b)
        => IlkDolu(
            b.NetbiosCihazAdi,
            KisaHostAdi(b.DnsAdi),
            KisaHostAdi(b.PingAdi),
            b.OnvifAdi,
            b.SsdpFriendlyName,
            b.AdvancedScannerAdi);

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

    private static string? YerelSubnetiBul()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))
                    return $"{b[0]}.{b[1]}.{b[2]}";
            }
        }
        return null;
    }

    private readonly ObservableCollection<KameraSatir> _kameraSatirlari = new();
    private readonly Dictionary<string, KameraSatir> _kameraSatirlar = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KameraBilgi> _kameraBilgileri = new(StringComparer.Ordinal);
    private ICollectionView? _kameraSatirView;

    private void BtnKamera_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabCihazTara;
        if (string.IsNullOrEmpty(KameraSubnetBox.Text))
            KameraSubnetBox.Text = YerelSubnetiBul() ?? "";
    }

    private void KameraPanelKapat_Click(object sender, RoutedEventArgs e)
    {
        _kameraCts?.Cancel();
        MainTabControl.SelectedIndex = TabChatbot;
    }

    private void KameraSubnetBox_TextChanged(object sender, TextChangedEventArgs e)
        => KameraSubnetPlaceholder.Visibility = string.IsNullOrEmpty(KameraSubnetBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

    private void KameraKolonFiltre_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraIpFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraIpFiltreBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        KameraAdFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraAdFiltreBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        KameraMarkaFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraMarkaFiltreBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        KameraPortFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraPortFiltreBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        KameraMacFiltrePlaceholder.Visibility = string.IsNullOrWhiteSpace(KameraMacFiltreBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
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
        if (KameraDataGrid.SelectedItem is not KameraSatir satir)
            return;

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
        PortIpBox.Text = satir.Ip;
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

    private void KameraDisaAktarBtn_Click(object sender, RoutedEventArgs e)
    {
        KameraDisaAktarBtn.ContextMenu.PlacementTarget = KameraDisaAktarBtn;
        KameraDisaAktarBtn.ContextMenu.IsOpen = true;
    }

    private void KameraExportExcel_Click(object sender, RoutedEventArgs e)
        => KameraDisariAktar(KameraExportFormat.Excel);

    private void KameraExportPdf_Click(object sender, RoutedEventArgs e)
        => KameraDisariAktar(KameraExportFormat.Pdf);

    private void KameraExportTxt_Click(object sender, RoutedEventArgs e)
        => KameraDisariAktar(KameraExportFormat.Txt);

    private void KameraExportCsv_Click(object sender, RoutedEventArgs e)
        => KameraDisariAktar(KameraExportFormat.Csv);

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

    private enum KameraExportFormat { Excel, Pdf, Txt, Csv }

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
            KameraExportFormat.Excel => ("Excel Raporu (*.xls)|*.xls", "xls"),
            KameraExportFormat.Pdf   => ("PDF Raporu (*.pdf)|*.pdf", "pdf"),
            KameraExportFormat.Txt   => ("Metin Raporu (*.txt)|*.txt", "txt"),
            KameraExportFormat.Csv   => ("CSV Dosyası (*.csv)|*.csv", "csv"),
            _                        => ("Rapor (*.*)|*.*", "txt"),
        };

        var dlg = new SaveFileDialog
        {
            Title = "Cihaz Tara Sonuçlarını Dışa Aktar",
            Filter = filter,
            DefaultExt = ext,
            AddExtension = true,
            FileName = $"Cihaz_Tara_Raporu_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            switch (format)
            {
                case KameraExportFormat.Excel:
                    File.WriteAllText(dlg.FileName, KameraExcelHtmlOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Pdf:
                    File.WriteAllBytes(dlg.FileName, KameraPdfOlustur(satirlar));
                    break;
                case KameraExportFormat.Txt:
                    File.WriteAllText(dlg.FileName, KameraTxtOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Csv:
                    File.WriteAllText(dlg.FileName, KameraCsvOlustur(satirlar), new UTF8Encoding(true));
                    break;
            }

            MesajEkle("sonuc", $"✔ Cihaz Tara raporu kaydedildi: {Path.GetFileName(dlg.FileName)}");
            ToastGoster($"Dışa aktarıldı: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            HataBildir("Cihaz Tara dışa aktarma hatası", ex);
        }
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
        {
            yield return new[]
            {
                s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Ping, s.Portlar,
                s.Kesif, s.Mac, s.Uretici, s.Servis
            };
        }
    }

    private static readonly string[] KameraExportBasliklari =
    {
        "IP", "Ad", "Tür", "Marka", "Model", "Ping", "Portlar", "Keşif", "MAC", "Üretici", "Servis"
    };

    private static string KameraCsvOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", KameraExportBasliklari.Select(CsvHucre)));
        foreach (var row in KameraExportSatirlari(satirlar))
            sb.AppendLine(string.Join(";", row.Select(CsvHucre)));
        return sb.ToString();
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
            sb.AppendLine($"  Ping    : {s.Ping}");
            sb.AppendLine($"  Portlar : {s.Portlar}");
            sb.AppendLine($"  Keşif   : {s.Kesif}");
            sb.AppendLine($"  MAC     : {s.Mac}  {s.Uretici}");
            sb.AppendLine($"  Servis  : {s.Servis}");
            sb.AppendLine(new string('-', 110));
        }
        return sb.ToString();
    }

    private static string KameraExcelHtmlOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#0D1117;color:#C9D1D9;margin:24px}");
        sb.AppendLine("h1{color:#58A6FF;margin:0 0 6px;font-size:24px}.meta{color:#8B949E;margin:0 0 18px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;background:#0D1117}th{background:#0D3B66;color:#E6EDF3;text-align:left;padding:9px;border:1px solid #243147}");
        sb.AppendLine("td{padding:8px;border:1px solid #243147;vertical-align:top}tr:nth-child(even){background:#101722}.type{font-weight:600;color:#3FB950}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>Network Sniffer - Cihaz Tara Raporu</h1>");
        sb.AppendLine($"<div class=\"meta\">Tarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &nbsp;|&nbsp; Cihaz: {satirlar.Count}</div>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var baslik in KameraExportBasliklari)
            sb.Append("<th>").Append(WebUtility.HtmlEncode(baslik)).AppendLine("</th>");
        sb.AppendLine("</tr></thead><tbody>");
        foreach (var row in KameraExportSatirlari(satirlar))
        {
            sb.AppendLine("<tr>");
            for (int i = 0; i < row.Length; i++)
            {
                var cls = i == 2 ? " class=\"type\"" : "";
                sb.Append("<td").Append(cls).Append(">").Append(WebUtility.HtmlEncode(row[i])).AppendLine("</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table></body></html>");
        return sb.ToString();
    }

    private static byte[] KameraPdfOlustur(List<KameraSatir> satirlar)
    {
        var sayfalar = new List<string>();
        const int sayfaBasina = 18;
        for (int i = 0; i < satirlar.Count; i += sayfaBasina)
            sayfalar.Add(KameraPdfSayfaIcerigi(satirlar.Skip(i).Take(sayfaBasina).ToList(), (i / sayfaBasina) + 1, (satirlar.Count + sayfaBasina - 1) / sayfaBasina, satirlar.Count));

        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Array.Empty<byte>());
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        var pageObjectNumbers = new List<int>();
        foreach (var content in sayfalar)
        {
            var streamBytes = Encoding.ASCII.GetBytes(content);
            var contentObj = objects.Count + 1;
            objects.Add(Encoding.ASCII.GetBytes($"<< /Length {streamBytes.Length} >>\nstream\n{content}\nendstream"));
            var pageObj = objects.Count + 1;
            pageObjectNumbers.Add(pageObj);
            objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObj} 0 R >>"));
        }

        objects[1] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(n => $"{n} 0 R"))}] >>");

        using var ms = new MemoryStream();
        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        ms.Write(header, 0, header.Length);
        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            var objHeader = Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n");
            ms.Write(objHeader, 0, objHeader.Length);
            ms.Write(objects[i], 0, objects[i].Length);
            var objFooter = Encoding.ASCII.GetBytes("\nendobj\n");
            ms.Write(objFooter, 0, objFooter.Length);
        }
        var xref = ms.Position;
        var xrefHeader = Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        ms.Write(xrefHeader, 0, xrefHeader.Length);
        foreach (var offset in offsets.Skip(1))
        {
            var line = Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n");
            ms.Write(line, 0, line.Length);
        }
        var trailer = Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        ms.Write(trailer, 0, trailer.Length);
        return ms.ToArray();
    }

    private static string KameraPdfSayfaIcerigi(List<KameraSatir> satirlar, int sayfa, int toplamSayfa, int toplamCihaz)
    {
        var sb = new StringBuilder();
        sb.AppendLine("0.05 0.07 0.09 rg 0 0 595 842 re f");
        sb.AppendLine("0.05 0.23 0.40 rg 30 790 535 28 re f");
        sb.AppendLine("BT /F1 18 Tf 1 1 1 rg 42 808 Td (Network Sniffer - Cihaz Tara Raporu) Tj ET");
        sb.AppendLine($"BT /F1 9 Tf 0.75 0.80 0.86 rg 42 776 Td ({PdfMetin($"Tarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  Cihaz: {toplamCihaz}  |  Sayfa: {sayfa}/{toplamSayfa}")}) Tj ET");

        var y = 742;
        foreach (var s in satirlar)
        {
            sb.AppendLine("0.06 0.09 0.13 rg 30 " + (y - 4) + " 535 34 re f");
            sb.AppendLine($"BT /F1 10 Tf 0.36 0.65 1 rg 42 {y + 14} Td ({PdfMetin(s.Ip)}) Tj ET");
            sb.AppendLine($"BT /F1 10 Tf 0.23 0.72 0.31 rg 122 {y + 14} Td ({PdfMetin(MetniKirp(s.Tur, 18))}) Tj ET");
            sb.AppendLine($"BT /F1 10 Tf 0.90 0.93 0.96 rg 230 {y + 14} Td ({PdfMetin(MetniKirp(IlkDolu(s.Marka, s.Uretici) ?? "", 34))}) Tj ET");
            sb.AppendLine($"BT /F1 8 Tf 0.72 0.76 0.82 rg 42 {y} Td ({PdfMetin(MetniKirp($"{s.Ad} {s.Model}", 80))}) Tj ET");
            sb.AppendLine($"BT /F1 8 Tf 0.72 0.76 0.82 rg 42 {y - 12} Td ({PdfMetin(MetniKirp($"Port: {s.Portlar}  MAC: {s.Mac}  Servis: {s.Servis}", 110))}) Tj ET");
            y -= 40;
        }
        return sb.ToString();
    }

    private static string PdfMetin(string metin)
        => PdfAscii(metin).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string PdfAscii(string metin)
    {
        var sb = new StringBuilder(metin.Length);
        foreach (var ch in metin)
        {
            sb.Append(ch switch
            {
                'ı' => 'i', 'İ' => 'I', 'ğ' => 'g', 'Ğ' => 'G', 'ü' => 'u', 'Ü' => 'U',
                'ş' => 's', 'Ş' => 'S', 'ö' => 'o', 'Ö' => 'O', 'ç' => 'c', 'Ç' => 'C',
                >= ' ' and <= '~' => ch,
                _ => '?'
            });
        }
        return sb.ToString();
    }

    private static string MetniKirp(string? metin, int uzunluk)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        metin = Regex.Replace(metin.Trim(), @"\s+", " ");
        return metin.Length <= uzunluk ? metin : metin[..Math.Max(0, uzunluk - 1)] + "…";
    }

    private void KameraBaslatBtn_Click(object sender, RoutedEventArgs e) => _ = KameraTaramaBaslat();
    private void KameraDurdurBtn_Click(object sender, RoutedEventArgs e) => _kameraCts?.Cancel();

    private async Task NetbiosBilgileriniGuncelleAsync(
        string ip,
        KameraBilgi bilgi,
        System.Collections.Concurrent.ConcurrentDictionary<string, byte> denenenler,
        System.Collections.Concurrent.ConcurrentBag<string> logSatirlari,
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

            var ozet = string.Join(" / ", new[] { CihazAdiSec(bilgi), netbios.GrupAdi }.Where(x => !string.IsNullOrWhiteSpace(x)));
            logSatirlari.Add($"{ip} NetBIOS: {ozet}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
        finally
        {
            netbiosSem.Release();
        }
    }

    private async Task NetbiosSweepAsync(
        string subnet,
        System.Collections.Concurrent.ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        System.Collections.Concurrent.ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var sem = new SemaphoreSlim(64);
        var tasks = Enumerable.Range(1, 254).Select(i =>
        {
            var ip = $"{subnet}.{i}";
            return Task.Run(async () =>
            {
                await sem.WaitAsync(token);
                try
                {
                    var netbios = await NetbiosService.NodeStatusAsync(ip, token);
                    if (netbios is null || string.IsNullOrWhiteSpace(netbios.NetbiosAdi)) return;

                    var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                    bilgi.NetbiosCihazAdi ??= netbios.NetbiosAdi;
                    bilgi.NetbiosGrupAdi  ??= netbios.GrupAdi;
                    logSatirlari.Add($"{ip} NetBIOS UDP: {netbios.NetbiosAdi} {netbios.GrupAdi}");
                    await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                }
                catch { }
                finally { sem.Release(); }
            }, token);
        });

        await Task.WhenAll(tasks);
    }

    // ─── mDNS / Bonjour Sweep ────────────────────────────────────────────────

    private static readonly (string Servis, string Marka, string Tur)[] MdnsServisler =
    {
        ("_apple-mobdev2._tcp.local", "Apple",   "Telefon"),
        ("_apple-mobdev._tcp.local",  "Apple",   "Telefon"),
        ("_airplay._tcp.local",       "Apple",   "Apple TV"),
        ("_raop._tcp.local",          "Apple",   "Apple TV"),
        ("_home-sharing._tcp.local",  "Apple",   "Bilgisayar"),
        ("_googlecast._tcp.local",    "Google",  "Akıllı TV"),
        ("_ipp._tcp.local",           "",        "Yazıcı"),
        ("_printer._tcp.local",       "",        "Yazıcı"),
        ("_pdl-datastream._tcp.local","",        "Yazıcı"),
        ("_smb._tcp.local",           "",        "Bilgisayar"),
        ("_workstation._tcp.local",   "",        "Bilgisayar"),
        ("_ssh._tcp.local",           "",        "Bilgisayar"),
    };

    private async Task MdnsSweepAsync(
        string subnet,
        System.Collections.Concurrent.ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        System.Collections.Concurrent.ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var multicast = System.Net.IPAddress.Parse("224.0.0.251");
        const int mdnsPort = 5353;

        // Servis adından aranacak anahtar kelimeler (label kısmı, örn "_googlecast")
        var anahtarlar = MdnsServisler
            .Select(s => (Anahtar: s.Servis.Split('.')[0].ToLowerInvariant(), s.Marka, s.Tur))
            .ToArray();

        try
        {
            using var udp = new System.Net.Sockets.UdpClient(System.Net.Sockets.AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                       System.Net.Sockets.SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, mdnsPort));
            udp.JoinMulticastGroup(multicast);
            udp.MulticastLoopback = false;

            // Tüm servis türleri için sorgu gönder
            var hedefEp = new System.Net.IPEndPoint(multicast, mdnsPort);
            foreach (var (servis, _, _) in MdnsServisler)
            {
                try
                {
                    var sorgu = OlusturMdnsSorgusu(servis);
                    await udp.SendAsync(sorgu, sorgu.Length, hedefEp);
                    await Task.Delay(20, token);
                }
                catch { }
            }

            // 4 saniye dinle
            var bitis = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < bitis && !token.IsCancellationRequested)
            {
                try
                {
                    using var zaman = CancellationTokenSource.CreateLinkedTokenSource(token);
                    zaman.CancelAfter(500);
                    var alindi = await udp.ReceiveAsync(zaman.Token);

                    var kaynakIp = alindi.RemoteEndPoint.Address.ToString();
                    if (!kaynakIp.StartsWith(subnet + ".")) continue;

                    var (marka, tur) = MdnsPaketCoz(alindi.Buffer, anahtarlar);
                    if (tur == null) continue;

                    var bilgi = bulunanlar.GetOrAdd(kaynakIp, new KameraBilgi { Ip = kaynakIp });
                    if (string.IsNullOrEmpty(bilgi.MdnsTur))
                    {
                        bilgi.MdnsTur   = tur;
                        bilgi.MdnsMarka = marka;
                        logSatirlari.Add($"mDNS: {kaynakIp} → {tur} ({marka})");
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }
        catch (Exception ex)
        {
            logSatirlari.Add($"mDNS hata: {ex.Message}");
        }
    }

    private static byte[] OlusturMdnsSorgusu(string servisAdi)
    {
        var adBytes = new List<byte>();
        foreach (var etiket in servisAdi.Split('.'))
        {
            var b = Encoding.ASCII.GetBytes(etiket);
            adBytes.Add((byte)b.Length);
            adBytes.AddRange(b);
        }
        adBytes.Add(0);

        var paket = new byte[12 + adBytes.Count + 4];
        paket[5] = 1; // QDCOUNT = 1
        adBytes.CopyTo(0, paket, 12, adBytes.Count);
        paket[12 + adBytes.Count + 1] = 0x0C; // Type PTR
        paket[12 + adBytes.Count + 3] = 0x01; // Class IN
        return paket;
    }

    private static (string Marka, string? Tur) MdnsPaketCoz(
        byte[] veri,
        (string Anahtar, string Marka, string Tur)[] anahtarlar)
    {
        // DNS etiketleri pakette ASCII olarak bulunur; doğrudan arama yeterli
        var str = Encoding.Latin1.GetString(veri).ToLowerInvariant();
        foreach (var (anahtar, marka, tur) in anahtarlar)
            if (str.Contains(anahtar))
                return (marka, tur);
        return ("", null);
    }

    private async Task KameraTaramaBaslat()
    {
        var subnet = KameraSubnetBox.Text.Trim();
        if (string.IsNullOrEmpty(subnet))
        {
            subnet = YerelSubnetiBul() ?? "";
            KameraSubnetBox.Text = subnet;
        }

        _kameraSatirlari.Clear();
        _kameraSatirlar.Clear();
        _kameraBilgileri.Clear();
        KameraFiltreSayacText.Text = "0 cihaz";
        KameraResultBorder.Visibility   = Visibility.Visible;
        KameraIlerlemeText.Visibility   = Visibility.Visible;
        KameraBaslatBtn.IsEnabled       = false;
        KameraDurdurBtn.Visibility      = Visibility.Visible;
        KameraIlerlemeText.Text         = "Başlatılıyor...";

        if (string.IsNullOrEmpty(subnet))
        {
            KameraKutucugaYaz("✖ Subnet tespit edilemedi — manuel girin (örn: 192.168.1)", "#F85149");
            KameraBaslatBtn.IsEnabled  = true;
            KameraDurdurBtn.Visibility = Visibility.Collapsed;
            return;
        }

        _kameraCts?.Cancel();
        _kameraCts = new CancellationTokenSource();
        var token  = _kameraCts.Token;

        var bulunanlar        = new System.Collections.Concurrent.ConcurrentDictionary<string, KameraBilgi>(StringComparer.Ordinal);
        var netbiosDenenenler = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var logSatirlari      = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var netbiosSem  = new SemaphoreSlim(16);
        int taranan           = 0;

        KameraKutucugaYaz($"Subnet  : {subnet}.1 – {subnet}.254", "#8B949E");
        KameraKutucugaYaz($"Portlar : {string.Join(", ", KameraPorts)}", "#484F58");
        KameraKutucugaYaz($"Kaynak  : ICMP + TCP port + DNS + NetBIOS + ONVIF + SSDP + ARP + Advanced IP Scanner", "#484F58");
        KameraKutucugaYaz("─────────────────────────", "#30363D");

        try
        {
            // ── Port taraması (paralel, tüm subnet) ──────────────────
            var portTask = Task.Run(async () =>
            {
                var sem   = new SemaphoreSlim(80);
                var tasks = Enumerable.Range(1, 254).Select(i =>
                {
                    var ip = $"{subnet}.{i}";
                    return Task.Run(async () =>
                    {
                        await sem.WaitAsync(token);
                        try
                        {
                            var acik = new List<int>();
                            foreach (var port in KameraPorts)
                            {
                                if (token.IsCancellationRequested) break;
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

                            if (acik.Count > 0)
                            {
                                var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                                lock (bilgi.AcikPortlar) bilgi.AcikPortlar.AddRange(acik.Except(bilgi.AcikPortlar));

                                if (acik.Contains(554))
                                    bilgi.RtspDurum = await RtspHizliKontrol(ip, 554, token);

                                await ServisDetaylariniGuncelleAsync(ip, bilgi, acik, token);

                                // HTTP banner — ilk açık HTTP portunu dene
                                foreach (var hp in new[] { 80, 8080, 8443, 443, 9000 })
                                {
                                    if (!acik.Contains(hp)) continue;
                                    var (sunucu, baslik) = await HttpBannerOku(ip, hp, token);
                                    bilgi.SunucuBasligi = sunucu;
                                    bilgi.SayfaBasligi  = baslik;
                                    break;
                                }

                                if (acik.Any(p => p is 139 or 445 or 3389))
                                    await NetbiosBilgileriniGuncelleAsync(ip, bilgi, netbiosDenenenler, logSatirlari, netbiosSem, token);

                                logSatirlari.Add($"{ip}: port={string.Join(",", bilgi.AcikPortlar)} marka={KimlikBelirle(bilgi).Marka}");
                                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                            }
                        }
                        finally
                        {
                            sem.Release();
                            int n = Interlocked.Increment(ref taranan);
                            if (n % 32 == 0)
                                await Dispatcher.InvokeAsync(() => KameraIlerlemeText.Text = $"Cihaz tarama: {n}/254 kontrol edildi…");
                        }
                    }, token);
                });
                await Task.WhenAll(tasks);
            }, token);

            // ── ONVIF WS-Discovery (4 sn) ────────────────────────────
            var onvifTask = Task.Run(async () =>
            {
                try
                {
                    string probe = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Envelope xmlns=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:tns=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\" xmlns:wsa=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\"><Header><wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action><wsa:MessageID>uuid:{Guid.NewGuid()}</wsa:MessageID><wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To></Header><Body><tns:Probe><tns:Types>dn:NetworkVideoTransmitter</tns:Types></tns:Probe></Body></Envelope>";
                    var bytes = Encoding.UTF8.GetBytes(probe);
                    using var udp = new UdpClient();
                    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));

                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts2.CancelAfter(4000);
                    while (!cts2.Token.IsCancellationRequested)
                    {
                        var res = await udp.ReceiveAsync(cts2.Token);
                        var xml = Encoding.UTF8.GetString(res.Buffer);
                        if (!xml.Contains("ProbeMatch")) continue;

                        var ip     = res.RemoteEndPoint.Address.ToString();
                        var xM     = Regex.Match(xml, @"<[^>]*XAddrs[^>]*>([^<]+)</[^>]*XAddrs>");
                        var scopes = Regex.Matches(xml, @"onvif://www\.onvif\.org/(\w+)/([^<\s""]+)");
                        var bilgi  = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                        bilgi.OnvifBulundu   = true;
                        bilgi.OnvifServisUrl = xM.Success ? xM.Groups[1].Value.Trim().Split(' ')[0] : null;

                        var scopeDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (Match m in scopes) scopeDict[m.Groups[1].Value] = Uri.UnescapeDataString(m.Groups[2].Value);
                        if (scopeDict.TryGetValue("hardware", out var hw)) bilgi.OnvifHardware = TemizKimlikMetni(hw);
                        if (scopeDict.TryGetValue("name", out var nm)) bilgi.OnvifAdi = TemizKimlikMetni(nm);
                        if (scopeDict.TryGetValue("location", out var loc)) bilgi.OnvifKonum = TemizKimlikMetni(loc);
                        if (!string.IsNullOrWhiteSpace(bilgi.OnvifAdi) || !string.IsNullOrWhiteSpace(bilgi.OnvifHardware))
                            logSatirlari.Add($"{ip} ONVIF: {bilgi.OnvifAdi} {bilgi.OnvifHardware}");

                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            });

            // ── SSDP / UPnP (3 sn) ──────────────────────────────────
            var ssdpTask = Task.Run(async () =>
            {
                try
                {
                    var bytes = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n");
                    using var udp = new UdpClient();
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

                    using var cts3 = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts3.CancelAfter(3000);
                    while (!cts3.Token.IsCancellationRequested)
                    {
                        var res  = await udp.ReceiveAsync(cts3.Token);
                        var resp = Encoding.UTF8.GetString(res.Buffer);
                        var ip    = res.RemoteEndPoint.Address.ToString();
                        if (!ip.StartsWith(subnet + ".")) continue;
                        var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                        bilgi.SsdpBulundu = true;

                        var headers = HttpBasliklariniParse(resp);
                        if (headers.TryGetValue("SERVER", out var ssdpServer)) bilgi.SsdpSunucu = TemizKimlikMetni(ssdpServer);
                        if (headers.TryGetValue("LOCATION", out var location))
                        {
                            bilgi.SsdpLocation = location.Trim();
                            var ssdpDetay = await SsdpDetayOku(bilgi.SsdpLocation, token);
                            bilgi.SsdpFriendlyName = ssdpDetay.FriendlyName ?? bilgi.SsdpFriendlyName;
                            bilgi.SsdpManufacturer = ssdpDetay.Manufacturer ?? bilgi.SsdpManufacturer;
                            bilgi.SsdpModelName    = ssdpDetay.ModelName ?? bilgi.SsdpModelName;
                            bilgi.SsdpModelNumber  = ssdpDetay.ModelNumber ?? bilgi.SsdpModelNumber;
                        }

                        logSatirlari.Add($"{ip} UPnP/SSDP: {IlkDolu(bilgi.SsdpFriendlyName, bilgi.SsdpManufacturer, bilgi.SsdpModelName, bilgi.SsdpSunucu)}");
                        await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            });

            // ── Ping Sweep (tüm subnet paralel ICMP) ─────────────────
            var pingSweepTask = Task.Run(async () =>
            {
                var sem   = new SemaphoreSlim(64);
                var tasks = Enumerable.Range(1, 254).Select(i =>
                {
                    var ip = $"{subnet}.{i}";
                    return Task.Run(async () =>
                    {
                        await sem.WaitAsync(token);
                        try
                        {
                            using var ping = new System.Net.NetworkInformation.Ping();
                            var reply = await ping.SendPingAsync(ip, 1000);
                            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                var bilgi = bulunanlar.GetOrAdd(ip, new KameraBilgi { Ip = ip });
                                bilgi.PingYanit = true;
                                bilgi.PingMs    = (int)reply.RoundtripTime;
                                bilgi.PingTtl   = reply.Options?.Ttl ?? 0;
                                await NetbiosBilgileriniGuncelleAsync(ip, bilgi, netbiosDenenenler, logSatirlari, netbiosSem, token);
                                await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
                            }
                        }
                        catch { }
                        finally { sem.Release(); }
                    }, token);
                });
                await Task.WhenAll(tasks);
            }, token);

            var mdnsTask = Task.Run(
                () => MdnsSweepAsync(subnet, bulunanlar, logSatirlari, token),
                token);

            var advancedScannerTask = Task.Run(
                () => AdvancedScannerKayitlariniIsleAsync(subnet, bulunanlar, logSatirlari, token),
                token);
            var netbiosSweepTask = Task.Run(
                () => NetbiosSweepAsync(subnet, bulunanlar, logSatirlari, token),
                token);

            await Task.WhenAll(portTask, onvifTask, ssdpTask, pingSweepTask, mdnsTask, advancedScannerTask, netbiosSweepTask);
            await ArpBilgileriniTopluGuncelleAsync(bulunanlar, logSatirlari, token);

            var sonuc = token.IsCancellationRequested
                ? $"■ Durduruldu — {bulunanlar.Count} cihaz bulundu"
                : $"✔ Tamamlandı — {bulunanlar.Count} cihaz bulundu";
            KameraKutucugaYaz("─────────────────────────", "#30363D");
            KameraKutucugaYaz(sonuc, bulunanlar.Count > 0 ? "#3FB950" : "#D29922");
            KameraIlerlemeText.Text = sonuc;
            LogService.Kaydet("CİHAZ TARA", $"{subnet}.0/24", logSatirlari.ToList());
        }
        catch (OperationCanceledException)
        {
            KameraIlerlemeText.Text = "Tarama durduruldu.";
        }
        catch (Exception ex)
        {
            KameraKutucugaYaz($"✖ {ex.Message}", "#F85149");
        }
        finally
        {
            KameraBaslatBtn.IsEnabled  = true;
            KameraDurdurBtn.Visibility = Visibility.Collapsed;
        }
    }

    private static async Task<(string? Sunucu, string? Baslik)> HttpBannerOku(string ip, int port, CancellationToken token)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2500);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2500;
            var req = Encoding.ASCII.GetBytes($"GET / HTTP/1.0\r\nHost: {ip}\r\nUser-Agent: AgTarama/1.0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(req, cts.Token);
            var buf = new byte[4096];
            int n   = await stream.ReadAsync(buf, cts.Token);
            var resp = Encoding.UTF8.GetString(buf, 0, n);

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

    private static async Task ServisDetaylariniGuncelleAsync(string ip, KameraBilgi bilgi, IEnumerable<int> acikPortlar, CancellationToken token)
    {
        foreach (var port in acikPortlar)
        {
            if (!BilindikPortlar.TryGetValue(port, out var servis)) servis = "Bilinmeyen";
            var banner = await PortBannerOku(ip, port, token);
            var detay = banner == null ? servis : $"{servis} - {banner}";
            lock (bilgi.ServisDetaylari) bilgi.ServisDetaylari[port] = detay;
        }
    }

    private static async Task<string?> PortBannerOku(string ip, int port, CancellationToken token)
    {
        if (port is 80 or 8080 or 8443 or 443 or 9000 or 554 or 445 or 3389) return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(1200);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, port, cts.Token);
            using var stream = tcp.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 1200;

            if (port is 23 or 21 or 22 or 25 or 110 or 143)
            {
                var buf = new byte[256];
                int n = await stream.ReadAsync(buf, cts.Token);
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2000);
            using var tcp = new TcpClient();
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

    private static Dictionary<string, string> HttpBasliklariniParse(string yanit)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in yanit.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return headers;
    }

    private static async Task<(string? FriendlyName, string? Manufacturer, string? ModelName, string? ModelNumber)> SsdpDetayOku(string location, CancellationToken token)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri)) return default;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(2200);
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(2200) };
            var xml = await client.GetStringAsync(uri, cts.Token);

            return (
                XmlEtiketiOku(xml, "friendlyName"),
                XmlEtiketiOku(xml, "manufacturer"),
                XmlEtiketiOku(xml, "modelName"),
                XmlEtiketiOku(xml, "modelNumber"));
        }
        catch { return default; }
    }

    private static string? XmlEtiketiOku(string xml, string etiket)
    {
        var match = Regex.Match(xml, $@"<{Regex.Escape(etiket)}(?:\s[^>]*)?>(?<v>.*?)</{Regex.Escape(etiket)}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success ? TemizKimlikMetni(match.Groups["v"].Value) : null;
    }

    private async Task AdvancedScannerKayitlariniIsleAsync(
        string subnet,
        System.Collections.Concurrent.ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        System.Collections.Concurrent.ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var kayitlar = await AdvancedIpScannerService.TaraAsync(subnet, token);
        foreach (var kayit in kayitlar)
        {
            if (token.IsCancellationRequested) break;
            var bilgi = bulunanlar.GetOrAdd(kayit.Ip, new KameraBilgi { Ip = kayit.Ip });
            bilgi.AdvancedScannerAdi = TemizKimlikMetni(kayit.Ad);
            bilgi.AdvancedScannerServisler = TemizKimlikMetni(kayit.Servisler);
            if (!string.IsNullOrWhiteSpace(kayit.Mac)) bilgi.MacAdresi = MacFormatla(kayit.Mac);
            bilgi.Uretici = IlkDolu(kayit.Uretici, bilgi.Uretici, UreticiAra(bilgi.MacAdresi));

            logSatirlari.Add($"{kayit.Ip} Advanced IP Scanner: {CihazAdiSec(bilgi)} {bilgi.MacAdresi} {bilgi.Uretici}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
    }

    private async Task ArpBilgileriniTopluGuncelleAsync(
        System.Collections.Concurrent.ConcurrentDictionary<string, KameraBilgi> bulunanlar,
        System.Collections.Concurrent.ConcurrentBag<string> logSatirlari,
        CancellationToken token)
    {
        var arp = await ArpTablosuOkuAsync(token);
        foreach (var (ip, bilgi) in bulunanlar)
        {
            if (!arp.TryGetValue(ip, out var mac)) continue;
            bilgi.MacAdresi = MacFormatla(mac);
            bilgi.Uretici = IlkDolu(bilgi.Uretici, UreticiAra(bilgi.MacAdresi));
            logSatirlari.Add($"{ip} ARP: {bilgi.MacAdresi} {bilgi.Uretici}");
            await Dispatcher.InvokeAsync(() => KameraKartEkleVeyaGuncelle(bilgi));
        }
    }

    private static async Task<Dictionary<string, string>> ArpTablosuOkuAsync(CancellationToken token)
    {
        var sonuc = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "arp",
                    Arguments              = "-a",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                },
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            foreach (Match m in Regex.Matches(output, @"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9A-Fa-f]{2}(?:[-:][0-9A-Fa-f]{2}){5})"))
                sonuc[m.Groups["ip"].Value] = MacFormatla(m.Groups["mac"].Value) ?? m.Groups["mac"].Value;
        }
        catch { }

        return sonuc;
    }

    private static readonly object MacDbLock = new();
    private static Dictionary<string, string>? _ipScannerMacDb;

    private static string? UreticiAra(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var yerel = OuiAra(mac);
        if (!string.IsNullOrWhiteSpace(yerel)) return yerel;

        var prefix = Regex.Replace(mac, @"[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (prefix.Length < 6) return null;
        prefix = prefix[..6];

        lock (MacDbLock)
        {
            _ipScannerMacDb ??= IpScannerMacDbYukle();
            return _ipScannerMacDb.TryGetValue(prefix, out var uretici) ? uretici : null;
        }
    }

    private static Dictionary<string, string> IpScannerMacDbYukle()
    {
        var db = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(Paths.IpScannerMacDb)) return db;
            foreach (var line in File.ReadLines(Paths.IpScannerMacDb))
            {
                var match = Regex.Match(line, @"^(?<hex>[0-9A-Fa-f]{12})\s+(?<vendor>.+)$");
                if (!match.Success) continue;
                var hex = match.Groups["hex"].Value.ToUpperInvariant();
                if (!hex.EndsWith("FFFFFF", StringComparison.Ordinal)) continue;
                var prefix = hex[..6];
                db.TryAdd(prefix, TemizKimlikMetni(match.Groups["vendor"].Value) ?? match.Groups["vendor"].Value.Trim());
            }
        }
        catch { }
        return db;
    }

    private static string? MacFormatla(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var hex = Regex.Replace(mac, @"[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (hex.Length != 12) return mac.Trim();
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }

    private void KameraKartEkleVeyaGuncelle(KameraBilgi bilgi)
    {
        _kameraBilgileri[bilgi.Ip] = bilgi;
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

    private KameraSatir KameraSatirOlustur(KameraBilgi bilgi)
    {
        var kim = KimlikBelirle(bilgi);
        var cihazAdi = CihazAdiSec(bilgi) ?? "";

        List<int> portlar;
        lock (bilgi.AcikPortlar) portlar = bilgi.AcikPortlar.Order().ToList();

        List<string> servisler;
        lock (bilgi.ServisDetaylari)
            servisler = bilgi.ServisDetaylari.OrderBy(x => x.Key).Select(x => $"{x.Key}/{x.Value}").ToList();

        var kesifler = new List<string>();
        if (bilgi.OnvifBulundu) kesifler.Add("ONVIF");
        if (bilgi.SsdpBulundu) kesifler.Add("UPnP");

        return new KameraSatir
        {
            Ip = bilgi.Ip,
            Ad = cihazAdi,
            Tur = kim.Tur,
            Marka = kim.Marka == "Bilinmiyor" ? "" : kim.Marka,
            Model = kim.Model ?? "",
            Ping = bilgi.PingYanit ? $"{bilgi.PingMs} ms" : "",
            PingMs = bilgi.PingYanit ? bilgi.PingMs : int.MaxValue,
            Portlar = string.Join(", ", portlar),
            Kesif = string.Join(", ", kesifler),
            Mac = bilgi.MacAdresi ?? "",
            Uretici = bilgi.Uretici ?? "",
            Servis = string.Join(" | ", servisler.DefaultIfEmpty(IlkDolu(bilgi.AdvancedScannerServisler, bilgi.SunucuBasligi, bilgi.SayfaBasligi, bilgi.RtspDurum) ?? "")),
            WebUrl = KameraWebUrlSec(bilgi),
        };
    }

    private static string? KameraWebUrlSec(KameraBilgi bilgi)
    {
        foreach (var (port, scheme) in new (int, string)[] { (80, "http"), (443, "https"), (8080, "http"), (8443, "https"), (9000, "http") })
        {
            if (!bilgi.AcikPortlar.Contains(port)) continue;
            return port is 80 or 443 ? $"{scheme}://{bilgi.Ip}/" : $"{scheme}://{bilgi.Ip}:{port}/";
        }
        return null;
    }

    private void KameraFiltreleriUygula()
    {
        _kameraSatirView?.Refresh();
        int toplam = _kameraSatirlari.Count;
        int gorunen = _kameraSatirView?.Cast<object>().Count() ?? toplam;
        KameraFiltreSayacText.Text = toplam == 0 ? "0 cihaz" : $"{gorunen}/{toplam} cihaz";
    }

    private bool KameraSatirFiltredenGecer(object obj)
    {
        if (obj is not KameraSatir satir) return false;

        var tur = (KameraTurFiltreBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Hepsi";
        if (!string.Equals(tur, "Hepsi", StringComparison.OrdinalIgnoreCase) &&
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

    public sealed class KameraSatir : INotifyPropertyChanged
    {
        private string _ip = "";
        private string _ad = "";
        private string _tur = "";
        private string _marka = "";
        private string _model = "";
        private string _ping = "";
        private int _pingMs = int.MaxValue;
        private string _portlar = "";
        private string _kesif = "";
        private string _mac = "";
        private string _uretici = "";
        private string _servis = "";
        private string? _webUrl;

        public string Ip { get => _ip; set => Set(ref _ip, value); }
        public string Ad { get => _ad; set => Set(ref _ad, value); }
        public string Tur { get => _tur; set => Set(ref _tur, value); }
        public string Marka { get => _marka; set => Set(ref _marka, value); }
        public string Model { get => _model; set => Set(ref _model, value); }
        public string Ping { get => _ping; set => Set(ref _ping, value); }
        public int PingMs { get => _pingMs; set => Set(ref _pingMs, value); }
        public string Portlar { get => _portlar; set => Set(ref _portlar, value); }
        public string Kesif { get => _kesif; set => Set(ref _kesif, value); }
        public string Mac { get => _mac; set => Set(ref _mac, value); }
        public string Uretici { get => _uretici; set => Set(ref _uretici, value); }
        public string Servis { get => _servis; set => Set(ref _servis, value); }
        public string? WebUrl { get => _webUrl; set => Set(ref _webUrl, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Kopyala(KameraSatir diger)
        {
            Ip = diger.Ip;
            Ad = diger.Ad;
            Tur = diger.Tur;
            Marka = diger.Marka;
            Model = diger.Model;
            Ping = diger.Ping;
            PingMs = diger.PingMs;
            Portlar = diger.Portlar;
            Kesif = diger.Kesif;
            Mac = diger.Mac;
            Uretici = diger.Uretici;
            Servis = diger.Servis;
            WebUrl = diger.WebUrl;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

}
