using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AgTarama.Services;
using AgTarama.Services.Ai;
using AgTarama.Services.Discovery.Models;

namespace AgTarama;

public partial class MainWindow
{
    private const double HaritaDugumBoyut = 46;
    private IReadOnlyList<MapNode> _haritaDugumler = Array.Empty<MapNode>();
    private DeviceInfo? _haritaSecili;
    private bool _haritaPanAktif;
    private Point _haritaPanBaslangic;
    private double _haritaPanX0, _haritaPanY0;

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
    private void HaritaZoomSifirlaBtn_Click(object sender, RoutedEventArgs e)
    {
        HaritaScale.ScaleX = 1; HaritaScale.ScaleY = 1;
        HaritaPan.X = 0; HaritaPan.Y = 0;
    }

    private void HaritaCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double faktor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        double yeni = Math.Clamp(HaritaScale.ScaleX * faktor, 0.3, 3.0);
        HaritaScale.ScaleX = yeni;
        HaritaScale.ScaleY = yeni;
        e.Handled = true;
    }

    private void HaritaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _haritaPanAktif = true;
        _haritaPanBaslangic = e.GetPosition(this);
        _haritaPanX0 = HaritaPan.X;
        _haritaPanY0 = HaritaPan.Y;
        HaritaCanvas.CaptureMouse();
    }

    private void HaritaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_haritaPanAktif) return;
        var p = e.GetPosition(this);
        HaritaPan.X = _haritaPanX0 + (p.X - _haritaPanBaslangic.X);
        HaritaPan.Y = _haritaPanY0 + (p.Y - _haritaPanBaslangic.Y);
    }

    private void HaritaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _haritaPanAktif = false;
        HaritaCanvas.ReleaseMouseCapture();
    }

    private RenderTargetBitmap? HaritaBitmapUret()
    {
        if (_haritaDugumler.Count == 0) return null;

        double w = HaritaCanvas.ActualWidth  > 10 ? HaritaCanvas.ActualWidth  : 1000;
        double h = HaritaCanvas.ActualHeight > 10 ? HaritaCanvas.ActualHeight : 650;

        var eskiTransform = HaritaCanvas.RenderTransform;
        HaritaCanvas.RenderTransform = Transform.Identity;
        HaritaCanvas.UpdateLayout();

        var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0B, 0x12, 0x20)),
                null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(HaritaCanvas) { Stretch = Stretch.None },
                null, new Rect(0, 0, w, h));
        }
        bmp.Render(dv);

        HaritaCanvas.RenderTransform = eskiTransform;
        HaritaCanvas.UpdateLayout();
        return bmp;
    }

    private byte[]? HaritaPngBytes()
    {
        var bmp = HaritaBitmapUret();
        if (bmp == null) return null;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private void HaritaPngBtn_Click(object sender, RoutedEventArgs e)
    {
        var png = HaritaPngBytes();
        if (png == null) { ToastGoster("Önce haritayı çizin (Tara / Yenile).", hata: true); return; }

        var dlg = new SaveFileDialog
        {
            Filter = "PNG görüntü (*.png)|*.png",
            FileName = $"ag-haritasi-{DateTime.Now:yyyyMMdd-HHmm}.png",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllBytes(dlg.FileName, png);
        ToastGoster($"Harita kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)}");
    }
    private void HaritaPdfBtn_Click(object sender, RoutedEventArgs e)
    {
        var png = HaritaPngBytes();
        if (png == null) { ToastGoster("Önce haritayı çizin (Tara / Yenile).", hata: true); return; }

        var dlg = new SaveFileDialog
        {
            Filter = "PDF rapor (*.pdf)|*.pdf",
            FileName = $"ag-haritasi-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var pdf = PdfReportService.GenerateMapReport(png, new ReportMetadata());
            File.WriteAllBytes(dlg.FileName, pdf);
            ToastGoster($"PDF kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            ToastGoster($"PDF hatası: {ex.Message}", hata: true);
        }
    }
    private void HaritaDetayKapat_Click(object sender, RoutedEventArgs e)
        => HaritaDetayPanel.Visibility = Visibility.Collapsed;
    private void HaritaDetayAiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_haritaSecili is not { } dev) return;
        if (!_ayarlar.AiEnabled)
        {
            ToastGoster("AI özellikleri Ayarlar > AI bölümünden kapalı.", hata: true);
            return;
        }

        var kimlik = KimlikBelirle(dev);
        List<int> portlar;
        lock (dev.AcikPortlar) portlar = dev.AcikPortlar.OrderBy(p => p).ToList();
        string servis;
        lock (dev.ServisDetaylari)
            servis = string.Join("; ", dev.ServisDetaylari.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        var dto = new CihazDto(
            dev.Ip,
            CihazAdiSec(dev) ?? "",
            kimlik.Tur,
            kimlik.Marka,
            kimlik.Model ?? "",
            dev.PingYanit ? $"{dev.PingMs} ms" : "-",
            string.Join(", ", portlar),
            string.Join(", ", dev.KesifKaynaklari),
            dev.MacAdresi ?? "",
            dev.Uretici ?? "",
            servis,
            GuvenSkoru(dev, kimlik));

        var win = new AiDeviceReportWindow(new[] { dto }, _ayarlar)
        {
            Owner = this,
        };
        win.Show();
    }
}
