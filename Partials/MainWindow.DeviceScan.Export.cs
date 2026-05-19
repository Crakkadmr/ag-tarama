using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ClosedXML.Excel;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    private enum KameraExportFormat { Excel, Pdf, Txt, Csv, Json }

    private void KameraExportExcel_Click(object sender, RoutedEventArgs e) => KameraDisariAktar(KameraExportFormat.Excel);
    private void KameraExportPdf_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Pdf);
    private void KameraExportTxt_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Txt);
    private void KameraExportCsv_Click(object sender, RoutedEventArgs e)   => KameraDisariAktar(KameraExportFormat.Csv);
    private void KameraExportJson_Click(object sender, RoutedEventArgs e)  => KameraDisariAktar(KameraExportFormat.Json);

    private void KameraDisariAktar(KameraExportFormat format)
    {
        var satirlar = KameraGorunenSatirlariAl();
        if (satirlar.Count == 0)
        {
            ToastGoster("Dışa aktarılacak cihaz yok", hata: true);
            return;
        }

        var (filter, ext) = format switch
        {
            KameraExportFormat.Excel => ("Excel Dosyası (*.xlsx)|*.xlsx", "xlsx"),
            KameraExportFormat.Pdf   => ("PDF Raporu (*.pdf)|*.pdf", "pdf"),
            KameraExportFormat.Txt   => ("Metin Raporu (*.txt)|*.txt", "txt"),
            KameraExportFormat.Csv   => ("CSV Dosyası (*.csv)|*.csv", "csv"),
            KameraExportFormat.Json  => ("JSON Dosyası (*.json)|*.json", "json"),
            _                        => ("Rapor (*.*)|*.*", "txt"),
        };

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "Cihaz Tara Sonuçlarını Dışa Aktar",
            Filter      = filter,
            DefaultExt  = ext,
            AddExtension = true,
            FileName    = $"Cihaz_Tara_Raporu_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            switch (format)
            {
                case KameraExportFormat.Excel:
                    File.WriteAllBytes(dlg.FileName, KameraExcelXlsxOlustur(satirlar));
                    break;
                case KameraExportFormat.Pdf:
                    File.WriteAllBytes(dlg.FileName, KameraPdfQuestOlustur(satirlar));
                    break;
                case KameraExportFormat.Txt:
                    File.WriteAllText(dlg.FileName, KameraTxtOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Csv:
                    File.WriteAllText(dlg.FileName, KameraCsvOlustur(satirlar), new UTF8Encoding(true));
                    break;
                case KameraExportFormat.Json:
                    File.WriteAllText(dlg.FileName, KameraJsonOlustur(satirlar), new UTF8Encoding(true));
                    break;
            }

            MesajEkle("sonuc", $"✔ Cihaz Tara raporu kaydedildi: {Path.GetFileName(dlg.FileName)}");
            ToastGoster($"Dışa aktarıldı: {Path.GetFileName(dlg.FileName)}");
            DisariAktarilanDosyayiAc(dlg.FileName);
        }
        catch (Exception ex)
        {
            HataBildir("Cihaz Tara dışa aktarma hatası", ex);
        }
    }

    private void DisariAktarilanDosyayiAc(string dosyaYolu)
    {
        try { Process.Start(new ProcessStartInfo(dosyaYolu) { UseShellExecute = true }); }
        catch (Exception ex) { HataBildir("Rapor kaydedildi ancak dosya otomatik açılamadı", ex); }
    }

    private List<KameraSatir> KameraGorunenSatirlariAl()
        => (_kameraSatirView?.Cast<object>().OfType<KameraSatir>().ToList() ?? _kameraSatirlari.ToList())
           .OrderBy(s => IpSiralamaAnahtari(s.Ip))
           .ThenBy(s => s.Ip, StringComparer.Ordinal)
           .ToList();

    private static long IpSiralamaAnahtari(string ip)
    {
        var parcalar = ip.Split('.');
        if (parcalar.Length != 4) return long.MaxValue;
        long sonuc = 0;
        foreach (var parca in parcalar)
        {
            if (!byte.TryParse(parca, out var b)) return long.MaxValue;
            sonuc = (sonuc << 8) + b;
        }
        return sonuc;
    }

    private static IEnumerable<string[]> KameraExportSatirlari(IEnumerable<KameraSatir> satirlar)
    {
        foreach (var s in satirlar)
            yield return new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
    }

    private static readonly string[] KameraExportBasliklari =
        { "IP", "Ad", "Tur", "Marka", "Model", "OS", "Durum", "Ping", "Portlar", "Kesif", "MAC", "Uretici", "Servis" };

    private static string KameraCsvOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", KameraExportBasliklari.Select(CsvHucre)));
        foreach (var row in KameraExportSatirlari(satirlar))
            sb.AppendLine(string.Join(";", row.Select(CsvHucre)));
        return sb.ToString();
    }

    private static string KameraJsonOlustur(List<KameraSatir> satirlar)
    {
        var veri = new
        {
            Uygulama = "AgTarama",
            Tur = "Cihaz Tara",
            OlusturmaTarihi = DateTimeOffset.Now,
            Toplam = satirlar.Count,
            Cihazlar = satirlar.Select(s => new
            {
                s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.SonGorulen,
                s.Ping, s.PingMs, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis, s.WebUrl,
            }).ToList(),
        };
        return System.Text.Json.JsonSerializer.Serialize(veri, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string CsvHucre(string? metin)
    {
        metin ??= "";
        metin = metin.Replace("\r", " ").Replace("\n", " ");
        return $"\"{metin.Replace("\"", "\"\"")}\"";
    }

    private static string KameraTxtOlustur(List<KameraSatir> satirlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NETWORK SNIFFER - CIHAZ TARA RAPORU");
        sb.AppendLine($"Tarih : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Cihaz : {satirlar.Count}");
        sb.AppendLine(new string('=', 110));
        foreach (var s in satirlar)
        {
            sb.AppendLine($"{s.Ip,-15}  {MetniKirp(s.Tur, 14),-14}  {MetniKirp(s.Marka, 16),-16}  {MetniKirp(s.Model, 34)}");
            sb.AppendLine($"  Ad      : {s.Ad}");
            sb.AppendLine($"  OS      : {s.Os}  Durum: {s.Durum}  Son Görülme: {s.SonGorulen}");
            sb.AppendLine($"  Ping    : {s.Ping}");
            sb.AppendLine($"  Portlar : {s.Portlar}");
            sb.AppendLine($"  Keşif   : {s.Kesif}");
            sb.AppendLine($"  MAC     : {s.Mac}  {s.Uretici}");
            sb.AppendLine($"  Servis  : {s.Servis}");
            sb.AppendLine(new string('-', 110));
        }
        return sb.ToString();
    }

    private static byte[] KameraExcelXlsxOlustur(List<KameraSatir> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cihaz Tara");

        for (int i = 0; i < KameraExportBasliklari.Length; i++)
            ws.Cell(1, i + 1).Value = KameraExportBasliklari[i];

        var headerRange = ws.Range(1, 1, 1, KameraExportBasliklari.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D3B66");
        headerRange.Style.Font.Bold            = true;
        headerRange.Style.Font.FontColor       = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        int row = 2;
        foreach (var s in satirlar)
        {
            var vals = new[] { s.Ip, s.Ad, s.Tur, s.Marka, s.Model, s.Os, s.Durum, s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis };
            for (int i = 0; i < vals.Length; i++)
                ws.Cell(row, i + 1).Value = vals[i];
            if (row % 2 == 0)
                ws.Range(row, 1, row, KameraExportBasliklari.Length)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#101722");
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] KameraPdfQuestOlustur(List<KameraSatir> satirlar)
    {
        var rows = satirlar.Select(s => new DeviceScanRow(
            s.Ip, s.Ad, s.Tur, s.Marka, s.Model,
            s.Ping, s.Portlar, s.Kesif, s.Mac, s.Uretici, s.Servis)).ToList();
        return PdfReportService.GenerateDeviceScanReport(rows, new ReportMetadata());
    }

    private static string MetniKirp(string? metin, int uzunluk)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        metin = Regex.Replace(metin.Trim(), @"\s+", " ");
        return metin.Length <= uzunluk ? metin : metin[..Math.Max(0, uzunluk - 1)] + "…";
    }
}
