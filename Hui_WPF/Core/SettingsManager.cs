// Core/SettingsManager.cs
using System;
using System.Diagnostics;
using Hui_WPF.Models;
using Hui_WPF.Properties;

namespace Hui_WPF.Core
{
    public class SettingsManager
    {
        public SettingsManager() { }

        public (string Language, NamingOptions Naming, PathOptions Paths, string ExifToolTag) LoadApplicationSettings()
        {
            try
            {
                var settings = Settings.Default;
                var namingOptions = new NamingOptions
                {
                    Prefix = settings.UserGeneralRenamePrefix ?? "",
                    IncludeFolder = true,
                    FolderText = settings.UserNamingFolderText ?? "FD",
                    IncludeParentDir = true,
                    ParentDirText = settings.UserNamingParentDirText ?? "HF",
                    IncludeSubDir = true,
                    SubDirText = settings.UserNamingSubDirText ?? "sF",
                    IncludeFileName = true,
                    FileNameText = settings.UserNamingFileNameText ?? "File",
                    IncludeTimestamp = true,
                    TimestampFormat = settings.UserTimestampFormat ?? NamingOptionsDefaults.TimestampFormat,
                    IncludeCounter = true,
                    CounterFormat = settings.UserCounterFormat ?? NamingOptionsDefaults.CounterFormat,
                    CounterStartValue = settings.UserCounterStartValue,
                    UseSeparator = settings.UserNamingUseSeparator,
                    Separator = settings.UserNamingSeparator ?? "_",
                    OutputSubfolder = settings.UserOutputSubfolder ?? "Processed"
                };

                var pathOptions = new PathOptions
                {
                    UseCustomImageOutputPath = !string.IsNullOrWhiteSpace(settings.UserImageOutputPath),
                    CustomImageOutputPath = settings.UserImageOutputPath ?? "",
                    UseCustomVideoOutputPath = !string.IsNullOrWhiteSpace(settings.UserVideoOutputPath),
                    CustomVideoOutputPath = settings.UserVideoOutputPath ?? "",
                    UseCustomBackupPath = !string.IsNullOrWhiteSpace(settings.UserBackupOutputPath),
                    CustomBackupPath = settings.UserBackupOutputPath ?? ""
                };

                string language = settings.UserLanguage ?? "zh";
                string exifToolTag = settings.UserExifToolTag ?? "ExifTool";
                return (language, namingOptions, pathOptions, exifToolTag);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load application settings: {ex.Message}");
                return ("zh", NamingOptionsDefaults.Default, PathOptionsDefaults.Default, "ExifTool");
            }
        }

        public void SaveApplicationSettings(string language, NamingOptions naming, PathOptions paths, string exifToolTag)
        {
            try
            {
                var settings = Settings.Default;
                settings.UserLanguage = language;
                settings.UserGeneralRenamePrefix = naming.Prefix ?? "";
                settings.UserNamingIncludeFolder = naming.IncludeFolder;
                settings.UserNamingFolderText = naming.FolderText ?? "FD";
                settings.UserNamingIncludeParentDir = naming.IncludeParentDir;
                settings.UserNamingParentDirText = naming.ParentDirText ?? "HF";
                settings.UserNamingIncludeSubDir = naming.IncludeSubDir;
                settings.UserNamingSubDirText = naming.SubDirText ?? "sF";
                settings.UserNamingIncludeFileName = naming.IncludeFileName;
                settings.UserNamingFileNameText = naming.FileNameText ?? "File";
                settings.UserEnableTimestamp = naming.IncludeTimestamp;
                settings.UserTimestampFormat = naming.TimestampFormat;
                settings.UserEnableCounter = naming.IncludeCounter;
                settings.UserCounterFormat = naming.CounterFormat;
                settings.UserCounterStartValue = naming.CounterStartValue;
                settings.UserNamingUseSeparator = naming.UseSeparator;
                settings.UserNamingSeparator = naming.Separator ?? "_";
                settings.UserOutputSubfolder = naming.OutputSubfolder ?? "Processed";

                settings.UserImageOutputPath = paths.UseCustomImageOutputPath ? paths.CustomImageOutputPath : "";
                settings.UserVideoOutputPath = paths.UseCustomVideoOutputPath ? paths.CustomVideoOutputPath : "";
                settings.UserBackupOutputPath = paths.UseCustomBackupPath ? paths.CustomBackupPath : "";

                settings.UserExifToolTag = exifToolTag;
                settings.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save application settings: {ex.Message}");
            }
        }
    }
}