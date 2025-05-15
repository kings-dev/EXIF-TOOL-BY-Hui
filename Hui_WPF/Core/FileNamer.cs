// Core/FileNamer.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hui_WPF.Models; // Ensure Models namespace is accessible

namespace Hui_WPF.Core
{
    // Helper class for generating new file and folder names based on naming options.
    public class FileNamer
    {
        // Regex for invalid characters in filenames and paths.
        private readonly Regex _invalidFileNameCharsRegex;
        private readonly Regex _invalidPathCharsRegex;

        // Constructor
        public FileNamer()
        {
            _invalidFileNameCharsRegex = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]");
            _invalidPathCharsRegex = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");
        }

        // Cleans a string to be safe for use as a file name.
        public string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "_";
            string cleaned = _invalidFileNameCharsRegex.Replace(fileName, "_");
            // Remove trailing dots and spaces
            cleaned = cleaned.Trim(' ', '.');
            if (string.IsNullOrEmpty(cleaned)) return "_";
            return cleaned;
        }

        // Cleans a string to be safe for use as a folder name (segment in a path).
        public string CleanFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return "_";
            string cleaned = _invalidPathCharsRegex.Replace(folderName, "_"); // Use path chars for folders
                                                                              // Remove trailing dots and spaces
            cleaned = cleaned.Trim(' ', '.');
            if (string.IsNullOrEmpty(cleaned)) return "_";
            return cleaned;
        }


        // Generates a new file path based on naming options and context.
        public string? GenerateNewFilePath(
            string originalFilePath, // Original path for context (e.g., determining extension)
            NamingOptions options,   // Naming rules (prefix, timestamp, counter format/include)
            int counter,             // Current counter value for this file/group
            string outputDirectory,  // The directory where the new file should be placed
            bool tryAddSuffixOnCollision = true) // Whether to add (1), (2)... on name collision
        {
            if (!Directory.Exists(outputDirectory))
            {
                // Directory does not exist, cannot generate path here.
                // This should ideally be checked and handled by the caller before calling this method.
                return null;
            }

            // Get file extension from the original path
            string extension = Path.GetExtension(originalFilePath);

            // Construct the base file name parts based on the NamingOptions (not the order)
            // This implementation differs from MainWindow's original ConstructNameFromGlobalSettings
            // which uses the ordering and folder/subdir parts.
            // FileNamer's role should probably be simpler: applying the prefix/timestamp/counter
            // and handling cleaning and collision.
            // The complex name construction using order should perhaps be a method in MainViewModel
            // or a separate helper class that uses NamingOptions and the current item's context (original path, parent dir).

            // Let's make FileNamer generate the name based on the provided options (prefix, timestamp, counter)
            // and collision handling. The caller (ViewModel) will decide the prefix/timestamp/counter content
            // based on global settings and item context, and pass them down.

            string timestampPart = "";
            if (options.IncludeTimestamp)
            {
                // Use a fixed time or pass the time from the caller for consistency across items in a batch
                // Let's use the current time here for simplicity, but acknowledge consistency need.
                try { timestampPart = DateTime.Now.ToString(options.TimestampFormat); if (string.IsNullOrWhiteSpace(timestampPart)) timestampPart = ""; }
                catch { timestampPart = "[TS_FmtErr]"; } // Indicate format error
            }

            string counterPart = "";
            if (options.IncludeCounter)
            {
                try 
                { 
                    if (options.CounterFormat == "中文")
                    {
                        counterPart = NumberToChinese(counter);
                    }
                    else
                    {
                        counterPart = counter.ToString(options.CounterFormat);
                    }
                }
                catch { counterPart = "[Cnt_FmtErr]"; } // Indicate format error
            }

            // Combine parts using the separator from options
            var nameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(options.Prefix)) nameParts.Add(CleanFileName(options.Prefix)); // Clean prefix as a filename part
            if (!string.IsNullOrWhiteSpace(timestampPart)) nameParts.Add(CleanFileName(timestampPart)); // Clean timestamp output
            if (!string.IsNullOrWhiteSpace(counterPart)) nameParts.Add(CleanFileName(counterPart)); // Clean counter output


            string baseFileName;
            string separator = options.UseSeparator ? (string.IsNullOrWhiteSpace(options.Separator) ? "_" : options.Separator) : "";

            if (nameParts.Any())
            {
                baseFileName = string.Join(separator, nameParts);
            }
            else
            {
                // Fallback if generated name is empty
                baseFileName = Path.GetFileNameWithoutExtension(originalFilePath) + "_processed"; // Default suffix
            }

            string finalBaseName = CleanFileName(baseFileName); // Final cleaning

            string proposedPath = Path.Combine(outputDirectory, finalBaseName + extension);
            string finalPath = proposedPath;
            int collisionSuffix = 1;

            // Check for collision and add suffix if requested
            while (File.Exists(finalPath) || Directory.Exists(finalPath)) // Check for both file and directory collision
            {
                if (!tryAddSuffixOnCollision)
                {
                    // Collision detected and suffix not requested, return null
                    return null;
                }
                // Add suffix like "(1)", "(2)"
                finalPath = Path.Combine(outputDirectory, $"{finalBaseName}({collisionSuffix++}){extension}");
                if (collisionSuffix > 1000) // Prevent infinite loop
                {
                    // Too many collisions, unlikely to find a unique name
                    return null;
                }
            }

            return finalPath; // Return the unique path
        }


        // Generates a new folder path based on naming options and context.
        // Similar logic to file naming, but uses CleanFolderName and doesn't have an extension.
        public string? GenerateNewFolderPath(
            string originalFolderPath, // Original path for context
            NamingOptions options,   // Naming rules
            int counter,             // Current counter value
            string parentDirectory,  // The directory where the new folder should be placed
            bool tryAddSuffixOnCollision = true) // Whether to add (1), (2)... on collision
        {
            if (!Directory.Exists(parentDirectory))
            {
                return null;
            }

            string timestampPart = "";
            if (options.IncludeTimestamp)
            {
                try { timestampPart = DateTime.Now.ToString(options.TimestampFormat); if (string.IsNullOrWhiteSpace(timestampPart)) timestampPart = ""; }
                catch { timestampPart = "[TS_FmtErr]"; }
            }

            string counterPart = "";
            if (options.IncludeCounter)
            {
                try 
                { 
                    if (options.CounterFormat == "中文")
                    {
                        counterPart = NumberToChinese(counter);
                    }
                    else
                    {
                        counterPart = counter.ToString(options.CounterFormat);
                    }
                }
                catch { counterPart = "[Cnt_FmtErr]"; }
            }

            var nameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(options.Prefix)) nameParts.Add(CleanFolderName(options.Prefix));
            if (!string.IsNullOrWhiteSpace(timestampPart)) nameParts.Add(CleanFolderName(timestampPart));
            if (!string.IsNullOrWhiteSpace(counterPart)) nameParts.Add(CleanFolderName(counterPart));

            string baseFolderName;
            string separator = options.UseSeparator ? (string.IsNullOrWhiteSpace(options.Separator) ? "_" : options.Separator) : "";

            if (nameParts.Any())
            {
                baseFolderName = string.Join(separator, nameParts);
            }
            else
            {
                baseFolderName = Path.GetFileName(originalFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + "_processed";
            }

            string finalBaseName = CleanFolderName(baseFolderName);

            string proposedPath = Path.Combine(parentDirectory, finalBaseName);
            string finalPath = proposedPath;
            int collisionSuffix = 1;

            while (Directory.Exists(finalPath) || File.Exists(finalPath))
            {
                if (!tryAddSuffixOnCollision)
                {
                    return null;
                }
                finalPath = Path.Combine(parentDirectory, $"{finalBaseName}({collisionSuffix++})");
                if (collisionSuffix > 1000)
                {
                    return null;
                }
            }

            return finalPath;
        }

        private string NumberToChinese(int number)
        {
            string[] chineseNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (number <= 10) return chineseNumbers[number];
            if (number < 20) return "十" + (number % 10 == 0 ? "" : chineseNumbers[number % 10]);
            if (number < 100) return chineseNumbers[number / 10] + "十" + (number % 10 == 0 ? "" : chineseNumbers[number % 10]);
            return number.ToString();
        }
    }
}