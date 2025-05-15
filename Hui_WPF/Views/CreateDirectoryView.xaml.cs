using System.Windows.Controls;
using Hui_WPF.ViewModels;

namespace Hui_WPF.Views
{
    public partial class CreateDirectoryView : UserControl
    {
        private CreateDirectoryViewModel? ViewModel => DataContext as CreateDirectoryViewModel;

        public CreateDirectoryView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.UpdatePreview();
                }
            };
            this.Loaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.UpdatePreview();
                }
            };
        }
    }
}