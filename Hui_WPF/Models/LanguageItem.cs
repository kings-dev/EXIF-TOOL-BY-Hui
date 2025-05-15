namespace Hui_WPF.Models
{
    public class LanguageItem
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }

        public LanguageItem(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public LanguageItem()
        {
            Code = string.Empty;
            DisplayName = string.Empty;
        }

        public override string ToString() => DisplayName;
    }
}