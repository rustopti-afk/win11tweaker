using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Win11Tweaker.UI.ViewModels;

namespace Win11Tweaker.UI.Pages;

public partial class ExplorerPage : Page
{
    public ExplorerPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ExplorerViewModel>();
    }
}
