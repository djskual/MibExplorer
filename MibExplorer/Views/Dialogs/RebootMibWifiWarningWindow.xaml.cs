using System.Windows;

namespace MibExplorer.Views.Dialogs;

public enum RebootMibWifiWarningChoice
{
    Cancel,
    OpenLocationSettings,
    RebootWithoutReconnect
}

public partial class RebootMibWifiWarningWindow : Window
{
    public RebootMibWifiWarningChoice Choice { get; private set; } = RebootMibWifiWarningChoice.Cancel;

    public RebootMibWifiWarningWindow(string? diagnostic)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            NetworkDiagnosticText.Text = diagnostic;
            NetworkDiagnosticText.Visibility = Visibility.Visible;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        Choice = RebootMibWifiWarningChoice.OpenLocationSettings;
        DialogResult = true;
    }

    private void RebootWithoutReconnect_Click(object sender, RoutedEventArgs e)
    {
        Choice = RebootMibWifiWarningChoice.RebootWithoutReconnect;
        DialogResult = true;
    }
}
