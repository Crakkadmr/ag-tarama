using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
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
}
