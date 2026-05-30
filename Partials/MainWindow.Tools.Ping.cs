using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    private void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabPing;
        PingIpBox.Focus();
    }

    private void PingIpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(PingIpBox);
        var metin = PingIpBox.Text.Trim();
        PingPlaceholder.Visibility = string.IsNullOrEmpty(PingIpBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(metin))
        {
            PingValidasyonIkonu.Text    = "";
            PingBaslatBtn.IsEnabled     = false;
            PingFavoriEkleBtn.IsEnabled = false;
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

    private void PingKutucugaYaz(string metin, string hex)
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
        PingResultPanel.Children.Add(satir);
        PingResultScroll.ScrollToEnd();
    }

    private async Task PingBaslat(string hedef)
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        _pingCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
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
            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("PING", hedef, ozet, logSatirlari,
                    new Dictionary<string, string>
                    {
                        ["Basarili"]   = basarili.ToString(),
                        ["Kayip"]      = (4 - basarili).ToString(),
                        ["OrtalamaMs"] = basarili > 0 ? (toplamMs / basarili).ToString() : "",
                    });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
            BildirimCal(hata: basarili == 0);
            ToastGoster(basarili > 0 ? $"Ping tamamlandı — {hedef}" : $"Ping başarısız — {hedef}", hata: basarili == 0);
        }
    }
}
