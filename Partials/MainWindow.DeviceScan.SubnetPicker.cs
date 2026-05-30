using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AgTarama;

public partial class MainWindow
{
    private sealed record NicSubneti(string Prefix, string NicAdi, string Tip, long Hiz);

    private bool _subnetBoxChipSenkronu;

    private static List<string> YerelSubnetleriBul()
    {
        var sonuc = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))
                    sonuc.Add($"{b[0]}.{b[1]}.{b[2]}");
            }
        }
        return sonuc.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static List<NicSubneti> YerelNicSubnetleriniBul()
    {
        var sonuc = new Dictionary<string, NicSubneti>(StringComparer.Ordinal);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) continue;
            if (SanalAdaptorMu(ni)) continue;

            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = uni.Address.GetAddressBytes();
                if (!(b[0] == 192 || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))) continue;
                var prefix = $"{b[0]}.{b[1]}.{b[2]}";
                if (sonuc.ContainsKey(prefix)) continue;
                var tip = ni.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                    NetworkInterfaceType.Ethernet      => "Ethernet",
                    NetworkInterfaceType.GigabitEthernet => "Ethernet",
                    _ => ni.NetworkInterfaceType.ToString(),
                };
                sonuc[prefix] = new NicSubneti(prefix, ni.Name, tip, ni.Speed);
            }
        }
        return sonuc.Values.OrderBy(x => x.Prefix, StringComparer.Ordinal).ToList();
    }

    private static string? YerelSubnetiBul()
        => YerelSubnetleriBul().FirstOrDefault();

    private static bool SanalAdaptorMu(NetworkInterface ni)
    {
        var ad = $"{ni.Name} {ni.Description}".ToLowerInvariant();
        return ad.Contains("virtual")
            || ad.Contains("vmware")
            || ad.Contains("hyper-v")
            || ad.Contains("vbox")
            || ad.Contains("vpn")
            || ad.Contains("wireguard")
            || ad.Contains("loopback")
            || ad.Contains("tunnel")
            || ad.Contains("tap")
            || ad.Contains("miniport");
    }

    private void BtnKamera_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = TabCihazTara;
        if (KameraSubnetChips.Children.Count == 0)
            KameraNicChipleriniYenile(seciliVarsayilan: true);
    }

    private void KameraNicYenileBtn_Click(object sender, RoutedEventArgs e)
        => KameraNicChipleriniYenile(seciliVarsayilan: false);

    public void KameraNicChipleriniYenile(bool seciliVarsayilan)
    {
        var mevcutSecili = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.Ordinal);

        KameraSubnetChips.Children.Clear();

        var nicler = YerelNicSubnetleriniBul();
        if (nicler.Count == 0)
        {
            var bos = new TextBlock
            {
                Text = "Aktif ağ arayüzü bulunamadı",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(2, 6, 0, 0),
            };
            KameraSubnetChips.Children.Add(bos);
            return;
        }

        for (int i = 0; i < nicler.Count; i++)
        {
            var nic = nicler[i];
            var chip = KameraChipOlustur(nic);
            bool secili = mevcutSecili.Contains(nic.Prefix) ||
                          (mevcutSecili.Count == 0 && seciliVarsayilan && i == 0);
            chip.IsChecked = secili;
            KameraSubnetChips.Children.Add(chip);
        }

        KameraChipleriSenkronizeEt();
    }

    private System.Windows.Controls.Primitives.ToggleButton KameraChipOlustur(NicSubneti nic)
    {
        var icerik = new StackPanel { Orientation = Orientation.Horizontal };
        icerik.Children.Add(new TextBlock
        {
            Text = $"{nic.Prefix}.0/24",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        icerik.Children.Add(new TextBlock
        {
            Text = $"  ({nic.Tip})",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var chip = new System.Windows.Controls.Primitives.ToggleButton
        {
            Tag = nic.Prefix,
            Content = icerik,
            Style = (Style)FindResource("DarkChip"),
            ToolTip = $"{nic.NicAdi}\nTür: {nic.Tip}" +
                      (nic.Hiz > 0 ? $"\nHız: {nic.Hiz / 1_000_000} Mbps" : ""),
        };
        chip.Checked += KameraChipDegisti;
        chip.Unchecked += KameraChipDegisti;
        return chip;
    }

    private void KameraChipDegisti(object sender, RoutedEventArgs e)
        => KameraChipleriSenkronizeEt();

    private void KameraChipleriSenkronizeEt()
    {
        var prefixler = KameraSubnetChips.Children
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true)
            .Select(t => t.Tag as string ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        if (prefixler.Count == 0) return;
        _subnetBoxChipSenkronu = true;
        try { KameraSubnetBox.Text = string.Join(",", prefixler); }
        finally { _subnetBoxChipSenkronu = false; }
    }

    private void KameraSubnetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        KameraSubnetPlaceholder.Visibility = string.IsNullOrEmpty(KameraSubnetBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (_subnetBoxChipSenkronu) return;

        var metin = KameraSubnetBox.Text ?? "";
        foreach (var tb in KameraSubnetChips.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>())
        {
            if (tb.IsChecked == true && tb.Tag is string p && !metin.Contains(p, StringComparison.Ordinal))
                tb.IsChecked = false;
        }
    }
}
