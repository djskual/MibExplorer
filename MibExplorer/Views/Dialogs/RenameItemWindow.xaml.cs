using MibExplorer.Core;
using System.Windows;

namespace MibExplorer.Views.Dialogs;

public partial class RenameItemWindow : Window
{
    private sealed class RenameItemViewModel : ObservableObject
    {
        private string _newName = string.Empty;
        private string _validationMessage = string.Empty;

        public string CurrentNameText { get; init; } = string.Empty;

        public string NewName
        {
            get => _newName;
            set => SetProperty(ref _newName, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }
    }

    private readonly RenameItemViewModel _viewModel;

    public string ResultName => _viewModel.NewName.Trim();

    public RenameItemWindow(string currentName)
    {
        InitializeComponent();

        _viewModel = new RenameItemViewModel
        {
            CurrentNameText = $"Current name: {currentName}",
            NewName = currentName
        };

        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        string newName = _viewModel.NewName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newName))
        {
            _viewModel.ValidationMessage = "The new name cannot be empty.";
            return;
        }

        if (newName.Contains('/') || newName.Contains('\\'))
        {
            _viewModel.ValidationMessage = "The new name must not contain path separators.";
            return;
        }

        if (newName == "." || newName == "..")
        {
            _viewModel.ValidationMessage = "This name is not allowed.";
            return;
        }

        DialogResult = true;
    }
}
