using MibExplorer.Models.Coding;
using MibExplorer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MibExplorer.Views.Coding;

public partial class CodingCenterWindow : Window
{
    public CodingCenterWindow(CodingCenterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync();
        };
    }

    private void SlowListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject dependencyObject)
            return;

        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(dependencyObject);

        if (scrollViewer is null)
            return;

        double offset = scrollViewer.VerticalOffset - Math.Sign(e.Delta) * 5.0;

        scrollViewer.ScrollToVerticalOffset(offset);
        e.Handled = true;
    }

    private void FeatureValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        if (comboBox.DataContext is not CodingFeatureValue feature)
            return;

        if (comboBox.SelectedItem is not CodingFeatureOption selectedOption)
            return;

        bool isCurrentValue =
            string.Equals(selectedOption.Raw, feature.RawValue, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(selectedOption.Label, feature.Value, StringComparison.OrdinalIgnoreCase);

        if (!isCurrentValue)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            feature.SelectedOption = null;
            comboBox.SelectedItem = null;
            comboBox.SelectedIndex = -1;
        }, DispatcherPriority.Background);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                return typedChild;

            T? result = FindVisualChild<T>(child);

            if (result is not null)
                return result;
        }

        return null;
    }
}