using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HostsTool;

public class HostEntry : INotifyPropertyChanged
{
    private bool _isActive;
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _isReadOnly;

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    // When true the UI should not allow editing this entry
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (SetField(ref _isReadOnly, value))
            {
                // notify that IsEditable effectively changed
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
            }
        }
    }

    // Convenience property for binding UI element enabled state
    public bool IsEditable => !_isReadOnly;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
