using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.Core
{
    public class ExifProcessor
    {
        private readonly string _exifToolPath;
        public string ExifToolPath => _exifToolPath;

        public ExifProcessor(string exifToolPath)
        {
            _exifToolPath = exifToolPath;
        }

        public record ProcessingResult(bool Success, string? NewFilePath = null, string? ErrorMessage = null, int ExitCode = -1);

        public async Task<ProcessingResult> RemoveExifAsync(
            string sourceFilePath,
            string destinationFilePath,
            ExifRemovalOptions options,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_exifToolPath) || !File.Exists(_exifToolPath))
            {
                return new ProcessingResult(false, ErrorMessage: "ExifTool executable not found.", ExitCode: -1);
            }
            progress?.Report(0);
            try
            {
                if (!File.Exists(sourceFilePath)) return new ProcessingResult(false, ErrorMessage: $"Source file not found: {sourceFilePath}");
                if (!string.Equals(sourceFilePath, destinationFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    string? destDir = Path.GetDirectoryName(destinationFilePath);
                    if (destDir != null && !Directory.Exists(destDir))
                    {
                        try { Directory.CreateDirectory(destDir); }
                        catch (Exception dirEx) { return new ProcessingResult(false, ErrorMessage: $"Failed to create output directory '{destDir}': {dirEx.Message}"); }
                    }
                }
                string args = BuildExifRemovalArgs(sourceFilePath, destinationFilePath, options);
                var (success, exitCode, stdErr, _) = await RunExternalProcessAsync(_exifToolPath, args, cancellationToken);
                progress?.Report(100);
                if (success)
                {
                    if (File.Exists(destinationFilePath) && new FileInfo(destinationFilePath).Length > 0)
                    {
                        return new ProcessingResult(true, NewFilePath: destinationFilePath, ExitCode: exitCode);
                    }
                    else
                    {
                        string error = $"ExifTool reported success (Code {exitCode}) but output file is missing or empty.";
                        if (File.Exists(destinationFilePath)) { try { File.Delete(destinationFilePath); } catch { } }
                        return new ProcessingResult(false, ErrorMessage: error, ExitCode: exitCode);
                    }
                }
                else
                {
                    string error = $"ExifTool failed (Code {exitCode}). Error: {stdErr}";
                    try { if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath); } catch { }
                    return new ProcessingResult(false, ErrorMessage: error, ExitCode: exitCode);
                }
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath); } catch { }
                return new ProcessingResult(false, ErrorMessage: $"Unexpected error during EXIF removal: {ex.Message}");
            }
        }

        public async Task<ProcessingResult> WriteExifAsync(
           string targetFilePath,
           ExifWriteOptions metadataToWrite,
           IProgress<double>? progress = null,
           CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_exifToolPath) || !File.Exists(_exifToolPath))
            {
                return new ProcessingResult(false, ErrorMessage: "ExifTool executable not found.", ExitCode: -1);
            }
            progress?.Report(0);
            try
            {
                if (!File.Exists(targetFilePath)) return new ProcessingResult(false, ErrorMessage: $"Target file not found: {targetFilePath}");
                string args = BuildExifWriteArgs(targetFilePath, metadataToWrite);
                if (string.IsNullOrWhiteSpace(args))
                {
                    progress?.Report(100);
                    return new ProcessingResult(true, NewFilePath: targetFilePath, ExitCode: 0);
                }
                args += " -overwrite_original";
                var (success, exitCode, stdErr, _) = await RunExternalProcessAsync(_exifToolPath, args, cancellationToken);
                progress?.Report(100);
                if (success)
                {
                    return new ProcessingResult(true, NewFilePath: targetFilePath, ExitCode: exitCode);
                }
                else
                {
                    string error = $"ExifTool failed writing tags (Code {exitCode}). Error: {stdErr}";
                    return new ProcessingResult(false, ErrorMessage: error, ExitCode: exitCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ProcessingResult(false, ErrorMessage: $"Unexpected error during EXIF writing: {ex.Message}");
            }
        }

        private string BuildExifRemovalArgs(string source, string destination, ExifRemovalOptions options)
        {
            var sb = new StringBuilder();
            if (options.RemoveAllMetadata) { sb.Append("-all= "); }
            else
            {
                sb.Append("-all= -tagsFromFile @");
                if (options.KeepDateTaken) sb.Append(" -DateTimeOriginal -CreateDate -ModifyDate -TrackCreateDate -TrackModifyDate -MediaCreateDate -MediaModifyDate");
                if (options.KeepGps) sb.Append(" -GPS*");
                if (options.KeepOrientation) sb.Append(" -Orientation");
                if (options.KeepCameraInfo) sb.Append(" -Make -Model -Software");
                if (options.KeepColorSpace) sb.Append(" -ColorSpace -ExifIFD:ColorSpace");
                sb.Append(" ");
            }
            if (options.RemoveThumbnail) { sb.Append("-ThumbnailImage= "); }
            if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append($"-o \"{destination}\" ");
                sb.Append($"-overwrite_original_in_place ");
            }
            else
            {
                sb.Append($"-overwrite_original ");
            }
            sb.Append($"\"{source}\"");
            sb.Append(" -q -m");
            return sb.ToString();
        }

        private string BuildExifWriteArgs(string targetFile, ExifWriteOptions options)
        {
            var sb = new StringBuilder();
            bool tagAdded = false;
            Action<string, string?> addTag = (tagName, value) => {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string safeValue = value.Replace("\"", "\\\"");
                    sb.Append($"-{tagName}=\"{safeValue}\" ");
                    tagAdded = true;
                }
            };
            if (options.WriteCommonTags)
            {
                addTag("Artist", options.Artist);
                addTag("XPAuthor", options.Artist);
                addTag("Copyright", options.Copyright);
                addTag("UserComment", options.Comment);
                addTag("XPComment", options.Comment);
                addTag("ImageDescription", options.Description);
                addTag("XPTitle", options.Description);
                if (options.Rating.HasValue)
                {
                    if (options.Rating.Value >= 0 && options.Rating.Value <= 5)
                    {
                        sb.Append($"-Rating={options.Rating.Value} ");
                        sb.Append($"-XMP-xmp:Rating={options.Rating.Value} ");
                        tagAdded = true;
                    }
                }
            }
            if (options.WriteDateTaken && options.DateTimeOriginal.HasValue)
            {
                string exifDate = options.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss");
                sb.Append($"-DateTimeOriginal=\"{exifDate}\" ");
                sb.Append($"-CreateDate=\"{exifDate}\" ");
                sb.Append($"-ModifyDate=\"{exifDate}\" ");
                tagAdded = true;
            }
            if (options.WriteGps && options.Latitude.HasValue && options.Longitude.HasValue)
            {
                string latStr = options.Latitude.Value.ToString(CultureInfo.InvariantCulture);
                string lonStr = options.Longitude.Value.ToString(CultureInfo.InvariantCulture);
                char latRef = options.Latitude.Value >= 0 ? 'N' : 'S';
                char lonRef = options.Longitude.Value >= 0 ? 'E' : 'W';
                sb.Append($"-GPSLatitude={latStr} ");
                sb.Append($"-GPSLatitudeRef={latRef} ");
                sb.Append($"-GPSLongitude={lonStr} ");
                sb.Append($"-GPSLongitudeRef={lonRef} ");
                tagAdded = true;
            }
            if (tagAdded)
            {
                sb.Append($"\"{targetFile}\"");
                sb.Append(" -q -m");
                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        private async Task<(bool Success, int ExitCode, string ErrorOutput, string StandardOutput)> RunExternalProcessAsync(
            string fileName, string arguments, CancellationToken cancellationToken, bool readStdOut = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = readStdOut,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? ""
            };
            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var errorOutput = new StringBuilder();
                var standardOutput = new StringBuilder();
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };
                if (readStdOut) { process.OutputDataReceived += (sender, e) => { if (e.Data != null) standardOutput.AppendLine(e.Data); }; }
                try
                {
                    process.Start();
                    process.BeginErrorReadLine();
                    if (readStdOut) process.BeginOutputReadLine();
                    await process.WaitForExitAsync(cancellationToken);
                    process.WaitForExit();
                    bool success = process.ExitCode == 0;
                    return (success, process.ExitCode, errorOutput.ToString().Trim(), standardOutput.ToString().Trim());
                }
                catch (OperationCanceledException)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    throw;
                }
                catch (Exception ex)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    throw new Exception($"Error running external process '{fileName}': {ex.Message}", ex);
                }
            }
        }
    }
}