using System.Linq;
using System.Windows;

namespace MibExplorer.Views.Dialogs;

public static class AppMessageBox
{
    private const string DefaultTitle = "MibExplorer";

    public static MessageBoxResult Show(string messageBoxText)
    => Show(null, messageBoxText, DefaultTitle, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption)
        => Show(null, messageBoxText, string.IsNullOrWhiteSpace(caption) ? DefaultTitle : caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => Show(null, messageBoxText, string.IsNullOrWhiteSpace(caption) ? DefaultTitle : caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => Show(null, messageBoxText, string.IsNullOrWhiteSpace(caption) ? DefaultTitle : caption, button, icon);

    public static MessageBoxResult Show(
    Window? owner,
    string messageBoxText,
    string caption,
    MessageBoxButton button,
    MessageBoxImage icon)
    {
        var dialog = new ThemedMessageBoxWindow(
            messageBoxText,
            string.IsNullOrWhiteSpace(caption) ? DefaultTitle : caption,
            button,
            icon);

        var resolvedOwner = ResolveOwner(owner);
        if (resolvedOwner != null && resolvedOwner != dialog)
        {
            dialog.Owner = resolvedOwner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? ResolveOwner(Window? owner)
    {
        if (owner != null)
            return owner;

        if (Application.Current == null)
            return null;

        var active = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);

        return active ?? Application.Current.MainWindow;
    }
}
