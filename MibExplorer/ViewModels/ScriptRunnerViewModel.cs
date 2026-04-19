using MibExplorer.Core;
using MibExplorer.Models.Scripting;
using MibExplorer.Services;
using MibExplorer.Services.Scripting;
using MibExplorer.Settings;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows;

namespace MibExplorer.ViewModels;

public sealed class ScriptRunnerViewModel : ObservableObject
{
    private readonly IMibConnectionService _connectionService;
    private readonly IScriptCatalogService _catalogService;
    private readonly IScriptExecutionService _executionService;

    public ObservableCollection<ScriptItem> Scripts { get; } = new();

    private ScriptItem? _selectedScript;
    public ScriptItem? SelectedScript
    {
        get => _selectedScript;
        set
        {
            if (SetProperty(ref _selectedScript, value))
            {
                RefreshCommandStates();
            }
        }
    }

    private string _outputText = string.Empty;
    public string OutputText
    {
        get => _outputText;
        set
        {
            if (SetProperty(ref _outputText, value))
            {
                RefreshCommandStates();
            }
        }
    }

    private string _statusText = "Idle";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsConnectedToMib => _connectionService.IsConnected;

    public string ConnectionStateText => IsConnectedToMib ? "Connected" : "Disconnected";

    public System.Windows.Visibility LoadingIndicatorVisibility =>
        IsBusy ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(LoadingIndicatorVisibility));
                RefreshCommandStates();
            }
        }
    }

    private string _scriptsFolderPath;
    public string ScriptsFolderPath
    {
        get => _scriptsFolderPath;
        set
        {
            if (SetProperty(ref _scriptsFolderPath, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public ICommand RefreshScriptsCommand { get; }
    public ICommand RunScriptCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand CopyLogCommand { get; }
    public ICommand OpenScriptsFolderCommand { get; }
    public ICommand OpenInEditorCommand { get; }

    public ScriptRunnerViewModel(
        IMibConnectionService connectionService,
        IScriptCatalogService catalogService,
        IScriptExecutionService executionService)
    {
        _connectionService = connectionService;
        _catalogService = catalogService;
        _executionService = executionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        _scriptsFolderPath = _catalogService.ScriptsFolderPath;

        RefreshScriptsCommand = new RelayCommand(RefreshScripts);
        RunScriptCommand = new RelayCommand(
            async () => await RunScriptAsync(),
            () => CanRunScript());
        ClearLogCommand = new RelayCommand(
            () => OutputText = string.Empty,
            () => !string.IsNullOrWhiteSpace(OutputText));
        CopyLogCommand = new RelayCommand(
            CopyLogToClipboard,
            () => !string.IsNullOrWhiteSpace(OutputText));
        OpenScriptsFolderCommand = new RelayCommand(OpenScriptsFolder);
        OpenInEditorCommand = new RelayCommand(
            () => { },
            () => SelectedScript is not null);

        RefreshScripts();
        RefreshCommandStates();
    }

    private void RefreshScripts()
    {
        ScriptsFolderPath = _catalogService.ScriptsFolderPath; 
        
        Scripts.Clear();

        var items = _catalogService.GetScripts();

        foreach (var item in items)
        {
            Scripts.Add(item);
        }

        StatusText = $"{Scripts.Count} script(s) loaded";
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        OnPropertyChanged(nameof(IsConnectedToMib));
        OnPropertyChanged(nameof(ConnectionStateText));
        RefreshCommandStates();
    }

    public void SetScriptsFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var settings = AppSettingsStore.Current.Clone();
        settings.ScriptsFolderPath = path;
        settings.Normalize();
        AppSettingsStore.Save(settings);

        ScriptsFolderPath = _catalogService.ScriptsFolderPath;
        RefreshScripts();
    }

    private void OpenScriptsFolder()
    {
        var folderPath = ScriptsFolderPath;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }

    private bool CanRunScript()
    {
        return !IsBusy
               && SelectedScript != null
               && _connectionService.IsConnected;
    }

    private async Task RunScriptAsync()
    {
        if (SelectedScript == null)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Running {SelectedScript.Name}...";
        OutputText = string.Empty;

        try
        {
            var result = await _executionService.ExecuteAsync(
                SelectedScript,
                onOutput: AppendOutput);

            if (result.Success)
            {
                StatusText = "Completed successfully";
            }
            else
            {
                StatusText = $"Failed (code {result.ExitCode})";

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    AppendOutput(result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error";
            AppendOutput(ex.Message);
        }
        finally
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                IsBusy = false;
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => IsBusy = false);
            }
        }
    }

    private void AppendOutput(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        void Append()
        {
            OutputText += line + Environment.NewLine;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            Append();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(Append);
        }
    }

    private void CopyLogToClipboard()
    {
        if (string.IsNullOrWhiteSpace(OutputText))
            return;

        Clipboard.SetText(OutputText);
    }

    private void RefreshCommandStates()
    {
        if (RefreshScriptsCommand is RelayCommand refreshCommand)
        {
            refreshCommand.RaiseCanExecuteChanged();
        }

        if (RunScriptCommand is RelayCommand runCommand)
        {
            runCommand.RaiseCanExecuteChanged();
        }

        if (ClearLogCommand is RelayCommand clearCommand)
        {
            clearCommand.RaiseCanExecuteChanged();
        }

        if (CopyLogCommand is RelayCommand copyLogCommand)
        {
            copyLogCommand.RaiseCanExecuteChanged();
        }

        if (OpenScriptsFolderCommand is RelayCommand openFolderCommand)
        {
            openFolderCommand.RaiseCanExecuteChanged();
        }

        if (OpenInEditorCommand is RelayCommand openInEditorCommand)
        {
            openInEditorCommand.RaiseCanExecuteChanged();
        }
    }
}