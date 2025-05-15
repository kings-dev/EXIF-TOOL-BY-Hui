using System;

namespace Hui_WPF.Models
{
    [Serializable]
    public class CustomZoompanPreset
    {
        private string _name = "Unnamed Preset";
        private string _expression = "z='min(zoom+0.0015,1.5)':d=125:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)'";

        public string Name
        {
            get => _name;
            set => _name = string.IsNullOrWhiteSpace(value) ? "Unnamed Preset" : value;
        }

        public string Expression
        {
            get => _expression;
            set => _expression = value ?? string.Empty;
        }

        public string Description => $"自定义: {Name}";

        public CustomZoompanPreset() { }

        public CustomZoompanPreset(string name, string expression)
        {
            Name = name;
            Expression = expression;
        }

        public override string ToString() => Name ?? "Unnamed";

        public override bool Equals(object? obj)
        {
            if (obj is CustomZoompanPreset other)
            {
                return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                       this.Expression == other.Expression;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name ?? string.Empty), this.Expression);
        }
    }
}