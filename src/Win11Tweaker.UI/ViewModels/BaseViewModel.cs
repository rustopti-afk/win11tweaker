using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Win11Tweaker.UI.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    protected void SetStatus(string message)
    {
        StatusMessage = message;
        // Find main window and update its status bar
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    protected void UpdateChangesCount()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.UpdateChangesCount();
    }
}
