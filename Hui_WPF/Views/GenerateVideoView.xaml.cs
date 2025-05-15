// Hui_WPF/Views/GenerateVideoView.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hui_WPF.ViewModels;
using Hui_WPF.Utils;
using Hui_WPF.Models;

namespace Hui_WPF.Views
{
    public partial class GenerateVideoView : UserControl
    {
        private GenerateVideoViewModel? ViewModel => DataContext as GenerateVideoViewModel;


        //private bool _useTimestampSubfolderFromGlobal = true;

        //public bool UseTimestampSubfolderFromGlobal
        //{
        //    get => _useTimestampSubfolderFromGlobal;
        //    set => SetProperty(ref _useTimestampSubfolderFromGlobal, value);
        //}


        //public GenerateVideoViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        //{
        //    _reporter = reporter;
        //    _mainViewModel = mainViewModel;
        //    // ... (其他构造函数代码) ...
        //    // Load initial value from MainViewModel when this VM is created/loaded
        //    UseTimestampSubfolderFromGlobal = _mainViewModel.UseTimestampSubfolderForMedia;
        //}


        //public void LoadSettings(NamingOptions globalNamingOptions,
        //                          PathOptions customPathOptions,
        //                          bool generalEnableBackup,
        //                          int originalFileActionIndex,
        //                          string outputImageFormat,
        //                          int jpegQuality,
        //                          string selectedExifToolTag)
        //{
        //    // ... (加载其他设置) ...
        //    // Update from MainViewModel's current state when settings are reloaded
        //    UseTimestampSubfolderFromGlobal = _mainViewModel.UseTimestampSubfolderForMedia;
        //    // ...
        //}


        public GenerateVideoView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure ViewModel is available before trying to use it
            if (ViewModel != null)
            {
                // Setup focus handlers after ViewModel is likely set (or via DataContextChanged)
                txtNewPresetName_VV.GotFocus += TxtNewPresetName_GotFocus_UserAction;
                txtNewPresetName_VV.LostFocus += TxtNewPresetName_LostFocus_UserAction;
                InitializeViewPostLoad(); // Initialize placeholder text
            }
            else
            {
                // ViewModel not yet available, try setting up on DataContextChanged
                this.DataContextChanged += OnDataContextChanged_SetupFocusHandlers;
            }
        }

        private void OnDataContextChanged_SetupFocusHandlers(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                // DataContext is now set, set up focus handlers and initialize
                txtNewPresetName_VV.GotFocus -= TxtNewPresetName_GotFocus_UserAction; // Remove first to avoid duplicates
                txtNewPresetName_VV.LostFocus -= TxtNewPresetName_LostFocus_UserAction;
                txtNewPresetName_VV.GotFocus += TxtNewPresetName_GotFocus_UserAction;
                txtNewPresetName_VV.LostFocus += TxtNewPresetName_LostFocus_UserAction;
                InitializeViewPostLoad();
                this.DataContextChanged -= OnDataContextChanged_SetupFocusHandlers; // Unsubscribe after setup
            }
        }


        private void TxtNewPresetName_GotFocus_UserAction(object sender, RoutedEventArgs e)
        {
            if (txtNewPresetName_VV == null || ViewModel == null) return;
            if (txtNewPresetName_VV.Text == ViewModel.NewPresetPlaceholderText)
            {
                txtNewPresetName_VV.Text = "";
                txtNewPresetName_VV.Foreground = SystemColors.WindowTextBrush;
            }
        }

        private void TxtNewPresetName_LostFocus_UserAction(object sender, RoutedEventArgs e)
        {
            if (txtNewPresetName_VV == null || ViewModel == null) return;
            if (string.IsNullOrWhiteSpace(txtNewPresetName_VV.Text))
            {
                txtNewPresetName_VV.Text = ViewModel.NewPresetPlaceholderText;
                txtNewPresetName_VV.Foreground = Brushes.Gray;
            }
        }

        internal void InitializeViewPostLoad()
        {
            if (ViewModel != null && txtNewPresetName_VV != null)
            {
                if (string.IsNullOrWhiteSpace(txtNewPresetName_VV.Text) || txtNewPresetName_VV.Text == "新预设名称" || txtNewPresetName_VV.Text == ViewModel.NewPresetPlaceholderText)
                {
                    txtNewPresetName_VV.Text = ViewModel.NewPresetPlaceholderText;
                    txtNewPresetName_VV.Foreground = Brushes.Gray;
                }
            }
        }
    }
}