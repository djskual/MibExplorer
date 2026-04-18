using MibExplorer.Settings;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace MibExplorer.Views.Dialogs;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _workingCopy;

    public AppSettings ResultSettings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        _workingCopy = settings.Clone();
        ResultSettings = _workingCopy.Clone();

        RememberWindowPlacementCheck.IsChecked = _workingCopy.RememberWindowSizeAndPosition;
        LastHostTextBox.Text = _workingCopy.LastHost ?? string.Empty;
        LastPortTextBox.Text = _workingCopy.LastPort ?? string.Empty;
        LastUsernameTextBox.Text = _workingCopy.LastUsername ?? string.Empty;
        UsePrivateKeyCheckBox.IsChecked = _workingCopy.UsePrivateKey;
        PrivateKeyPathTextBox.Text = _workingCopy.LastPrivateKeyPath ?? string.Empty;
        ScriptsFolderPathTextBox.Text = _workingCopy.ScriptsFolderPath ?? string.Empty;
        AutoCheckUpdatesCheck.IsChecked = _workingCopy.AutoCheckUpdatesOnStartup;
        IncludePrereleaseCheck.IsChecked = _workingCopy.IncludePrereleaseVersionsInUpdateCheck;

        Loaded += (_, __) => ShowSection(0);
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ShowSection(SectionList.SelectedIndex);
    }

    private void ShowSection(int index)
    {
        GeneralPanel.Visibility = Visibility.Collapsed;
        ConnectionPanel.Visibility = Visibility.Collapsed;
        ScriptsPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        switch (index)
        {
            case 1:
                ConnectionPanel.Visibility = Visibility.Visible;
                break;
            case 2:
                ScriptsPanel.Visibility = Visibility.Visible;
                break;
            case 3:
                UpdatesPanel.Visibility = Visibility.Visible;
                break;
            default:
                GeneralPanel.Visibility = Visibility.Visible;
                break;
        }

        SectionHost.Opacity = 0.35;
        var fade = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(220)
        };
        SectionHost.BeginAnimation(OpacityProperty, fade);
    }

    private void BrowsePrivateKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SSH private key",
            Filter = "Private key files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        var currentPath = PrivateKeyPathTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }

                var fileName = Path.GetFileName(currentPath);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    dialog.FileName = fileName;
                }
            }
            catch
            {
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            PrivateKeyPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseScriptsFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select scripts folder",
            ShowNewFolderButton = true
        };

        var currentPath = ScriptsFolderPathTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
        {
            dialog.SelectedPath = currentPath;
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ScriptsFolderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _workingCopy.RememberWindowSizeAndPosition = RememberWindowPlacementCheck.IsChecked == true;
        _workingCopy.LastHost = LastHostTextBox.Text;
        _workingCopy.LastPort = LastPortTextBox.Text;
        _workingCopy.LastUsername = LastUsernameTextBox.Text;
        _workingCopy.UsePrivateKey = UsePrivateKeyCheckBox.IsChecked == true;
        _workingCopy.LastPrivateKeyPath = PrivateKeyPathTextBox.Text;
        _workingCopy.ScriptsFolderPath = ScriptsFolderPathTextBox.Text;
        _workingCopy.AutoCheckUpdatesOnStartup = AutoCheckUpdatesCheck.IsChecked == true;
        _workingCopy.IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseCheck.IsChecked == true;

        _workingCopy.Normalize();
        ResultSettings = _workingCopy.Clone();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}