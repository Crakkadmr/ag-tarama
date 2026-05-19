namespace AgTarama.Services.Discovery.Models;

internal sealed record ScanProgress(
    int Taranan,
    int Toplam,
    int BulunanCihaz,
    string AsamaMetni,
    int PaketSayisi = 0);
