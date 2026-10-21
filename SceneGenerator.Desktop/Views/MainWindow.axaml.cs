using Avalonia.Controls;
using Avalonia.Interactivity;
using SceneGenerator.Desktop.Controls;

namespace SceneGenerator.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) =>
        this.FindControl<SceneView>("SceneViewport")?.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) =>
        this.FindControl<SceneView>("SceneViewport")?.ZoomOut();

    private void OnFitScene(object? sender, RoutedEventArgs e) =>
        this.FindControl<SceneView>("SceneViewport")?.FitScene();
}
