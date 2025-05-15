using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows; // For Application.Current

namespace Hui_WPF.Models
{
    public class ZoompanSettings : INotifyPropertyChanged
    {
        private ZoompanEffectType _effectType = ZoompanEffectType.ZoomInCenterSlow;
        private double _targetZoom = 1.5;
        private PanDirection _panDirection = PanDirection.None;
        private double _durationSeconds = 5.0;
        private int _fps = 30;
        private int _burstFramerate = 15; // Specific to Burst Mode
        private bool _enableJitter = false;
        private string _customFilterExpression = "zoompan=z='min(zoom+0.0015,1.5)':d=125:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)'";
        // IsBurstMode is not part of settings, but a mode of operation.
        // It should be managed by the calling ViewModel (GenerateVideoViewModel or GenerateBurstViewModel)

        public static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();
            var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }
        public ZoompanEffectType EffectType
        {
            get => _effectType;
            set
            {
                if (SetProperty(ref _effectType, value))
                {
                    OnPropertyChanged(nameof(IsCustomEffectSelected));
                    OnPropertyChanged(nameof(IsCustomExpressionSelected));
                    OnPropertyChanged(nameof(AreStandardControlsEnabled));
                }
            }
        }

        public bool IsCustomEffectSelected => EffectType == ZoompanEffectType.Custom;
        public bool IsCustomExpressionSelected => EffectType == ZoompanEffectType.CustomExpression;
        public bool AreStandardControlsEnabled => EffectType != ZoompanEffectType.CustomExpression;

        public double TargetZoom
        {
            get => _targetZoom;
            set { SetProperty(ref _targetZoom, value); }
        }

        public PanDirection PanDirection
        {
            get => _panDirection;
            set { SetProperty(ref _panDirection, value); }
        }

        public double DurationSeconds
        {
            get => _durationSeconds;
            set { SetProperty(ref _durationSeconds, Math.Max(0.1, value)); }
        }

        public int Fps
        {
            get => _fps;
            set { SetProperty(ref _fps, Math.Max(1, value)); }
        }

        public int BurstFramerate
        {
            get => _burstFramerate;
            set { SetProperty(ref _burstFramerate, Math.Max(1, value)); }
        }

        public bool EnableJitter
        {
            get => _enableJitter;
            set { SetProperty(ref _enableJitter, value); }
        }

        public string CustomFilterExpression
        {
            get => _customFilterExpression;
            set { SetProperty(ref _customFilterExpression, value?.Trim() ?? string.Empty); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (PropertyChanged == null) return;
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
                );
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ZoompanSettings Clone()
        {
            return (ZoompanSettings)this.MemberwiseClone();
        }
    }
}