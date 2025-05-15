// ViewModels/IProcessingTaskViewModel.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hui_WPF.Models; // NamingOptions, PathOptions will be here

namespace Hui_WPF.ViewModels
{
    public interface IProcessingTaskViewModel
    {
        Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter);
        void LoadSettings(NamingOptions globalNamingOptions,
                          PathOptions customPathOptions,
                          bool generalEnableBackup,
                          int originalFileActionIndex,
                          string outputImageFormat,
                          int jpegQuality,
                          string selectedExifToolTag);
    }
}