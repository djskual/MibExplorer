using MibExplorer.Core;
using MibExplorer.Services;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace MibExplorer.ViewModels;

public sealed class FileEditorViewModel : ObservableObject
{
    private readonly IMibConnectionService _connectionService;
    private readonly RelayCommand _saveCommand;

    private string _editorText = string.Empty;
    private string _originalText = string.Empty;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _hasLoaded;
    private bool _isUpdatingDocumentState;

    private string _statusMessage = "Loading remote file...";

    private Encoding _documentEncoding = new UTF8Encoding(false);
    private string _lineEnding = "\n";

    public FileEditorViewModel(
        IMibConnectionService connectionService,
        string remotePath,
        bool isReadOnly)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        RemotePath = remotePath;
        IsReadOnly = isReadOnly;

        BaseTitle = isReadOnly
            ? $"Remote File Editor (Read-only) - {remotePath}"
            : $"Remote File Editor - {remotePath}";

        _saveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        SaveCommand = _saveCommand;
    }

    public string RemotePath { get; }

    private readonly string BaseTitle;

    public string WindowTitle => $"{BaseTitle}{(IsDirty ? " *" : string.Empty)}";

    public bool IsReadOnly { get; }

    public string ModeLabel => IsReadOnly ? "Read-only" : "Editable";

    public ICommand SaveCommand { get; }

    public string EditorText
    {
        get => _editorText;
        set
        {
            if (SetProperty(ref _editorText, value))
            {
                if (!_isUpdatingDocumentState)
                {
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(CanShowDiff));
                    RefreshSaveCommand();
                }
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsEditorEnabled));
                OnPropertyChanged(nameof(LoadingIndicatorVisibility));
                OnPropertyChanged(nameof(CanReload));
                RefreshSaveCommand();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsEditorEnabled));
                OnPropertyChanged(nameof(LoadingIndicatorVisibility));
                OnPropertyChanged(nameof(CanReload));
                RefreshSaveCommand();
            }
        }
    }

    public bool IsBusy => IsLoading || IsSaving;

    public Visibility LoadingIndicatorVisibility =>
        IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public bool IsEditorEnabled => !IsBusy;

    public bool IsDirty => !string.Equals(EditorText, _originalText, StringComparison.Ordinal);

    public bool CanSave => !IsBusy && _hasLoaded && !IsReadOnly && IsDirty;

    public bool CanReload => !IsBusy;

    public bool CanShowDiff => _hasLoaded && IsDirty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string OriginalText => _originalText;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadCoreAsync(forceReload: false, cancellationToken);
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        return LoadCoreAsync(forceReload: true, cancellationToken);
    }

    private async Task LoadCoreAsync(bool forceReload, CancellationToken cancellationToken)
    {
        if (_hasLoaded && !forceReload)
            return;

        bool isReload = forceReload && _hasLoaded;
        string tempFilePath = Path.GetTempFileName();

        string previousEditorText = EditorText;
        string previousOriginalText = _originalText;
        Encoding previousEncoding = _documentEncoding;
        string previousLineEnding = _lineEnding;

        try
        {
            IsLoading = true;
            StatusMessage = isReload
                ? "Reloading remote file..."
                : "Loading remote file...";

            await _connectionService.DownloadFileAsync(
                RemotePath,
                tempFilePath,
                cancellationToken: cancellationToken);

            byte[] bytes = await File.ReadAllBytesAsync(tempFilePath, cancellationToken);

            (_documentEncoding, int preambleLength) = DetectEncoding(bytes);

            string loadedText = _documentEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
            _lineEnding = DetectLineEnding(loadedText);

            _isUpdatingDocumentState = true;
            EditorText = loadedText;
            _originalText = loadedText;
            _isUpdatingDocumentState = false;

            _hasLoaded = true;

            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(OriginalText));
            OnPropertyChanged(nameof(CanShowDiff));

            if (isReload)
            {
                StatusMessage = IsReadOnly
                    ? "Remote file reloaded. Read-only mode."
                    : "Remote file reloaded.";
            }
            else
            {
                StatusMessage = IsReadOnly
                    ? "Remote file loaded. Read-only mode."
                    : "Remote file loaded.";
            }
        }
        catch (Exception ex)
        {
            if (!isReload)
            {
                _isUpdatingDocumentState = true;
                EditorText = string.Empty;
                _originalText = string.Empty;
                _isUpdatingDocumentState = false;

                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(OriginalText));
                OnPropertyChanged(nameof(CanShowDiff));
            }
            else
            {
                _isUpdatingDocumentState = true;
                EditorText = previousEditorText;
                _originalText = previousOriginalText;
                _isUpdatingDocumentState = false;

                _documentEncoding = previousEncoding;
                _lineEnding = previousLineEnding;

                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(OriginalText));
                OnPropertyChanged(nameof(CanShowDiff));
            }

            StatusMessage = isReload
                ? $"Reload failed: {ex.Message}"
                : $"Load failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RefreshSaveCommand();

            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch
            {
            }
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
            return;

        string tempFilePath = Path.GetTempFileName();

        try
        {
            IsSaving = true;
            StatusMessage = "Saving remote file...";

            string normalizedText = NormalizeLineEndings(EditorText, _lineEnding);
            byte[] bytes = EncodeText(normalizedText, _documentEncoding);

            await File.WriteAllBytesAsync(tempFilePath, bytes, cancellationToken);

            await _connectionService.ReplaceFileAsync(
                tempFilePath,
                RemotePath,
                cancellationToken: cancellationToken);

            _isUpdatingDocumentState = true;
            EditorText = normalizedText;
            _originalText = normalizedText;
            _isUpdatingDocumentState = false;

            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(OriginalText));
            OnPropertyChanged(nameof(CanShowDiff));
            StatusMessage = "Remote file saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            RefreshSaveCommand();

            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch
            {
            }
        }
    }

    private void RefreshSaveCommand()
    {
        _saveCommand.RaiseCanExecuteChanged();
    }

    private static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), 3);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, 2);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFE &&
            bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        return (new UTF8Encoding(false), 0);
    }

    private static string DetectLineEnding(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
            return "\r\n";

        if (text.Contains('\n'))
            return "\n";

        return "\n";
    }

    private static string NormalizeLineEndings(string text, string targetLineEnding)
    {
        string normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        return targetLineEnding == "\r\n"
            ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal)
            : normalized;
    }

    private static byte[] EncodeText(string text, Encoding encoding)
    {
        byte[] contentBytes = encoding.GetBytes(text);
        byte[] preamble = encoding.GetPreamble();

        if (preamble.Length == 0)
            return contentBytes;

        byte[] output = new byte[preamble.Length + contentBytes.Length];
        Buffer.BlockCopy(preamble, 0, output, 0, preamble.Length);
        Buffer.BlockCopy(contentBytes, 0, output, preamble.Length, contentBytes.Length);
        return output;
    }
}