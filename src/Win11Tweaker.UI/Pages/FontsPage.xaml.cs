using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Win11Tweaker.Core.Services;

namespace Win11Tweaker.UI.Pages;

public partial class FontsPage : Page
{
    private readonly FontService _svc;

    public FontsPage()
    {
        InitializeComponent();
        _svc = App.Services.GetRequiredService<FontService>();
        PopulateFontPicker();
    }

    private void PopulateFontPicker()
    {
        // Load all system fonts into the combo box
        var fonts = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(f => f)
            .ToList();

        SystemFontPicker.ItemsSource = fonts;
        SystemFontPicker.SelectedItem = "Segoe UI";
    }

    // ── Font preview ──────────────────────────────────────────────────────────

    private void SystemFontPicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SystemFontPicker.SelectedItem is string fontName)
        {
            try
            {
                FontPreviewText.FontFamily = new FontFamily(fontName);
                ApplyFontBtn.IsEnabled = true;
            }
            catch
            {
                ApplyFontBtn.IsEnabled = false;
            }
        }
    }

    // ── Apply / restore ───────────────────────────────────────────────────────

    private void ApplySystemFont_Click(object sender, RoutedEventArgs e)
    {
        if (SystemFontPicker.SelectedItem is not string fontName) return;

        try
        {
            _svc.ReplaceSegoeUi(fontName);
            SetMainStatus($"✓ System font set to '{fontName}'. Restart to apply.");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ {ex.Message}");
        }
    }

    private void RestoreDefaultFont_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _svc.RestoreSegoeUi();
            FontPreviewText.FontFamily = new FontFamily("Segoe UI");
            SystemFontPicker.SelectedItem = "Segoe UI";
            SetMainStatus("✓ System font restored to Segoe UI. Restart to apply.");
        }
        catch (Exception ex)
        {
            SetMainStatus($"✗ {ex.Message}");
        }
    }

    // ── Install font ──────────────────────────────────────────────────────────

    private void BrowseFont_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose Font File",
            Filter = "Font Files|*.ttf;*.otf;*.ttc|All Files|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        InstallFonts(dlg.FileNames);
    }

    private void FontDrop_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            FontDropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        }
        e.Handled = true;
    }

    private void FontDrop_DragLeave(object sender, DragEventArgs e)
    {
        FontDropZone.BorderBrush = (Brush)FindResource("SystemControlForegroundBaseMediumBrush");
    }

    private void FontDrop_Drop(object sender, DragEventArgs e)
    {
        FontDropZone.BorderBrush = (Brush)FindResource("SystemControlForegroundBaseMediumBrush");
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var fonts = files.Where(f =>
            f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (fonts.Length > 0)
            InstallFonts(fonts);
        e.Handled = true;
    }

    private void InstallFonts(string[] paths)
    {
        int ok = 0, fail = 0;
        foreach (var path in paths)
        {
            try { _svc.InstallFont(path); ok++; }
            catch { fail++; }
        }

        var msg = ok > 0 ? $"✓ {ok} font(s) installed" : "";
        if (fail > 0) msg += $" ({fail} failed)";
        SetMainStatus(msg.TrimStart());

        // Refresh font picker
        PopulateFontPicker();
    }

    private void SetMainStatus(string msg)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(msg);
    }
}
