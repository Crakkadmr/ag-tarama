# UI Referansı — MainWindow.xaml

## UI Düzeni

- `WindowState="Maximized"` — uygulama tam ekran açılır
- **3 satırlı kök Grid:** Satır 0 (Auto) = başlık kartı; Satır 1 (Auto) = `LisansBanner` (gizli, < 7 gün kaldığında görünür); Satır 2 (`*`) = iç Grid
- **İç Grid (Grid.Row=2):** Satır 0 (`*`) = `MainTabControl`; Satır 1 (Auto) = `ConsolePanel` (F12 toggle)
- **Toast:** `Grid.RowSpan="3"` — tüm satırları kapsar
- **Başlık kartı** (`#161B22`, CornerRadius=12): sol — ikon + `NETWORK SNIFFER` + `StatusText`; orta — araç WrapPanel; sağ — versiyon yazısı
- **TabControl** (`x:Name="MainTabControl"`, custom ControlTemplate — TabPanel ScrollViewer ile sarılmış)
- **ConsolePanel** (`x:Name="ConsolePanel"`, MinHeight=180, Visibility=Collapsed): `ConsoleOutput` (TextBox, IsReadOnly) + `ConsoleInput` (TextBox). F12 ile toggle. **ControlTemplate dışında** — code-behind'dan doğrudan erişilir.
- **LisansBanner** (`x:Name="LisansBanner"`, Grid.Row=1, Visibility=Collapsed): uyarı rengi (`#1F1400`/`#D29A22`), `LisansBannerMetin` + [Yenile] butonu + kapatma butonu

### Sekmeler

| # | Başlık | Panel x:Name | CTS | İçerik |
|---|---|---|---|---|
| 0 | 💬 Chatbot | `ChatPanel` + `FavoriChipleri` | — | ChatScrollViewer; header butonları Chatbot'u kontrol eder |
| 1 | ◎ Cihaz Tara | `KameraPanel` | `_kameraCts` | Subnet giriş, Tara/Durdur, filtreler, DataGrid |
| 2 | ◈ Ping Testi | `PingPanel` | `_pingCts` | IP giriş, chip'ler, PingResultPanel |
| 3 | ⊞ Port Tara | `PortPanel` | `_portScanCts` | IP + port aralığı, chip'ler, PortResultPanel |
| 4 | ⇢ Traceroute | `TracePanel` | `_traceCts` | IP giriş, TraceResultPanel |
| 5 | ⊕ DNS Lookup | `DnsPanel` | — | Hostname giriş, DnsResultPanel |
| 6 | ⏻ Wake-on-LAN | `WolPanel` | — | MAC giriş, magic packet gönder |
| 7 | ★ Favoriler | `FavorilerPanel` | — | Favori IP listesi + sil chip'leri |
| 8 | ▶ Bant Genişliği | `BantPanel` | — | 5/15/60dk seçici, Canvas grafiği (Rx mavi / Tx yeşil), Peak/Avg/Toplam stat kartları, adaptör listesi, per-app trafik (admin gerekli) |
| 9 | ◷ Geçmiş | `GecmisPanel` | — | JSON geçmiş kayıtları, aç/tekrar çalıştır/karşılaştır |
| 10 | 📶 Wi-Fi | `WlanPanel` | `_wlanCts` | Tara/Durdur, otomatik yenile (10s), DataGrid (SSID/BSSID/Sinyal/Kanal/Kimlik/Şifreleme/Radyo/Durum), Evil-Twin göstergesi; Wi-Fi adaptörü yoksa `WlanTab.IsEnabled=false` |
| 11 | ⊙ Lisans | `LisansPanel` | — | Lisans durumu, kalan süre (renk kodlu), son online doğrulama UTC, NTP zamanı, MachineId (8 char), sticky banner (< 7 gün), kopyala butonu |

**TabItem stili:** Consolas 12pt, `#8B949E` fg, transparent. Seçilince alt kenarlık `#2F81F7` (2px), bg `#0D1F2F`, metin `#58A6FF`. Hover: bg `#161B22`, metin `#C9D1D9`. CornerRadius=6,6,0,0.

