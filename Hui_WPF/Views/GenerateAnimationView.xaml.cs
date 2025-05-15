// Hui_WPF/Views/GenerateAnimationView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using Hui_WPF.ViewModels; // Use ViewModel namespace

namespace Hui_WPF.Views
{
    // Code-behind for GenerateAnimationView.xaml.
    // Should contain minimal UI-specific logic, mostly InitializeComponent().
    // Interactions and state are handled by GenerateAnimationViewModel.
    public partial class GenerateAnimationView : UserControl
    {
        // ViewModel instance (can be accessed via DataContext)
        private GenerateAnimationViewModel? ViewModel => DataContext as GenerateAnimationViewModel;

        public GenerateAnimationView()
        {
            InitializeComponent();

            // Keep slider value changed handler if the ViewModel updates separate text blocks
            // or performs other UI actions based on the slider value.
            // If the TextBlock text is bound directly to the slider's Value,
            // this handler might be redundant for the bound text.
            sliderAnimationDelay_AnimView.ValueChanged += SliderAnimationDelay_ValueChanged;
        }

        // Handles ValueChanged event for the frame delay slider.
        // Calls a ViewModel method or updates UI text blocks.
        private void SliderAnimationDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // The text block is bound directly to the slider's value in XAML.
            // This handler might be redundant for the bound text.
            // If other UI updates depended on this, add logic here or in ViewModel.
            // ViewModel.UpdateFrameDelayText(); // Example: Call VM method
        }

        // Properties like AnimationFileName, FrameDelayMs, SelectedFormat, LoopAnimation are now in ViewModel.
        // The XAML binds directly to these ViewModel properties.
    }
}