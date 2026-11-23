using System.Windows;

namespace MibExplorer.Views.Dialogs;

public partial class RebootMibConfirmWindow : Window
{
    public bool AutoReconnect => AutoReconnectCheckBox.IsChecked == true;

    public RebootMibConfirmWindow()
    {
        InitializeComponent();
    }

    private void Reboot_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
