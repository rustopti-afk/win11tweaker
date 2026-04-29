using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Win11Tweaker.UI.ViewModels;

namespace Win11Tweaker.UI.Pages;

public partial class TaskbarPage : Page
{
    public TaskbarPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TaskbarViewModel>();
    }
}
