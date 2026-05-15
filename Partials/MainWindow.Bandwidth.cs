using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
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

    private void BantAralikBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !int.TryParse(btn.Tag?.ToString(), out var sn)) return;
        _bantAralikSn = sn;

        // Aktif buton stilini güncelle
        foreach (var b in new[] { BantBtn5dk, BantBtn15dk, BantBtn60dk })
            b.Style = (Style)FindResource("ChipButton");
        btn.Style = (Style)FindResource("ActiveActionButton");

        BantGrafigiVeStatlariGuncelle();
    }

    private async void BantPerAppBtn_Click(object sender, RoutedEventArgs e)
    {
        var isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        if (!isAdmin)
        {
            BantPerAppPanel.Children.Clear();
            BantPerAppPanel.Children.Add(new TextBlock
            {
                Text       = "⚠ Per-uygulama trafik görünümü için yönetici hakları gerekli.",
                Foreground = new SolidColorBrush(Color.FromRgb(0xD2, 0x9A, 0x22)),
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 11,
                Margin     = new Thickness(0, 4, 0, 4),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        BantPerAppPanel.Children.Clear();
        BantPerAppPanel.Children.Add(new TextBlock
        {
            Text       = "netstat -bo çalışıyor…",
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 11,
        });

        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netstat", "-bno")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    Verb                   = "runas",
                }
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            // Parse: her Local Address'in hemen altındaki "[Process.exe]" satırı
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string? lastProcess = null;
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    lastProcess = trimmed[1..^1];
                    if (!dict.ContainsKey(lastProcess)) dict[lastProcess] = 0;
                    dict[lastProcess]++;
                }
            }

            BantPerAppPanel.Children.Clear();
            var top5 = dict.OrderByDescending(kv => kv.Value).Take(5).ToList();
            if (top5.Count == 0)
            {
                BantPerAppPanel.Children.Add(new TextBlock
                {
                    Text = "Aktif bağlantı bulunamadı.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                });
                return;
            }

            foreach (var (proc2, cnt) in top5)
            {
                var satir = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                satir.Children.Add(new TextBlock
                {
                    Text = proc2,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    MinWidth = 180,
                });
                satir.Children.Add(new TextBlock
                {
                    Text = $"{cnt} bağlantı",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                });
                BantPerAppPanel.Children.Add(satir);
            }
        }
        catch (Exception ex)
        {
            BantPerAppPanel.Children.Clear();
            BantPerAppPanel.Children.Add(new TextBlock
            {
                Text = $"Hata: {ex.Message}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private void BantIzlemeBaslat()
    {
        _bantOnceki.Clear();
        BantAdaptorPanel.Children.Clear();

        // İlk snapshot al
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = ni.GetIPv4Statistics();
            _bantOnceki[ni.Id] = (stats.BytesReceived, stats.BytesSent, Environment.TickCount64);
        }

        _bantTimer = new System.Windows.Threading.DispatcherTimer
        { Interval = TimeSpan.FromSeconds(1) };
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

        if (adaptorler.Count == 0)
        {
            BantDurumText.Text = "Aktif adaptör bulunamadı.";
            return;
        }

        BantDurumText.Text = $"{adaptorler.Count} adaptör — {DateTime.Now:HH:mm:ss}";

        double totalRx = 0, totalTx = 0;

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
            BandwidthHistoryService.RecordTick(rxHiz, txHiz);

            totalRx += rxHiz;
            totalTx += txHiz;

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

        BandwidthHistoryService.RecordTick(totalRx, totalTx);
        BantGrafigiVeStatlariGuncelle();
    }

    private void BantGrafigiVeStatlariGuncelle()
    {
        // Grafik çiz
        BantGrafiginiCiz();

        // Stat bilgilerini güncelle
        var (peakRx, peakTx, avgRx, avgTx, totRx, totTx) = BandwidthHistoryService.Stats(_bantAralikSn);
        BantStatPeakRx.Text = BantHizFormatla((long)peakRx);
        BantStatPeakTx.Text = BantHizFormatla((long)peakTx);
        BantStatAvgRx.Text  = BantHizFormatla((long)avgRx);
        BantStatAvgTx.Text  = BantHizFormatla((long)avgTx);
        BantStatToplam.Text = $"↓{totRx} MB  ↑{totTx} MB";
    }

    private void BantGrafiginiCiz()
    {
        BantGrafikCanvas.Children.Clear();
        var w = BantGrafikCanvas.ActualWidth;
        var h = BantGrafikCanvas.ActualHeight;
        if (w <= 10 || h <= 10) return;

        var (rx, tx) = BandwidthHistoryService.GetAggregate(_bantAralikSn);
        if (rx.Length < 2) return;

        double maxVal = Math.Max(rx.Concat(tx).DefaultIfEmpty(1).Max(), 1);
        double xStep  = w / (rx.Length - 1);
        double margin  = 6;
        double chartH  = h - margin * 2;

        // Arka plan grid çizgisi
        for (int i = 1; i < 4; i++)
        {
            double yLine = margin + chartH * i / 4;
            var line = new Line
            {
                X1 = 0, X2 = w, Y1 = yLine, Y2 = yLine,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 0x58, 0xA6, 0xFF)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 },
            };
            BantGrafikCanvas.Children.Add(line);
        }

        // Rx çizgisi (mavi)
        var rxPoints = new PointCollection();
        for (int i = 0; i < rx.Length; i++)
            rxPoints.Add(new System.Windows.Point(i * xStep, margin + chartH - (rx[i] / maxVal * chartH)));

        var rxLine = new Polyline
        {
            Points          = rxPoints,
            Stroke          = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        };
        BantGrafikCanvas.Children.Add(rxLine);

        // Tx çizgisi (yeşil)
        var txPoints = new PointCollection();
        for (int i = 0; i < tx.Length; i++)
            txPoints.Add(new System.Windows.Point(i * xStep, margin + chartH - (tx[i] / maxVal * chartH)));

        var txLine = new Polyline
        {
            Points          = txPoints,
            Stroke          = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        };
        BantGrafikCanvas.Children.Add(txLine);

        // Max değer etiketi
        var maxLabel = new TextBlock
        {
            Text       = BantHizFormatla((long)maxVal),
            Foreground = new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58)),
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 9,
        };
        Canvas.SetLeft(maxLabel, 4);
        Canvas.SetTop(maxLabel, margin);
        BantGrafikCanvas.Children.Add(maxLabel);

        // Legend
        var legend = new StackPanel { Orientation = Orientation.Horizontal };
        legend.Children.Add(new TextBlock { Text = "↓ Rx", Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)), FontFamily = new FontFamily("Consolas"), FontSize = 9, Margin = new Thickness(0, 0, 8, 0) });
        legend.Children.Add(new TextBlock { Text = "↑ Tx", Foreground = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)), FontFamily = new FontFamily("Consolas"), FontSize = 9 });
        Canvas.SetRight(legend, 8);
        Canvas.SetTop(legend, margin);
        BantGrafikCanvas.Children.Add(legend);
    }

    private void BantGrafikCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        => BantGrafiginiCiz();

    private static string BantHizFormatla(long bytesPerSec)
    {
        if (bytesPerSec >= 1_000_000) return $"{bytesPerSec / 1_000_000.0:0.0} MB/s";
        if (bytesPerSec >= 1_000)     return $"{bytesPerSec / 1_000.0:0.0} KB/s";
        return $"{bytesPerSec} B/s";
    }
}