Sekme geçişi: `MainTabControl.SelectedIndex = TabXxx` (sabitler `MainWindow.xaml.cs`'de).

---

## Araç Butonları (başlık WrapPanel — soldan sağa)

| Adı | x:Name | Click Handler | Açıklama |
|---|---|---|---|
| Taramayı Başlat | `BtnTaramaBaslat` | `BtnTaramaBaslat_Click` | tshark yakalama başlatır |
| Taramayı Durdur | `BtnTaramaDurdur` | `BtnTaramaDurdur_Click` | CancellationToken iptal |
| ARP Tablosu | `BtnArp` | `BtnArp_Click` | `arp -a` → chat kart + OUI |
| Ağ Bilgisi | `BtnAgBilgi` | `BtnAgBilgi_Click` | `NetworkInterface` → chat kartı |
| Advanced IP Scanner | `BtnCihazlar` | `BtnCihazlar_Click` | Harici exe başlatır |
| SADP | `BtnSadp` | `BtnSadp_Click` | `tools/sadp/sadptool.exe` |
| Rapor Kaydet | `BtnRapor` | `BtnRapor_Click` | Chat → .txt dosyası |
| Ayarlar | `BtnAyarlar` | `BtnAyarlar_Click` | SettingsWindow açar |
| Ekranı Temizle | `BtnTemizle` | `BtnTemizle_Click` | Tarama sırasında disabled |

**Buton `ⓘ` badge:** Her buton `Grid` içerik kullanır — sol `StackPanel` (ikon + etiket), sağ-üst `TextBlock` (`ⓘ`, `#58A6FF88`). `HorizontalContentAlignment="Stretch"` zorunlu.

---

## Stil Sistemi (Window.Resources)

| Kaynak Anahtar | Tip | Kullanım |
|---|---|---|
| `ActionButton` | Button | Standart başlık butonu (koyu mavi, 44px yükseklik) |
| `ActiveActionButton` | Button | Aktif — yeşil çerçeve (#3FB950, 2px). **`ActionButton`'dan SONRA tanımlanmalı** |
| `PrimaryButton` | Button | Yeşil "Başlat" butonu (48px, BasedOn ActionButton) |
| `DangerButton` | Button | Kırmızı "Durdur" butonu (BasedOn ActionButton) |
| `PingInputBox` | TextBox | IP/değer giriş kutusu |
| `ChipButton` | Button | Hızlı seçim chip'leri (CornerRadius=12) |
| `DarkComboBox` | ComboBox | Cihaz Tara tür filtresi; koyu açılır liste |
| `DarkComboBoxItem` | ComboBoxItem | Koyu dropdown satırları |
| `DarkDataGrid` | DataGrid | Cihaz Tara tablo; koyu tema, sıralanabilir |
| `DarkDataGridColumnHeader` | DataGridColumnHeader | Koyu sütun başlıkları |
| `DarkDataGridCell` | DataGridCell | Koyu hücre template'i |
| `DarkDataGridRow` | DataGridRow | Koyu satır/hover/seçili |
| `FlatContextMenu` | ContextMenu | Cihaz Tara sağ tık menüsü; koyu tema |
| `FlatContextMenuItem` | MenuItem | Sağ tık menü satırları; tek sütunlu |
| `ToolTip` (default) | ToolTip | `#1C2128` bg, `#C9D1D9` fg, `#3D444D` border, CornerRadius=5, MaxWidth=280 |
| (default) | ScrollBar | 6px ince ScrollBar |

---

## Renk Paleti (GitHub Dark)

```
Background:  #0D1117 (ana), #161B22 (yüzey), #21262D (ayırıcı)
Border:      #30363D (varsayılan), #21262D (silik)
Mavi:        #58A6FF (vurgu), #1F6FEB (seçim), #0D3B66 (basılı)
Yeşil:       #3FB950 (başarı), #1A4A2E (PrimaryButton bg), #238636
Kırmızı:     #F85149 (hata), #3D1A1A (DangerButton bg), #8B1A1A
Metin:       #E6EDF3 (parlak), #C9D1D9 (orta), #8B949E (silik), #484F58 (devre dışı)
```
