using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Hui_WPF.Models
{
    public class DirectoryRule
    {
        public string BasePath { get; set; } = string.Empty;
        public string Prefix { get; set; } = "Folder_";
        public int Count { get; set; } = 1;
        public bool Recursive { get; set; } = false;
        public List<DirectoryRule> SubRules { get; set; } = new List<DirectoryRule>();

        private static readonly Regex invalidPathCharsRegex = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

        public static string CleanPathSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment)) return "_";
            string cleaned = invalidPathCharsRegex.Replace(segment, "_");
            cleaned = cleaned.Trim(' ', '.');
            if (string.IsNullOrEmpty(cleaned)) return "_";
            return cleaned;
        }

        public override string ToString()
        {
            return $"{Prefix} ×{Count} {(Recursive ? "[递归]" : "")}";
        }
    }
}