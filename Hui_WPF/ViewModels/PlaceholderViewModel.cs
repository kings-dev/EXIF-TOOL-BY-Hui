// ViewModels/PlaceholderViewModel.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hui_WPF.Models; // Ensure models are accessible

namespace Hui_WPF.ViewModels
{
    // ViewModel for navigation items that don't have a dedicated view/logic yet, or separators.
    public class PlaceholderViewModel : ViewModelBase
    {
        public string Message { get; }

        public PlaceholderViewModel(string message)
        {
            Message = message;
        }
    }

    // Placeholder implementation for IProcessingTaskViewModel
    // Ensures that any ViewModel can potentially be cast to this interface,
    // and if it's a Placeholder, ExecuteAsync does nothing.
    // This is not ideal, a better approach is to only cast/call ExecuteAsync
    // if the ViewModel *actually* implements IProcessingTaskViewModel.
    // Let's remove IProcessingTaskViewModel from here and ensure checks in MainViewModel.
}

