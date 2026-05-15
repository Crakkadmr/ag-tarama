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

    // ─── Lisans iptal kontrolü ───────────────────────────────────────
    public bool LisansIptal { get; private set; }
    public CancellationTokenSource MasterCts { get; } = new();

    public void LisansIptalEt()
    {
        LisansIptal = true;
        MasterCts.Cancel();
    }

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
    private const int TabGecmis    = 9;
    private const int TabWlan      = 10;
    private const int TabLisans    = 11;

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
    private int _bantAralikSn = 300; // 5 dk default

    // ─── Lisans banner (oturum başı gizle durumu) ─────────────────────
    private bool _lisansBannerGizle = false;

    // ─── Toast ────────────────────────────────────────────────────────
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    // ─── Mesaj geçmişi (HTML rapor için) ─────────────────────────────
    private readonly List<(string Tur, string Metin, string Zaman)> _mesajGecmisi = new();
    private List<HistoryRecord> _gecmisKayitlari = new();
    private string _gecmisFiltreTur = "";
    private bool _gecmisdenCalistiriliyor = false;

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
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v0.1.0";
        MesajEkle("sistem", "Network Sniffer başlatıldı — made by demircan.");
        WlanPanelBaşlat();
        KonsoleBaslat();
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
}
