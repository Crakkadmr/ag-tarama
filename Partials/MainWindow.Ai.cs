using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgTarama.Services.Ai;

namespace AgTarama;

public partial class MainWindow
{
    private async void AiGonderBtn_Click(object sender, RoutedEventArgs e)
    {
        await AiSoruGonderAsync();
    }

    private async void AiInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Keyboard.Modifiers == ModifierKeys.Shift) return;

        e.Handled = true;
        await AiSoruGonderAsync();
    }

    private void AiTemizleBtn_Click(object sender, RoutedEventArgs e)
    {
        _aiSohbetGecmisi.Clear();
        MesajEkle("sistem", "AI sohbet baglami temizlendi.");
    }

    private async Task AiSoruGonderAsync()
    {
        if (_aiSohbetCalisiyor)
        {
            ToastGoster("AI yaniti bekleniyor.", hata: true);
            return;
        }

        var soru = AiInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(soru))
            return;

        AiInputBox.Clear();
        MesajEkle("kullanici", soru);

        _aiSohbetCalisiyor = true;
        AiGonderBtn.IsEnabled = false;
        AiInputBox.IsEnabled = false;
        AiTemizleBtn.IsEnabled = false;

        _aiSohbetCts?.Dispose();
        _aiSohbetCts = CancellationTokenSource.CreateLinkedTokenSource(MasterCts.Token);

        var beklemeSatiri = BeklemeSatiriEkle();
        try
        {
            var mesajlar = new List<AiChatMessage>
            {
                new("system", AiPrompts.SohbetSystemPrompt)
            };
            mesajlar.AddRange(_aiSohbetGecmisi.TakeLast(6));
            mesajlar.Add(new AiChatMessage("user", soru));

            var yanit = await AiClient.ChatAsync(_ayarlar, mesajlar, _aiSohbetCts.Token);

            _aiSohbetGecmisi.Add(new AiChatMessage("user", soru));
            _aiSohbetGecmisi.Add(new AiChatMessage("assistant", yanit));

            MesajEkle("sonuc", "AI: " + yanit);
        }
        catch (OperationCanceledException)
        {
            MesajEkle("sistem", "AI istegi iptal edildi.");
        }
        catch (Exception ex)
        {
            HataBildir("AI sohbet hatasi", ex);
        }
        finally
        {
            ChatPanel.Children.Remove(beklemeSatiri);
            _aiSohbetCalisiyor = false;
            AiGonderBtn.IsEnabled = true;
            AiInputBox.IsEnabled = true;
            AiTemizleBtn.IsEnabled = true;
            AiInputBox.Focus();
        }
    }

    private Border BeklemeSatiriEkle()
    {
        var satir = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 3, 0, 3),
            Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = "◆ AI dusunuyor...",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
            }
        };

        ChatPanel.Children.Add(satir);
        if (ChatSondaMi())
            ChatScrollViewer.ScrollToEnd();
        return satir;
    }
}
