using System.Windows;
using System.Windows.Media;

namespace MibExplorer.Views.Dialogs;

public partial class ThemedMessageBoxWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ThemedMessageBoxWindow(
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        InitializeComponent();

        Title = string.IsNullOrWhiteSpace(caption) ? "Message" : caption;
        CaptionText.Text = string.IsNullOrWhiteSpace(caption) ? "Message" : caption;
        MessageText.Text = message ?? string.Empty;

        ConfigureIcon(icon);
        ConfigureButtons(buttons);

        KeyDown += ThemedMessageBoxWindow_KeyDown;
    }

    private void ThemedMessageBoxWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape)
            return;

        if (CancelButton.Visibility == Visibility.Visible)
            CloseWithResult(MessageBoxResult.Cancel);
        else if (NoButton.Visibility == Visibility.Visible)
            CloseWithResult(MessageBoxResult.No);
        else if (OkButton.Visibility == Visibility.Visible)
            CloseWithResult(MessageBoxResult.OK);
        else
            CloseWithResult(MessageBoxResult.None);

        e.Handled = true;
    }
    
    private void ConfigureIcon(MessageBoxImage icon)
    {
        string glyph;
        Brush foreground;

        if (icon == MessageBoxImage.Error || icon == MessageBoxImage.Stop || icon == MessageBoxImage.Hand)
        {
            glyph = "\uE783"; // StatusErrorFull
            foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x7A));
        }
        else if (icon == MessageBoxImage.Warning || icon == MessageBoxImage.Exclamation)
        {
            glyph = "\uE7BA"; // Warning
            foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
        }
        else if (icon == MessageBoxImage.Question)
        {
            glyph = "\uE897"; // Help
            foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xB3, 0xFF));
        }
        else if (icon == MessageBoxImage.Information || icon == MessageBoxImage.Asterisk)
        {
            glyph = "\uE946"; // Info
            foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xB3, 0xFF));
        }
        else
        {
            glyph = "\uE946"; // Info fallback
            foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xB3, 0xFF));
        }

        IconText.Text = glyph;
        IconText.Foreground = foreground;
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        OkButton.Visibility = Visibility.Collapsed;
        YesButton.Visibility = Visibility.Collapsed;
        NoButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;

        OkButton.IsDefault = false;
        OkButton.IsCancel = false;
        YesButton.IsDefault = false;
        YesButton.IsCancel = false;
        NoButton.IsDefault = false;
        NoButton.IsCancel = false;
        CancelButton.IsDefault = false;
        CancelButton.IsCancel = false;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                OkButton.IsCancel = true;
                Loaded += (_, __) => OkButton.Focus();
                break;

            case MessageBoxButton.OKCancel:
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                CancelButton.IsCancel = true;
                Loaded += (_, __) => OkButton.Focus();
                break;

            case MessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                NoButton.IsCancel = true;
                Loaded += (_, __) => YesButton.Focus();
                break;

            case MessageBoxButton.YesNoCancel:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                CancelButton.IsCancel = true;
                Loaded += (_, __) => YesButton.Focus();
                break;

            default:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                OkButton.IsCancel = true;
                Loaded += (_, __) => OkButton.Focus();
                break;
        }
    }

    private void CloseWithResult(MessageBoxResult result)
    {
        Result = result;
        Close();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var title = CaptionText.Text?.Trim();
            var message = MessageText.Text ?? string.Empty;

            string textToCopy = string.IsNullOrWhiteSpace(title)
                ? message
                : $"{title}{Environment.NewLine}{Environment.NewLine}{message}";

            Clipboard.SetText(textToCopy);
        }
        catch
        {
            // On ignore volontairement les erreurs clipboard
        }
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(MessageBoxResult.OK);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(MessageBoxResult.Yes);
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(MessageBoxResult.No);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(MessageBoxResult.Cancel);
    }
}
