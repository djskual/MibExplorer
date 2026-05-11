using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using MibExplorer.Models.Adaptations;
using MibExplorer.ViewModels;

namespace MibExplorer.Views.Adaptations;

public partial class AdaptationsCenterWindow : Window
{
    private const double FineTreeScrollPixelsPerDetent = 24.0;

    public AdaptationsCenterWindow()
    {
        InitializeComponent();
    }

    private async void GroupHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (element.DataContext is not AdaptationGroupView group)
            return;

        if (DataContext is not AdaptationsCenterViewModel viewModel)
            return;

        await viewModel.ToggleGroupAsync(group);
        e.Handled = true;
    }

    private void AdaptationsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not AdaptationsCenterViewModel viewModel)
            return;

        viewModel.SelectedAdaptation = e.NewValue as AdaptationItemView;
    }

    private void AdaptationsTree_ItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem treeViewItem)
            return;

        if (treeViewItem.DataContext is not AdaptationGroupView)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            treeViewItem.BringIntoView();
            treeViewItem.Focus();
        }), DispatcherPriority.Background);
    }

    private void AdaptationsTree_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyFineVerticalScroll(AdaptationsTree, e);
    }

    private static void ApplyFineVerticalScroll(DependencyObject source, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null)
            return;

        double deltaSteps = e.Delta / 120.0;
        double targetOffset = scrollViewer.VerticalOffset - (deltaSteps * FineTreeScrollPixelsPerDetent);

        if (targetOffset < 0)
            targetOffset = 0;
        else if (targetOffset > scrollViewer.ScrollableHeight)
            targetOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
            return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                return typedChild;

            T? descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private void AdaptationValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        if (comboBox.DataContext is not AdaptationItemView adaptation)
            return;

        if (comboBox.SelectedItem is not string selectedValue)
            return;

        if (!string.Equals(selectedValue, adaptation.CurrentValue, StringComparison.OrdinalIgnoreCase))
            return;

        Dispatcher.BeginInvoke(() =>
        {
            adaptation.EditValue = null;
            comboBox.SelectedItem = null;
            comboBox.SelectedIndex = -1;
        }, DispatcherPriority.Background);
    }

    private void NumericBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        e.Handled = !IsValidNumericPreview(textBox, e.Text);
    }

    private void NumericBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        string text = (string)e.DataObject.GetData(typeof(string))!;
        if (!IsValidNumericPreview(textBox, text))
            e.CancelCommand();
    }

    private static bool IsValidNumericPreview(TextBox textBox, string input)
    {
        if (textBox.DataContext is not AdaptationItemView adaptation)
            return true;

        string current = textBox.Text ?? string.Empty;
        int start = textBox.SelectionStart;
        int length = textBox.SelectionLength;

        string candidate = current.Remove(start, length).Insert(start, input);

        if (string.IsNullOrWhiteSpace(candidate))
            return true;

        NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (adaptation.IsIntegerNumeric && (candidate.Contains('.') || candidate.Contains(',')))
            return false;

        candidate = candidate.Replace(',', '.');

        if (!double.TryParse(candidate, style, CultureInfo.InvariantCulture, out double value))
            return false;

        if (adaptation.MinValue is double min && value < min)
            return false;

        if (adaptation.MaxValue is double max && value > max)
            return false;

        return true;
    }
}