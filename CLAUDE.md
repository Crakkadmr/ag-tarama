# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ÖNCELİKLE DETAY.md'Yİ OKU

Yeni bir session başladığında **önce `DETAY.md` dosyasını oku.** Bu dosya projenin tüm dosya yapısı, mimarisi, alanları, metodları, UI bileşenleri ve TODO listesini içerir — kaynak dosyaları tek tek taramaya gerek kalmaz.

## DETAY.md OTOMATİK GÜNCELLEME (ZORUNLU)

Aşağıdaki tetikleyicilerden **herhangi biri** olduğunda `DETAY.md` dosyasını **aynı turda** güncellemen gerekir (kullanıcı ayrıca istemese bile):

- `MainWindow.xaml` veya `MainWindow.xaml.cs` üzerinde Edit/Write yapıldığında
- Yeni `.cs` / `.xaml` dosyası eklendiğinde veya silindiğinde
- `AgTarama.csproj` değiştirildiğinde (TargetFramework, paket vs.)
- Yeni klasör/araç eklendiğinde (`tools/`, `Req/` altı dahil)
- Yeni buton, stil veya UI bileşeni eklendiğinde
- Yeni metot/alan/state değişkeni eklendiğinde
- TODO maddesi tamamlandığında veya yeni TODO doğduğunda

Güncelleme yaparken:
- İlgili bölümü (Klasör Yapısı / Mimari / 6.x metot haritası / TODO / Git Durumu) bul ve **yerinde** düzenle.
- Üstteki `Son güncelleme:` tarihini bugünün tarihine çevir.
- Satır numaralarını mümkün olduğunca güncel tut (ana metotların yaklaşık satır aralığı).

## Proje Amacı

WPF tabanlı bir ağ tarama (network scanner) uygulaması. Chatbot tarzı arayüz: sol taraf mesaj akışı, sağ taraf kontrol butonları. Hedef: ping sweep, port tarama, ARP ile cihaz tespiti.

## Komutlar

```bash
# Proje dizini
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"

# Derleme
dotnet build

# Çalıştırma
dotnet run

# Release build
dotnet build -c Release
```

## Mimari

Tek pencereli WPF uygulaması. `MainWindow.xaml` + `MainWindow.xaml.cs` çifti tüm UI ve iş mantığını barındırıyor — henüz ayrı ViewModel/servis katmanı yok.

**UI düzeni:**
- Sol: `ChatPanel` (ScrollViewer içinde StackPanel) — mesajlar `MesajEkle(tur, metin)` metoduyla ekleniyor
- Sağ: Kontrol butonları (220px sabit genişlik)
- Alt: `InputBox` + Gönder butonu, Enter ile de tetikleniyor

**Mesaj türleri** (`MesajEkle` metodu):
| Tür | Renk | Kullanım |
|---|---|---|
| `"sistem"` | Gri | Durum bildirimleri |
| `"kullanici"` | Koyu mavi, sağda | Kullanıcı girdisi |
| `"sonuc"` | Mavi | Tarama çıktıları |
| `"hata"` | Kırmızı | Hatalar |

**Tarama durumu:** `_taramaDevamEdiyor` bool ile izleniyor. `TaramaDurumunuAyarla(bool)` metodu butonları ve `StatusText`'i senkronize ediyor.

## Sıradaki Adımlar (TODO)

`MainWindow.xaml.cs` içinde `// TODO` yorumları ile işaretlenmiş:

1. **Taramayı Başlat** — CIDR veya IP aralığı parse edip async ping sweep
2. **Taramayı Durdur** — `CancellationToken` ile iptal mekanizması
3. **Ping Testi** — `System.Net.NetworkInformation.Ping` kullanımı
4. **Port Tara** — `TcpClient` ile belirli portları async tarama
5. **Cihazları Listele** — ARP tablosu okuma (`arp -a`) veya broadcast ping
6. **Sonuçları Kaydet** — Mesajları `.txt` veya `.csv` olarak dışa aktarma

## Geliştirme Notları

- Tüm ağ işlemleri `async/await` + `CancellationToken` ile yapılmalı; UI thread'i bloke etme.
- Tarama sonuçları `MesajEkle("sonuc", ...)` ile ChatPanel'e yazılacak.
- Yeni buton eklendikçe sağ paneldeki `StackPanel`'e `ActionButton` stiliyle ekleniyor.
- Stil kaynaklarının tamamı `MainWindow.xaml` içindeki `<Window.Resources>` bloğunda tanımlı.
- .NET 10 / WPF — `LetterSpacing` gibi web'e özgü CSS özellikleri WPF XAML'de yok, kullanma.
