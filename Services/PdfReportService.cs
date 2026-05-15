using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AgTarama.Services;

public record ReportMetadata(string Operator = "-", string Project = "AgTarama Ag Taramasi");

public record DeviceScanRow(
    string Ip, string Ad, string Tur, string Marka, string Model,
    string Ping, string Portlar, string Kesif, string Mac, string Uretici, string Servis);

public static class PdfReportService
{
    static PdfReportService()
        => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly string[] Headers =
        { "IP", "Ad", "Tur", "Marka", "Model", "Ping", "Portlar", "Kesif", "MAC", "Uretici", "Servis" };

    public static byte[] GenerateDeviceScanReport(IEnumerable<DeviceScanRow> rows, ReportMetadata meta)
    {
        var list = rows.ToList();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(16);
                page.MarginVertical(12);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Height(36).Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().Text("NETWORK SNIFFER - CIHAZ TARA RAPORU").Bold().FontSize(12).FontColor("#58A6FF");
                        col.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm} | Toplam: {list.Count} cihaz | Operator: {meta.Operator}")
                           .FontSize(8).FontColor("#8B949E");
                    });
                });

                page.Content().PaddingVertical(2).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                    });

                    table.Header(h =>
                    {
                        foreach (var col in Headers)
                        {
                            h.Cell().Background("#0D3B66").Padding(4).Text(t => t.Span(col).Bold().FontColor("#E6EDF3"));
                        }
                    });

                    bool odd = true;
                    foreach (var row in list)
                    {
                        var bg = odd ? "#0D1117" : "#101722";
                        odd = !odd;

                        void C(string text, string color = "#C9D1D9")
                            => table.Cell()
                                .Background(bg)
                                .BorderBottom(1).BorderColor("#243147")
                                .Padding(3)
                                .Text(t => t.Span(text ?? "").FontColor(color));

                        C(row.Ip, "#58A6FF");
                        C(row.Ad);
                        C(row.Tur, "#3FB950");
                        C(row.Marka);
                        C(row.Model);
                        C(row.Ping);
                        C(row.Portlar);
                        C(row.Kesif);
                        C(row.Mac, "#8B949E");
                        C(row.Uretici);
                        C(row.Servis);
                    }
                });

                page.Footer().Height(16).Row(r =>
                {
                    r.RelativeItem().Text("Network Sniffer - made by demircan").FontSize(7).FontColor("#484F58");
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(7).FontColor("#484F58");
                        t.CurrentPageNumber().FontSize(8).FontColor("#8B949E");
                        t.Span(" / ").FontSize(7).FontColor("#484F58");
                        t.TotalPages().FontSize(8).FontColor("#8B949E");
                    });
                });
            });
        }).GeneratePdf();
    }

}
