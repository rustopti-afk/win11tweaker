namespace Win11Tweaker.UI.ViewModels;

public class MainViewModel : BaseViewModel
{
    public TaskbarViewModel Taskbar { get; }
    public ExplorerViewModel Explorer { get; }
    public AppearanceViewModel Appearance { get; }

    public MainViewModel(
        TaskbarViewModel taskbar,
        ExplorerViewModel explorer,
        AppearanceViewModel appearance)
    {
        Taskbar = taskbar;
        Explorer = explorer;
        Appearance = appearance;
    }
}
