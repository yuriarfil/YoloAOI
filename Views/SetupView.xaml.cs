using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YoloAOIApp.ViewModels;

namespace YoloAOIApp.Views;

public partial class SetupView : Page
{
    private SetupViewModel? _viewModel;

    public SetupView() 
    {
        InitializeComponent();
        // Fallback viewmodel if needed
        _viewModel = new SetupViewModel();
        DataContext = _viewModel;
    }

    public SetupView(string modelPath, string setupImagePath, string configPath)
    {
        InitializeComponent();
        _viewModel = new SetupViewModel(modelPath, setupImagePath, configPath);
        DataContext = _viewModel;
        
        // Push initial status
        _viewModel.StatusMessage = "Ready. Please click 'Load / Start Setup' to begin.";
    }

    private void Image_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || DisplayImage.Source == null)
            return;

        // Get click position relative to the Image control
        var position = e.GetPosition(DisplayImage);

        // Map UI coordinates to original pixel coordinates
        double pixelX = (position.X / DisplayImage.ActualWidth) * DisplayImage.Source.Width;
        double pixelY = (position.Y / DisplayImage.ActualHeight) * DisplayImage.Source.Height;

        var clickPoint = ((int)pixelX, (int)pixelY);
        _viewModel.HandleImageClick(clickPoint);
    }
}
