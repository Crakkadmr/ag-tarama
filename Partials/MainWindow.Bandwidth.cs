using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
}
