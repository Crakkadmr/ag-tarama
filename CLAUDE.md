# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
