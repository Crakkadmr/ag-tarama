using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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
    // ─── UI yardımcıları ──────────────────────────────────────────────

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
        _mesajGecmisi.Add((tur, metin, DateTime.Now.ToString("HH:mm:ss")));

        var zaman = new TextBlock
        {
            Text                = DateTime.Now.ToString("HH:mm:ss"),
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
        if (sel == TabGecmis)
            GecmisPanelGuncelle();
        if (sel == TabLisans)
            LisansPanelGuncelle();
        if (sel == TabCihazTara && string.IsNullOrEmpty(KameraSubnetBox.Text))
            KameraSubnetBox.Text = string.Join(",", YerelSubnetleriBul());
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

    // ─── Ping paneli event'leri ──────────────────────────────────────
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

    // ─── Ping işlemi ────────────────────────────────────────────────
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

    private void BtnArp_Click(object sender, RoutedEventArgs e)    => _ = ArpTablosuGoster();
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
    private void PortPanelAcAnimasyon()  { PortIpBox.Focus(); }
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

    // ─── Traceroute ───────────────────────────────────────────────────
    private void TraceHedefBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OtomatikNoktaUygula(TraceHedefBox);
        var m = TraceHedefBox.Text.Trim();
        TraceHedefPlaceholder.Visibility = string.IsNullOrEmpty(TraceHedefBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(m)) { TraceHedefValidasyon.Text = ""; TraceBaslatBtn.IsEnabled = false; return; }
        if (GecerliIpv4Mu(m))        { TraceHedefValidasyon.Text = "✓"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63,  185, 80));  TraceBaslatBtn.IsEnabled = true; }
        else if (GecerliHostnameMu(m)){ TraceHedefValidasyon.Text = "~"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34)); TraceBaslatBtn.IsEnabled = true; }
        else                          { TraceHedefValidasyon.Text = "✗"; TraceHedefValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81,  73));  TraceBaslatBtn.IsEnabled = false; }
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
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private async Task TracerouteBaslat(string hedef)
    {
        _traceCts?.Cancel();
        _traceCts?.Dispose();
        _traceCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);
        var token = _traceCts.Token;

        TraceResultPanel.Children.Clear();
        TraceResultBorder.Visibility = Visibility.Visible;
        TraceBaslatBtn.IsEnabled     = false;
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
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private async Task DnsLookupBaslat(string hedef)
    {
        DnsResultPanel.Children.Clear();
        DnsResultBorder.Visibility = Visibility.Visible;
        DnsBaslatBtn.IsEnabled     = false;
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
                    var s = $"  {(ip.AddressFamily == AddressFamily.InterNetwork ? "A   " : "AAAA")}  →  {ip}";
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
        if (_macRegex.IsMatch(m)) { WolMacValidasyon.Text = "✓"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)); WolGonderBtn.IsEnabled = true; }
        else                      { WolMacValidasyon.Text = "✗"; WolMacValidasyon.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73)); WolGonderBtn.IsEnabled = false; }
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
            Text         = metin,
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 1, 0, 1),
        });

    private void WolGonder(string mac)
    {
        WolResultPanel.Children.Clear();
        WolResultBorder.Visibility = Visibility.Visible;
        var logSatirlari = new List<string>();
        try
        {
            var temiz    = mac.Replace(":", "").Replace("-", "");
            var macBytes = Enumerable.Range(0, 6).Select(i => Convert.ToByte(temiz.Substring(i * 2, 2), 16)).ToArray();
            var paket    = new byte[102];
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

            var regex    = new Regex(@"(\d{1,3}(?:\.\d{1,3}){3})\s+([0-9a-fA-F]{2}(?:[:\-][0-9a-fA-F]{2}){5})\s+(\w+)");
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
            var metin   = sb.ToString().TrimEnd();
            MesajEkle("sonuc", metin);
            var satirlar = metin.Split('\n').Select(s => s.TrimEnd()).ToList();
            LogService.Kaydet("ARP", "arp -a", satirlar);
            if (!_gecmisdenCalistiriliyor)
            {
                HistoryService.Kaydet("ARP", "arp -a", $"ARP tablosu — {esleseler.Count} kayıt", satirlar,
                    new Dictionary<string, string> { ["KayitSayisi"] = esleseler.Count.ToString() });
                if (MainTabControl.SelectedIndex == TabGecmis) GecmisPanelGuncelle();
            }
            _gecmisdenCalistiriliyor = false;
        }
        catch (Exception ex) { MesajEkle("hata", $"ARP tablosu okunamadı: {ex.Message}"); }
    }
}
