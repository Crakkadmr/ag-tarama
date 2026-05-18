using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgTarama.Services;
using AgTarama.Services.Ai;

namespace AgTarama;

public partial class AiDeviceReportWindow : Window
{
    private static readonly Regex _ipRx = new(
        @"\b(25[0-5]|2[0-4]\d|[01]?\d\d?)(\.(25[0-5]|2[0-4]\d|[01]?\d\d?)){3}\b",
        RegexOptions.Compiled);

    private readonly IReadOnlyList<CihazDto> _cihazlar;
    private readonly AppSettings             _ayarlar;
    private readonly Func<IReadOnlyList<string>, Task>? _yenidenTaraCallback;

    private string?              _sonYanit;
    private IReadOnlyList<string>? _bulunanIpler;
    private string?              _secilenPreset;

    public AiDeviceReportWindow(
        IReadOnlyList<CihazDto> cihazlar,
        AppSettings ayarlar,
        Func<IReadOnlyList<string>, Task>? yenidenTaraCallback = null)
    {
        InitializeComponent();
        _cihazlar            = cihazlar;
        _ayarlar             = ayarlar;
        _yenidenTaraCallback = yenidenTaraCallback;

        SubtitleText.Text = $"{cihazlar.Count} cihaz analiz edilecek";
        PresetChipleriniYukle();
    }

    private void PresetChipleriniYukle()
    {
        PresetChipPanel.Children.Clear();

        foreach (var p in AiDeviceAnalyzer.Presetler)
        {
            var btn = new Button
            {
                Content = $"{p.Ikon}  {p.Etiket}",
                Style   = (Style)FindResource("PresetChipStyle"),
            };
            var talep = p.Talep;
            var etiket = p.Etiket;
            btn.Click += (_, _) => AnalizeBasla(talep, etiket);
            PresetChipPanel.Children.Add(btn);
        }

        var ozelBtn = new Button
        {
            Content = "✏️  Kendi sorum",
            Style   = (Style)FindResource("PresetChipStyle"),
        };
        ozelBtn.Click += (_, _) =>
        {
            OzelSoruSiniri.Visibility = Visibility.Visible;
            OzelSoruBox.Focus();
        };
        PresetChipPanel.Children.Add(ozelBtn);
    }

    private void OzelSoruBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OzelSoruGonder();
        }
    }

    private void OzelSoruGonderBtn_Click(object sender, RoutedEventArgs e)
        => OzelSoruGonder();

    private void OzelSoruGonder()
    {
        var soru = OzelSoruBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(soru)) return;
        AnalizeBasla(soru, "Özel Soru");
    }

    private async void AnalizeBasla(string talep, string presetAdi)
    {
        _secilenPreset = presetAdi;
        PresetPanel.Visibility  = Visibility.Collapsed;
        BeklemePanel.Visibility = Visibility.Visible;
        SonucSiniri.Visibility  = Visibility.Collapsed;
        KopyalaBtn.Visibility   = Visibility.Collapsed;
        TxtKaydetBtn.Visibility = Visibility.Collapsed;
        YenidenSorBtn.Visibility = Visibility.Collapsed;
        IpYenidenTaraBtn.Visibility = Visibility.Collapsed;
        TitleText.Text = $"🤖 AI Cihaz Analizi — {presetAdi}";

        try
        {
            var yanit = await AiDeviceAnalyzer.AnalyzeAsync(
                _cihazlar, talep, _ayarlar);

            _sonYanit    = yanit;
            _bulunanIpler = IpleriBul(yanit);

            SonucBox.Text           = yanit;
            BeklemePanel.Visibility = Visibility.Collapsed;
            SonucSiniri.Visibility  = Visibility.Visible;

            KopyalaBtn.Visibility    = Visibility.Visible;
            TxtKaydetBtn.Visibility  = Visibility.Visible;
            YenidenSorBtn.Visibility = Visibility.Visible;

            if (_bulunanIpler.Count > 0 && _yenidenTaraCallback is not null)
                IpYenidenTaraBtn.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            BeklemePanel.Visibility = Visibility.Collapsed;
            SonucBox.Text           = "AI analizi iptal edildi.";
            SonucSiniri.Visibility  = Visibility.Visible;
        }
        catch (Exception ex)
        {
            BeklemePanel.Visibility = Visibility.Collapsed;
            SonucBox.Text           = $"Hata: {ex.Message}";
            SonucSiniri.Visibility  = Visibility.Visible;
            YenidenSorBtn.Visibility = Visibility.Visible;
        }
    }

    private static IReadOnlyList<string> IpleriBul(string metin)
        => _ipRx.Matches(metin)
               .Select(m => m.Value)
               .Distinct()
               .ToList();

    private void KopyalaBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_sonYanit))
            Clipboard.SetText(_sonYanit);
    }

    private void TxtKaydetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_sonYanit)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Metin Dosyası|*.txt",
            FileName = $"AI_Cihaz_{_secilenPreset}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };

        if (dlg.ShowDialog() == true)
            File.WriteAllText(dlg.FileName, _sonYanit, System.Text.Encoding.UTF8);
    }

    private void YenidenSorBtn_Click(object sender, RoutedEventArgs e)
    {
        _sonYanit     = null;
        _bulunanIpler = null;

        SonucSiniri.Visibility       = Visibility.Collapsed;
        KopyalaBtn.Visibility        = Visibility.Collapsed;
        TxtKaydetBtn.Visibility      = Visibility.Collapsed;
        YenidenSorBtn.Visibility     = Visibility.Collapsed;
        IpYenidenTaraBtn.Visibility  = Visibility.Collapsed;

        OzelSoruSiniri.Visibility = Visibility.Collapsed;
        OzelSoruBox.Clear();

        TitleText.Text = "🤖 AI Cihaz Analizi";
        PresetChipleriniYukle();
        PresetPanel.Visibility = Visibility.Visible;
    }

    private async void IpYenidenTaraBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_bulunanIpler is null || _yenidenTaraCallback is null) return;
        Close();
        await _yenidenTaraCallback(_bulunanIpler);
    }

    private void KapatBtn_Click(object sender, RoutedEventArgs e) => Close();
}
