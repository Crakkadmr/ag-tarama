using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services;
using AgTarama.Services.Ai;

namespace AgTarama;

public partial class MainWindow
{
    private void BtnPortTara_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabPort;
        PortIpBox.Focus();
    }

    private void PortPanelAcAnimasyon()  { PortIpBox.Focus(); }
    private void PortPanelKapatAnimasyon() { _portScanCts?.Cancel(); }

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
        _portScanCts?.Dispose();
        _portScanCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = _portScanCts.Token;

        PortResultPanel.Children.Clear();
        PortResultBorder.Visibility = Visibility.Visible;
        PortBaslatBtn.IsEnabled     = false;
        PortKutucugaYaz($"◆ {hedef} → {portlar.Length} port taranıyor...", "#8B949E");

        var acikPortlar = new System.Collections.Concurrent.ConcurrentBag<(int Port, string Satir)>();

        async Task PortAcikCallback(int port)
        {
            var servis = BilindikPortlar.TryGetValue(port, out var s) ? $"  ({s})" : "";
            var banner = await PortBannerOku(hedef, port, token);
            var detay  = string.IsNullOrWhiteSpace(banner) ? "" : $" — {banner}";
            var satir  = $"[AÇIK]  {port}{servis}{detay}";
            acikPortlar.Add((port, satir));
            await Dispatcher.InvokeAsync(() => PortKutucugaYaz(satir, "#3FB950"));
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
            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("PORT TARA", hedef, ozet, logSatirlari,
                    new Dictionary<string, string>
                    {
                        ["TarananPortlar"] = string.Join(",", portlar),
                        ["AcikPortlar"]    = string.Join(",", acikPortlar.OrderBy(x => x.Port).Select(x => x.Port)),
                        ["AcikSayisi"]     = acik.ToString(),
                    });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
            BildirimCal(hata: acik == 0);
            ToastGoster(acik > 0 ? $"{acik} açık port bulundu — {hedef}" : $"Açık port yok — {hedef}", hata: acik == 0);

            // AI Yorumla butonu — yalnızca açık port varsa göster
            if (acik > 0 && _ayarlar.AiEnabled)
            {
                var portListesi = string.Join(", ", acikPortlar.OrderBy(x => x.Port).Select(x => x.Port));
                var aiBtn = new Button
                {
                    Content         = "✨  AI ile yorumla",
                    FontFamily      = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize        = 11,
                    Padding         = new Thickness(12, 6, 12, 6),
                    Margin          = new Thickness(0, 8, 0, 0),
                    Background      = new SolidColorBrush(Color.FromRgb(13, 27, 42)),
                    Foreground      = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(31, 111, 235)),
                    BorderThickness = new Thickness(1),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                UygulaButonSablon(aiBtn);
                aiBtn.Click += async (_, _) =>
                {
                    aiBtn.IsEnabled = false;
                    aiBtn.Content   = "⏳  AI düşünüyor...";
                    try
                    {
                        var prompt =
                            $"{hedef} adresinde şu portlar açık: {portListesi}.\n" +
                            "Her port için: servis adı, tipik kullanım, risk seviyesi (düşük/orta/yüksek), " +
                            "kapatma veya sertleştirme önerisi. Türkçe, kısa, maddeler halinde.";
                        var yanit = await AiClient.AskAsync(
                            _ayarlar, AiPrompts.SohbetSystemPrompt, prompt, MasterCts.Token);
                        PortKutucugaYaz("─────────────────────────", "#30363D");
                        PortKutucugaYaz("🤖 AI Port Yorumu:", "#58A6FF");
                        foreach (var satir in yanit.Split('\n'))
                            if (!string.IsNullOrWhiteSpace(satir))
                                PortKutucugaYaz(satir.Trim(), "#C9D1D9");
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        PortKutucugaYaz($"AI hatası: {ex.Message}", "#F85149");
                    }
                    finally
                    {
                        aiBtn.IsEnabled = false; // tek seferlik
                        aiBtn.Content   = "✨  Yorumlandı";
                    }
                };
                PortResultPanel.Children.Add(aiBtn);
            }
        }

        PortBaslatBtn.IsEnabled = true;
    }
}
