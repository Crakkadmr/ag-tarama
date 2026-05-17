using System.Text;
using System.Windows.Input;
using AgTarama.Services;

namespace AgTarama;

public partial class MainWindow
{
    // ─── Komut Konsolu (F12) ──────────────────────────────────────────

    private int _konsoleGecmisIndex = -1;
    private CancellationTokenSource? _konsoleCts;
    private bool _konsoleCalistiriliyor = false;

    private void KonsoleBaslat()
    {
        this.KeyDown += Window_KonsoleKeyDown;
    }

    private void Window_KonsoleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            e.Handled = true;
            KonsoleToggle();
        }
    }

    private void KonsoleToggle()
    {
        if (ConsolePanel.Visibility == System.Windows.Visibility.Collapsed)
        {
            ConsolePanel.Visibility = System.Windows.Visibility.Visible;
            ConsoleInput.Focus();
            if (string.IsNullOrEmpty(ConsoleOutput.Text))
                KonsoleYaz("Network Sniffer Komut Konsolu  •  `help` yazarak başlayın  •  F12: kapat\n");
        }
        else
        {
            ConsolePanel.Visibility = System.Windows.Visibility.Collapsed;
            _konsoleCts?.Cancel();
        }
    }

    private void ConsoleKapat_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ConsolePanel.Visibility = System.Windows.Visibility.Collapsed;
        _konsoleCts?.Cancel();
    }

    private async void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
    {
        var history = CommandRouter.History;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var line = ConsoleInput.Text.Trim();
            if (string.IsNullOrEmpty(line)) return;

            ConsoleInput.Text = "";
            _konsoleGecmisIndex = -1;

            KonsoleYaz($"\n▶ {line}\n");

            if (_konsoleCalistiriliyor)
            {
                _konsoleCts?.Cancel();
                await Task.Delay(100);
            }

            _konsoleCts?.Dispose();
            _konsoleCts = new CancellationTokenSource();
            _konsoleCalistiriliyor = true;
            try
            {
                var result = await CommandRouter.ExecuteAsync(line, _konsoleCts.Token);
                if (result == "\x00CLEAR")
                    ConsoleOutput.Clear();
                else if (!string.IsNullOrEmpty(result))
                    KonsoleYaz(result + "\n");
            }
            finally
            {
                _konsoleCalistiriliyor = false;
            }
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (history.Count == 0) return;
            _konsoleGecmisIndex = Math.Min(_konsoleGecmisIndex + 1, history.Count - 1);
            ConsoleInput.Text = history[_konsoleGecmisIndex];
            ConsoleInput.CaretIndex = ConsoleInput.Text.Length;
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            _konsoleGecmisIndex = Math.Max(_konsoleGecmisIndex - 1, -1);
            ConsoleInput.Text = _konsoleGecmisIndex < 0 ? "" : history[_konsoleGecmisIndex];
            ConsoleInput.CaretIndex = ConsoleInput.Text.Length;
        }
        else if (e.Key == Key.Tab)
        {
            e.Handled = true;
            var cur = ConsoleInput.Text;
            if (string.IsNullOrEmpty(cur)) return;
            var matches = CommandRouter.Names
                .Where(n => n.StartsWith(cur, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1)
            {
                ConsoleInput.Text = matches[0] + " ";
                ConsoleInput.CaretIndex = ConsoleInput.Text.Length;
            }
            else if (matches.Count > 1)
            {
                KonsoleYaz("  " + string.Join("  ", matches) + "\n");
            }
        }
        else if (e.Key == Key.Escape)
        {
            _konsoleCts?.Cancel();
        }
    }

    private void KonsoleYaz(string metin)
    {
        ConsoleOutput.AppendText(metin);
        ConsoleScrollViewer.ScrollToEnd();
    }
}
