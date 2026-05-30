using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Chat mesaj sistemi ───
    // Mesaj türleri: "sistem" | "kullanici" | "sonuc" | "hata"
    public void MesajEkle(string tur, string metin)
    {
        var sondaMiydi = ChatSondaMi();

        var satir = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding      = new Thickness(12, 8, 12, 8),
            Margin       = new Thickness(0, 3, 0, 3),
        };

        var txt = new TextBlock
        {
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 13,
            TextWrapping = TextWrapping.Wrap,
        };

        switch (tur)
        {
            case "kullanici":
                satir.Background          = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                satir.BorderBrush         = new SolidColorBrush(Color.FromRgb(48, 54, 61));
                satir.BorderThickness     = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Right;
                satir.MaxWidth            = 500;
                txt.Foreground            = new SolidColorBrush(Color.FromRgb(201, 209, 217));
                txt.Text                  = "› " + metin;
                break;

            case "sonuc":
                satir.Background          = new SolidColorBrush(Color.FromRgb(13, 59, 102));
                satir.BorderBrush         = new SolidColorBrush(Color.FromRgb(31, 111, 235));
                satir.BorderThickness     = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground            = new SolidColorBrush(Color.FromRgb(88, 166, 255));
                break;

            case "hata":
                satir.Background          = new SolidColorBrush(Color.FromRgb(61, 26, 26));
                satir.BorderBrush         = new SolidColorBrush(Color.FromRgb(139, 26, 26));
                satir.BorderThickness     = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground            = new SolidColorBrush(Color.FromRgb(248, 81, 73));
                txt.Text                  = "✖ " + metin;
                break;

            default: // sistem
                satir.Background          = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                satir.BorderBrush         = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                satir.BorderThickness     = new Thickness(1);
                satir.HorizontalAlignment = HorizontalAlignment.Stretch;
                txt.Foreground            = new SolidColorBrush(Color.FromRgb(139, 148, 158));
                txt.Text                  = "◆ " + metin;
                break;
        }

        satir.Child = txt;
        ChatPanel.Children.Add(satir);
        _mesajGecmisi.Add((tur, metin, System.DateTime.Now.ToString("HH:mm:ss")));

        var zaman = new TextBlock
        {
            Text                = System.DateTime.Now.ToString("HH:mm:ss"),
            FontFamily          = new FontFamily("Consolas"),
            FontSize            = 10,
            Foreground          = new SolidColorBrush(Color.FromRgb(72, 79, 88)),
            HorizontalAlignment = tur == "kullanici"
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left,
            Margin = new Thickness(4, 0, 4, 4),
        };
        ChatPanel.Children.Add(zaman);
        if (sondaMiydi)
            Dispatcher.InvokeAsync(
                () => ChatScrollViewer.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private bool ChatSondaMi()
    {
        // Kullanici yukariya ciktiysa otomatik olarak alta ziplatma.
        return ChatScrollViewer.ScrollableHeight - ChatScrollViewer.VerticalOffset < 16;
    }

    private void TaramaDurumunuAyarla(bool devamEdiyor)
    {
        _taramaDevamEdiyor        = devamEdiyor;
        BtnTaramaBaslat.IsEnabled = !devamEdiyor;
        BtnTaramaDurdur.IsEnabled =  devamEdiyor;
        BtnTemizle.IsEnabled      = !devamEdiyor;
        StatusText.Text           = devamEdiyor ? "● Yakalanıyor..." : "● Hazır";
        StatusText.Foreground     = devamEdiyor
            ? new SolidColorBrush(Color.FromRgb(210, 153, 34))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
    }

    // ─── Sağ panel buton olayları ───
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
        if (sel == TabGecmis)
            GecmisPanelGuncelle();
        if (sel == TabLisans)
            LisansPanelGuncelle();
        if (sel == TabCihazTara && string.IsNullOrEmpty(KameraSubnetBox.Text))
            KameraSubnetBox.Text = string.Join(",", YerelSubnetleriBul());
    }

    // ─── Ortak IP/hostname/otomatik nokta yardımcıları (Ping/Port/Trace/Dns hepsi kullanır) ───
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
        if (yeniUzunluk <= eskiUzunluk) return;
        if (kutu.CaretIndex != yeniUzunluk) return;
        var parcalar = kutu.Text.Split('.');
        if (parcalar.Length >= 4) return;
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
}
