using System.Windows;
using System.Windows.Controls;

namespace Win11Tweaker.UI.Controls;

public partial class TweakRow : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(TweakRow),
            new PropertyMetadata(string.Empty, OnHeaderChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(TweakRow),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty IsToggledProperty =
        DependencyProperty.Register(nameof(IsToggled), typeof(bool), typeof(TweakRow),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsToggledChanged));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    private bool _suppressToggleEvent;

    public TweakRow()
    {
        InitializeComponent();
    }

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TweakRow)d).HeaderText.Text = e.NewValue as string ?? string.Empty;

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TweakRow)d).DescriptionText.Text = e.NewValue as string ?? string.Empty;

    private static void OnIsToggledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (TweakRow)d;
        row._suppressToggleEvent = true;
        row.Toggle.IsOn = (bool)e.NewValue;
        row._suppressToggleEvent = false;
    }

    private void Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvent) return;
        IsToggled = Toggle.IsOn;
    }
}
