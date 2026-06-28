using System.IO;
using System.Windows;
using YoloAOIApp.Views;

namespace YoloAOIApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Use relative paths (relative to application working directory)
        // Place your files in: Assets/Models/, Assets/Images/, Assets/Config/
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelPath = Path.Combine(appDir, "Assets", "Models", "best.onnx");
        var setupImagePath = Path.Combine(appDir, "Assets", "Images", "setup_image.jpg");
        var configPath = Path.Combine(appDir, "Assets", "Config", "ray_config.json");

        var setupView = new SetupView(modelPath, setupImagePath, configPath);
        SetupFrame.Navigate(setupView);

        var inferenceView = new InferenceView(modelPath, configPath);
        InferenceFrame.Navigate(inferenceView);
    }
}
