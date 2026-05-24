using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AgTarama.Services;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

public partial class MainWindow
{
    private const double HaritaDugumBoyut = 46;
    private IReadOnlyList<MapNode> _haritaDugumler = Array.Empty<MapNode>();
    private DeviceInfo? _haritaSecili;

    private void HaritaYenileBtn_Click(object sender, RoutedEventArgs e) => HaritaCiz();

    private void HaritaCiz()
    {
        var cihazlar = _engine.Store.All;

        double w = HaritaCanvas.ActualWidth  > 10 ? HaritaCanvas.ActualWidth  : 1000;
        double h = HaritaCanvas.ActualHeight > 10 ? HaritaCanvas.ActualHeight : 650;

        _haritaDugumler = NetworkMapLayout.Hesapla(
            cihazlar,
            d => { var k = KimlikBelirle(d); return (k.Tur, k.TurIkon); },
            w, h);

        HaritaCanvas.Children.Clear();
        HaritaDetayPanel.Visibility = Visibility.Collapsed;
        _haritaSecili = null;

        if (_haritaDugumler.Count == 0)
        {
            HaritaBosMesaj.Visibility = Visibility.Visible;
            return;
        }
        HaritaBosMesaj.Visibility = Visibility.Collapsed;

        var gw = _haritaDugumler.FirstOrDefault(n => n.IsGateway);
        double cx = gw?.X ?? w / 2.0;
        double cy = gw?.Y ?? h / 2.0;

        foreach (var dugum in _haritaDugumler.Where(n => !n.IsGateway))
        {
            var line = new Line
            {
                X1 = cx, Y1 = cy, X2 = dugum.X, Y2 = dugum.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                StrokeThickness = 1,
                Opacity = dugum.Online ? 0.55 : 0.25,
            };
            HaritaCanvas.Children.Add(line);
        }

        foreach (var dugum in _haritaDugumler)
            HaritaCanvas.Children.Add(HaritaDugumOlustur(dugum));
    }

    private FrameworkElement HaritaDugumOlustur(MapNode dugum)
    {
        var renk = dugum.Online
            ? Color.FromRgb(0x3F, 0xB9, 0x50)
            : Color.FromRgb(0x6E, 0x76, 0x81);

        var ikon = new TextBlock
        {
            Text = dugum.Ikon,
            FontFamily = new FontFamily("Consolas"),
            FontSize = dugum.IsGateway ? 22 : 18,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var etiket = new TextBlock
        {
            Text = CihazAdiSec(dugum.Device) ?? dugum.Device.Ip,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 96,
        };

        var icerik = new StackPanel();
        icerik.Children.Add(ikon);
        icerik.Children.Add(etiket);

        var kutu = new Border
        {
            Background = new SolidColorBrush(dugum.IsGateway
                ? Color.FromRgb(0x0D, 0x3B, 0x66)
                : Color.FromRgb(0x1E, 0x29, 0x3B)),
            BorderBrush = new SolidColorBrush(renk),
            BorderThickness = new Thickness(dugum.IsGateway ? 3 : 2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 4),
            Opacity = dugum.Online ? 1.0 : 0.5,
            Cursor = Cursors.Hand,
            Child = icerik,
            Tag = dugum.Device,
        };
        kutu.MouseLeftButtonUp += HaritaDugum_Click;

        kutu.Loaded += (_, _) =>
        {
            Canvas.SetLeft(kutu, dugum.X - kutu.ActualWidth / 2.0);
            Canvas.SetTop(kutu, dugum.Y - kutu.ActualHeight / 2.0);
        };
        Canvas.SetLeft(kutu, dugum.X - HaritaDugumBoyut / 2.0);
        Canvas.SetTop(kutu, dugum.Y - HaritaDugumBoyut / 2.0);

        return kutu;
    }

    private void HaritaDugum_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DeviceInfo dev)
        {
            e.Handled = true;
            HaritaDetayGoster(dev);
        }
    }

    private void HaritaDetayGoster(DeviceInfo dev)
    {
        _haritaSecili = dev;
        var kimlik = KimlikBelirle(dev);

        HaritaDetayBaslik.Text = $"{kimlik.TurIkon}  {kimlik.Marka} · {kimlik.Tur}";
        HaritaDetayIcerik.Children.Clear();

        void Satir(string etiket, string? deger)
        {
            if (string.IsNullOrWhiteSpace(deger)) return;
            var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = etiket,
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            });
            sp.Children.Add(new TextBlock
            {
                Text = deger,
                FontFamily = new FontFamily("Consolas"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9)),
                TextWrapping = TextWrapping.Wrap,
            });
            HaritaDetayIcerik.Children.Add(sp);
        }

        Satir("IP", dev.Ip);
        Satir("Ad", CihazAdiSec(dev));
        Satir("Model", kimlik.Model);
        Satir("MAC", dev.MacAdresi);
        Satir("Üretici", dev.Uretici);
        Satir("Durum", dev.Online ? "● Online" : "○ Offline");
        Satir("Ping", dev.PingYanit ? $"{dev.PingMs} ms (TTL {dev.PingTtl})" : null);

        List<int> portlar;
        lock (dev.AcikPortlar) portlar = dev.AcikPortlar.OrderBy(p => p).ToList();
        Satir("Açık Portlar", portlar.Count > 0 ? string.Join(", ", portlar) : null);

        Satir("Keşif", dev.KesifKaynaklari.Count > 0 ? string.Join(", ", dev.KesifKaynaklari) : null);
        Satir("SNMP", dev.SnmpSysDescr);
        Satir("HTTP", dev.HttpFpMarka is null ? null : $"{dev.HttpFpMarka} {dev.HttpFpModel}".Trim());
        Satir("mDNS", string.IsNullOrEmpty(dev.MdnsTur) ? null : dev.MdnsTur);
        Satir("ONVIF", dev.OnvifBulundu ? (dev.OnvifAdi ?? "Bulundu") : null);
        Satir("SSDP", dev.SsdpFriendlyName);

        HaritaDetayAiBtn.IsEnabled = _ayarlar.AiEnabled;
        HaritaDetayPanel.Visibility = Visibility.Visible;
    }
    private void HaritaZoomSifirlaBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPngBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaPdfBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaDetayKapat_Click(object sender, RoutedEventArgs e)
        => HaritaDetayPanel.Visibility = Visibility.Collapsed;
    private void HaritaDetayAiBtn_Click(object sender, RoutedEventArgs e) { }
    private void HaritaCanvas_MouseWheel(object sender, MouseWheelEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
    private void HaritaCanvas_MouseMove(object sender, MouseEventArgs e) { }
    private void HaritaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
}
