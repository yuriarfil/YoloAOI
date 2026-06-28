using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YoloAOIApp.ViewModels;

namespace YoloAOIApp.Views;

public partial class InferenceView : Page
{
    private InferenceViewModel? _viewModel;

    public InferenceView() { }

    public InferenceView(string modelPath, string configPath)
    {
        InitializeComponent();
        _viewModel = new InferenceViewModel(modelPath, configPath);
        DataContext = _viewModel;
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*",
            Title = "Select Image for Inference"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var imageBytes = System.IO.File.ReadAllBytes(dialog.FileName);
                if (_viewModel != null)
                    await _viewModel.SetImageAsync(imageBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
