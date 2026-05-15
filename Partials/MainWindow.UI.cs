using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── 4.3 Ayarlar Paneli ──────────────────────────────────────────

    private void BtnAyarlar_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_ayarlar) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _ayarlar = win.Ayarlar;
            if (WlanOtoYenileCheck is not null)
            {
                WlanOtoYenileCheck.Content = $"Otomatik yenile ({Math.Clamp(_ayarlar.WlanAutoRefreshSeconds, 5, 300)}s)";
                if (WlanOtoYenileCheck.IsChecked == true) WlanOtoTimerBaslat();
            }
        }
    }

    // ─── 4.4 HTML Rapor Çıktısı ──────────────────────────────────────

    private void BtnRapor_Click(object sender, RoutedEventArgs e) => RaporKaydet();

    private void RaporKaydet()
    {
        if (_mesajGecmisi.Count == 0) { MesajEkle("sistem", "Henüz kaydedilecek mesaj yok."); return; }

        var dlg = new SaveFileDialog
        {
            Title    = "Raporu Kaydet",
            Filter   = "HTML Rapor (*.html)|*.html|Metin Dosyası (*.txt)|*.txt",
            FileName = $"AgTarama_Rapor_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        if (dlg.FilterIndex == 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AG TARAMA PROGRAMI — Rapor");
            sb.AppendLine($"Oluşturulma: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('─', 60));
            foreach (var (tur, metin, zaman) in _mesajGecmisi)
                sb.AppendLine($"[{zaman}] [{tur.ToUpper(),-8}] {metin}");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }
        else
        {
            static string Enc(string s) => s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Ağ Tarama Raporu</title><style>");
            sb.AppendLine("body{background:#0D1117;color:#E6EDF3;font-family:Consolas,monospace;padding:24px;margin:0}");
            sb.AppendLine("h1{color:#58A6FF;margin-bottom:4px}.meta{color:#484F58;font-size:11px;margin-bottom:20px}");
            sb.AppendLine(".msg{margin:3px 0;padding:8px 12px;border-radius:6px;border:1px solid;white-space:pre-wrap;word-break:break-all}");
            sb.AppendLine(".sistem{background:#161B22;border-color:#21262D;color:#8B949E}");
            sb.AppendLine(".sonuc{background:#0D3B66;border-color:#1F6FEB;color:#58A6FF}");
            sb.AppendLine(".hata{background:#3D1A1A;border-color:#8B1A1A;color:#F85149}");
            sb.AppendLine(".kullanici{background:#161B22;border-color:#30363D;color:#C9D1D9;text-align:right}");
            sb.AppendLine(".zaman{font-size:10px;color:#484F58;margin:0 4px 6px}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>AG TARAMA PROGRAMI</h1>");
            sb.AppendLine($"<div class=\"meta\">Rapor tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &nbsp;|&nbsp; {_mesajGecmisi.Count} mesaj</div>");
            foreach (var (tur, metin, zaman) in _mesajGecmisi)
            {
                sb.AppendLine($"<div class=\"msg {tur}\">{Enc(metin)}</div>");
                sb.AppendLine($"<div class=\"zaman\">{zaman}</div>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        MesajEkle("sonuc", $"✔ Rapor kaydedildi: {Path.GetFileName(dlg.FileName)}");
        ToastGoster($"Rapor kaydedildi: {Path.GetFileName(dlg.FileName)}");
    }

    // ─── 5.3 Sürükle-Bırak pcap Açma ────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var dosyalar = (string[])e.Data.GetData(DataFormats.FileDrop);
            e.Effects = dosyalar.Any(f =>
                f.EndsWith(".pcap",   StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
                ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var dosyalar = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pcap = dosyalar.FirstOrDefault(f =>
            f.EndsWith(".pcap",   StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase));
        if (pcap == null) return;
        MesajEkle("sistem", $"pcap sürüklendi → Wireshark'ta açılıyor: {Path.GetFileName(pcap)}");
        WiresharkIleAc(pcap);
    }

    // ─── 5.4 Bildirim Sesleri / Toast ────────────────────────────────

    private void ToastGoster(string mesaj, bool hata = false)
    {
        if (!_ayarlar.ToastAcik) return;
        ToastMetin.Text       = mesaj;
        ToastIkon.Text        = hata ? "✖" : "✔";
        ToastIkon.Foreground  = hata
            ? new SolidColorBrush(Color.FromRgb(248, 81, 73))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
        ToastBildirim.BorderBrush = hata
            ? new SolidColorBrush(Color.FromRgb(248, 81, 73))
            : new SolidColorBrush(Color.FromRgb(63, 185, 80));
        ToastBildirim.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (s, _) => { ToastBildirim.Visibility = Visibility.Collapsed; ((System.Windows.Threading.DispatcherTimer)s!).Stop(); _toastTimer = null; };
        _toastTimer = t;
        t.Start();
    }

    private void BildirimCal(bool hata = false)
    {
        if (!_ayarlar.SesAcik) return;
        try
        {
            if (hata) System.Media.SystemSounds.Hand.Play();
            else      System.Media.SystemSounds.Asterisk.Play();
        }
        catch { }
    }
}
