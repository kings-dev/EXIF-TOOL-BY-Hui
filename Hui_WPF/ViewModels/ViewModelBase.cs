// ViewModels/ViewModelBase.cs
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Hui_WPF.Models; // Ensure models are accessible

namespace Hui_WPF.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Check if there are any subscribers before invoking
            if (PropertyChanged == null) return;

            // Use dispatcher to ensure the event is raised on the UI thread
            // Use BeginInvokeAsync for potentially lower priority updates like logs
            // Use InvokeAsync or Invoke for updates that need to happen immediately or modal dialogs
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
               );
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}