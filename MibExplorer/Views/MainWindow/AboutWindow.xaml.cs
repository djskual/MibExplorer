using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.IO;

namespace MibExplorer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        VersionText.Text = $"Version: {GetBuildTag()}";
        FrameworkText.Text = $".NET: {RuntimeInformation.FrameworkDescription}";
    }

    private static string GetBuildTag()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "git-tag.txt");
            if (!File.Exists(path))
                return "unknown";

            var tag = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(tag) ? "unknown" : tag;
        }
        catch
        {
            return "unknown";
        }
    }

    private void GithubLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/djskual/MibExplorer",
            UseShellExecute = true
        });
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
