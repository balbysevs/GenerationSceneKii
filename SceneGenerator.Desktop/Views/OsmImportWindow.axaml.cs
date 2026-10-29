using Avalonia.Controls;
using SceneGenerator.Desktop.ViewModels;

namespace SceneGenerator.Desktop.Views;

public partial class OsmImportWindow : Window
{
    public OsmImportWindow() => InitializeComponent();

    protected override void OnClosed(System.EventArgs e)
    {
        if (DataContext is OsmImportViewModel model) model.Cancel();
        base.OnClosed(e);
    }
}
