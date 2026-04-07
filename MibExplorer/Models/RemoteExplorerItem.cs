using System.Collections.ObjectModel;
using MibExplorer.Core;

namespace MibExplorer.Models;

public sealed class RemoteExplorerItem : ObservableObject
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private RemoteEntryType _entryType;
    private long _size;
    private DateTimeOffset? _modifiedAt;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoaded;
    private bool _isLoading;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    public string DisplayType => EntryType switch
    {
        RemoteEntryType.Directory => "Folder",
        RemoteEntryType.File => "File",
        RemoteEntryType.Symlink => "Symlink",
        _ => "Unknown"
    };

    public RemoteEntryType EntryType
    {
        get => _entryType;
        set
        {
            if (SetProperty(ref _entryType, value))
            {
                OnPropertyChanged(nameof(IsDirectory));
                OnPropertyChanged(nameof(TypeLabel));
                OnPropertyChanged(nameof(Glyph));
            }
        }
    }

    public bool IsDirectory => EntryType == RemoteEntryType.Directory;

    public bool IsNavigable => EntryType == RemoteEntryType.Directory || EntryType == RemoteEntryType.Symlink;

    public long Size
    {
        get => _size;
        set
        {
            if (SetProperty(ref _size, value))
                OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public DateTimeOffset? ModifiedAt
    {
        get => _modifiedAt;
        set
        {
            if (SetProperty(ref _modifiedAt, value))
                OnPropertyChanged(nameof(ModifiedAtLabel));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set => SetProperty(ref _isLoaded, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<RemoteExplorerItem> Children { get; } = new();

    public string Glyph => EntryType switch
    {
        RemoteEntryType.Directory => "📁",
        RemoteEntryType.File => "📄",
        RemoteEntryType.Symlink => "🔗",
        _ => "?",
    };

    public string TypeLabel => EntryType switch
    {
        RemoteEntryType.Directory => "Folder",
        RemoteEntryType.File => "File",
        RemoteEntryType.Symlink => "Symlink",
        _ => "Unknown",
    };

    public string SizeLabel => IsDirectory ? "-" : FormatSize(Size);

    public string ModifiedAtLabel => ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";

    public static string FormatSize(long size)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        double value = size;
        int index = 0;

        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return index == 0 ? $"{value:0} {suffixes[index]}" : $"{value:0.##} {suffixes[index]}";
    }
}
