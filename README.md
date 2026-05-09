# Ağ Tarama Programı

WPF tabanlı, chatbot arayüzlü ağ paket yakalama uygulaması. Aktif ağ arayüzlerini otomatik tespit eder, seçilen arayüzler üzerinde tshark ile paket yakalama gerçekleştirir ve sonuçları Wireshark Portable ile analiz etmeye hazır hale getirir.

---

## Özellikler

- **Otomatik Npcap kurulumu** — program ilk açılışta `Req/npcap-1.88.exe` dosyasını sessiz olarak kurar
- **Aktif arayüz tespiti** — tüm ağ arayüzleri 2 saniyelik paralel testle taranır; trafik olmayan arayüzler elenir
- **Seçimli dinleme** — aktif arayüzler chatbot'ta listelenir, kullanıcı hangilerini dinleyeceğini seçer
- **Dosya boyutu sınırı** — yakalama 16 MB dolunca otomatik durur
- **Canlı görsel kart** — ilerleme çubuğu, paket sayısı ve geçen süre gerçek zamanlı güncellenir
- **Wireshark Portable entegrasyonu** — yakalama tamamlandığında pcap dosyası doğrudan Wireshark'ta açılabilir
- **Chatbot arayüzü** — tüm işlemler renkli mesaj akışıyla izlenir

---

## Gereksinimler

| Bileşen | Notlar |
|---|---|
| Windows 10 / 11 (64-bit) | |
| .NET 10 Runtime | WPF desteği zorunlu |
| Npcap | `Req/npcap-1.88.exe` üzerinden otomatik kurulur |
| Wireshark Portable 64 | `tools/WiresharkPortable64/` altında bulunmalı |

> **Not:** Npcap sürücü kurulumu için yönetici (UAC) izni istenir. İzin verilmezse paket yakalama çalışmaz.

---

## Klasör Yapısı

```
AG TARAMA PROGRAMI/
└── AgTarama/
    ├── AgTarama.csproj
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml / MainWindow.xaml.cs
    ├── CLAUDE.md
    ├── README.md
    ├── Req/
    │   └── npcap-1.88.exe          ← Npcap kurulum dosyası
    ├── tools/
    │   └── WiresharkPortable64/
    │       ├── WiresharkPortable64.exe
    │       └── App/
    │           └── Wireshark/
    │               └── tshark.exe  ← Paket yakalama motoru
    └── captures/                   ← Yakalama dosyaları buraya kaydedilir (otomatik oluşur)
        └── analiz_GGAAYYYY_SS_DD.pcap
```

---

## Derleme ve Çalıştırma

```bash
# Proje dizinine geç
cd "C:\Projects\AG TARAMA PROGRAMI\AgTarama"

# Debug derle ve çalıştır
dotnet run

# Release derle
dotnet build -c Release
```

---

## Kullanım

1. Programı **yönetici olarak** çalıştırın (Npcap kurulumu için)
2. Npcap ilk seferinde otomatik kurulur — UAC onayı verin
3. Sağ panelden **Taramayı Başlat**'a tıklayın
4. Program 2 saniye tüm arayüzleri test eder, trafiği olanları listeler
5. Chatbot'ta çıkan arayüz butonlarına tıklayarak dinlemek istediklerinizi seçin (mavi = seçili)
6. **Dinlemeyi Başlat** butonu en az bir arayüz seçilince aktif olur — tıklayın
7. Yakalama 16 MB dolana kadar devam eder; ilerleme karttan izlenir
8. Tamamlandığında **Sonuçları Kaydet** butonu ile `captures/` klasöründeki pcap dosyasını istediğiniz yere kopyalayabilirsiniz

### Sağ Panel Butonları

| Buton | İşlev |
|---|---|
| Taramayı Başlat | Arayüz tespiti başlatır, seçim ekranı açar |
| Taramayı Durdur | Devam eden yakalamayı anında sonlandırır |
| Ping Testi | *(Yakında)* |
| Port Tara | *(Yakında)* |
| Cihazları Listele | *(Yakında)* |
| Ekranı Temizle | Tüm chatbot mesajlarını siler |

---

## Yakalama Dosyaları

Dosyalar `captures/` klasörüne aşağıdaki formatta kaydedilir:

```
analiz_GGAAYYYY_SS_DD.pcap
```

Örnek: `analiz_10052026_14_35.pcap`

Dosyalar `.pcap` formatındadır; Wireshark, tcpdump ve benzeri araçlarla açılabilir.

---

## Teknik Detaylar

- **Dil / Çerçeve:** C# 13, .NET 10, WPF
- **Paket yakalama:** tshark (Wireshark Portable'dan)
- **Yakalama limiti:** 16 MB (`-a filesize:16384`)
- **Arayüz testi:** 2 saniyelik paralel tshark taraması
- **Paket sayacı:** tshark `-P` flag ile stdout'a basılan satırlar sayılır
- **UI:** Tek pencere, `MainWindow.xaml` + `MainWindow.xaml.cs`; MVVM katmanı yok

---

## Bilinen Kısıtlamalar

- Npcap sürücüsü yüklü değilse yakalama başlamaz
- Bazı sistemlerde Npcap kurulduktan sonra yeniden başlatma gerekebilir
- `tools/WiresharkPortable64/` klasörü projeyle birlikte manuel olarak sağlanmalıdır
