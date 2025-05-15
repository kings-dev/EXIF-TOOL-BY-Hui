// Hui_WPF/utils/ZipHelper.cs
// (Ensure filename matches the class name: ZipsHelper.cs if class is ZipsHelper)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Hui_WPF.utils
{
    public static class ZipsHelper // Class name as used in MainWindow
    {
        private static Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>();
        private static string _currentLanguage = "zh";
        private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string _zipsDirectory = Path.Combine(_baseDirectory, "Zips");

        public static void Initialize(Dictionary<string, Dictionary<string, string>> translations, string currentLanguage)
        {
            _translations = translations ?? new Dictionary<string, Dictionary<string, string>>();
            _currentLanguage = currentLanguage ?? "zh";
        }

        public static void SetLanguage(string language)
        {
            _currentLanguage = language ?? "zh";
        }

        private static string GetHelperLocalizedString(string key, string fallback)
        {
            if (_translations.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(_currentLanguage, out var translation) && !string.IsNullOrEmpty(translation)) { return translation; }
                if (_currentLanguage != "en" && langDict.TryGetValue("en", out var enTranslation) && !string.IsNullOrEmpty(enTranslation)) { return enTranslation; }
                if (_currentLanguage != "zh" && langDict.TryGetValue("zh", out var zhTranslation) && !string.IsNullOrEmpty(zhTranslation)) { return zhTranslation; }
            }
            Debug.WriteLine($"[ZipsHelper Loc Fallback] Key: '{key}', Lang: '{_currentLanguage}'");
            return fallback;
        }

        public static async Task EnsureAllToolsReady(
            IProgress<int>? progress,
            IProgress<string> status,
            CancellationToken token)
        {
            status.Report($"[{DateTime.Now:HH:mm:ss}] " + GetHelperLocalizedString("CheckingTools", "正在检查工具..."));
            progress?.Report(5);

            var tasks = new[]
            {
                new { Keyword = "exiftool", Folder = "exiftool",   CheckPath = "exiftool.exe",    ZipKey = "exiftool",   RenameTarget = "exiftool(-k).exe", RenameFlag = true  },
                new { Keyword = "ImageMagick", Folder = "ImageMagick", CheckPath = "magick.exe",    ZipKey = "ImageMagick", RenameTarget = string.Empty, RenameFlag = false },
                new { Keyword = "ffmpeg",     Folder = "ffmpeg",      CheckPath = "bin/ffmpeg.exe", ZipKey = "ffmpeg",     RenameTarget = string.Empty, RenameFlag = false },
                new { Keyword = "ffprobe",    Folder = "ffmpeg",      CheckPath = "bin/ffprobe.exe",ZipKey = "ffmpeg",     RenameTarget = string.Empty, RenameFlag = false }
            };

            int totalSteps = tasks.Length;
            for (int i = 0; i < totalSteps; i++)
            {
                token.ThrowIfCancellationRequested();
                var t = tasks[i];
                status.Report($"[{DateTime.Now:HH:mm:ss}] " + GetHelperLocalizedString($"PreparingTool_{t.Keyword}", $"正在准备 {t.Keyword} …"));
                await Task.Run(() =>
                    EnsureToolReadyInternal(
                        zipKeyword: t.ZipKey,
                        finalFolder: t.Folder,
                        checkRelativePath: t.CheckPath,
                        renameTarget: t.RenameTarget,
                        shouldRename: t.RenameFlag,
                        status: status,
                        toolDisplayName: t.Keyword),
                    token);
                int percentage = (i + 1) * 100 / totalSteps;
                progress?.Report(percentage);
                if (i < totalSteps - 1)
                {
                    await Task.Delay(100, token);
                }
            }
            status.Report($"[{DateTime.Now:HH:mm:ss}] " + GetHelperLocalizedString("AllToolsReadyComplete", "✅ 全部工具已准备就绪。"));
        }

        private static void EnsureToolReadyInternal(
            string zipKeyword,
            string finalFolder,
            string checkRelativePath,
            string renameTarget,
            bool shouldRename,
            IProgress<string> status,
            string toolDisplayName)
        {
            var basePath = _baseDirectory;
            var zipDir = _zipsDirectory;
            var toolDir = Path.Combine(basePath, finalFolder);
            var checkFullPath = Path.Combine(toolDir, checkRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(zipDir))
            {
                status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ " + GetHelperLocalizedString("ErrorZipFolderMissing", $"错误: ZIP 文件夹 '{zipDir}' 不存在。"));
                throw new DirectoryNotFoundException($"Required 'Zips' directory not found at '{zipDir}' for {toolDisplayName}.");
            }

            var zips = Directory.GetFiles(zipDir, "*.zip")
                .Where(f => Path.GetFileName(f).IndexOf(zipKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (!zips.Any())
            {
                status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ " + GetHelperLocalizedString($"ZipNotFound_{zipKeyword}", $"未找到 {toolDisplayName} ZIP 包 (关键词: {zipKeyword})。"));
                throw new FileNotFoundException($"Required zip file matching keyword '{zipKeyword}' not found in '{zipDir}' for {toolDisplayName}.");
            }

            var selectedZip = zips.OrderByDescending(f => ParseVersion(Path.GetFileNameWithoutExtension(f))).FirstOrDefault();
            if (selectedZip == null)
            {
                status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ 无法确定最新的 {toolDisplayName} ZIP 包。");
                throw new FileNotFoundException($"Could not determine latest zip for {toolDisplayName}.");
            }

            var newVer = ParseVersion(Path.GetFileNameWithoutExtension(selectedZip));
            var versionFile = Path.Combine(toolDir, $"{zipKeyword}.version.txt");

            bool skipUpdate = false;
            if (File.Exists(checkFullPath) && File.Exists(versionFile))
            {
                try
                {
                    var oldVerStr = File.ReadAllText(versionFile).Trim();
                    if (Version.TryParse(oldVerStr, out var oldVer) && oldVer >= newVer)
                    {
                        skipUpdate = true;
                        status.Report($"[{DateTime.Now:HH:mm:ss}] ✅ {toolDisplayName} 版本 {oldVer} ≥ 最新 {newVer}，跳过更新。");
                    }
                }
                catch (Exception ex)
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ 读取版本文件 '{versionFile}' 出错: {ex.Message}。将尝试更新。");
                }
            }

            if (skipUpdate) return;

            var tempDir = Path.Combine(Path.GetTempPath(), $"{zipKeyword}_Temp_{Guid.NewGuid():N}");
            try
            {
                status.Report($"[{DateTime.Now:HH:mm:ss}] ⏳ 解压 {Path.GetFileName(selectedZip)} 到临时目录 ({toolDisplayName})...");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                ZipFile.ExtractToDirectory(selectedZip, tempDir);
                status.Report($"[{DateTime.Now:HH:mm:ss}] ℹ️ 解压完成 ({toolDisplayName}).");

                string sourceDirForCopy = tempDir;
                var extractedRootItems = Directory.EnumerateFileSystemEntries(tempDir).ToList();
                if (extractedRootItems.Count == 1 && Directory.Exists(extractedRootItems[0]))
                {
                    sourceDirForCopy = extractedRootItems[0];
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ℹ️ 解压后找到单根目录: {Path.GetFileName(sourceDirForCopy)}，将从此目录复制 ({toolDisplayName})");
                }
                else
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ℹ️ 解压后直接处理临时目录内容: {sourceDirForCopy} ({toolDisplayName})");
                }

                if (shouldRename && !string.IsNullOrEmpty(renameTarget))
                {
                    var oldExeCandidates = Directory.GetFiles(sourceDirForCopy, renameTarget, SearchOption.AllDirectories);
                    if (oldExeCandidates.Any())
                    {
                        string oldExe = oldExeCandidates.First();
                        string targetFileNameInFinalCheckPath = Path.GetFileName(checkRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        var newExePath = Path.Combine(Path.GetDirectoryName(oldExe)!, targetFileNameInFinalCheckPath);
                        if (!File.Exists(newExePath) || !oldExe.Equals(newExePath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Move(oldExe, newExePath, true);
                                status.Report($"[{DateTime.Now:HH:mm:ss}] ℹ️ 已在临时目录重命名 {Path.GetFileName(oldExe)} -> {targetFileNameInFinalCheckPath} ({toolDisplayName})");
                            }
                            catch (Exception rnEx)
                            {
                                status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ 在临时目录重命名时出错: {rnEx.Message} ({toolDisplayName})");
                            }
                        }
                    }
                    else
                    {
                        status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ 未在解压文件中找到需要重命名的 '{renameTarget}' ({toolDisplayName})。");
                    }
                }

                status.Report($"[{DateTime.Now:HH:mm:ss}] ⏳ 正在复制新版本 {toolDisplayName} 到 '{finalFolder}'...");
                if (Directory.Exists(toolDir))
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ℹ️ 清理旧目录: {toolDir} ({toolDisplayName})");
                    try { Directory.Delete(toolDir, true); }
                    catch (IOException ioEx) { status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ 清理旧目录 '{toolDir}' 时出错 (可能被占用): {ioEx.Message}"); Thread.Sleep(500); try { Directory.Delete(toolDir, true); } catch (Exception finalEx) { status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ 无法删除旧目录 '{toolDir}': {finalEx.Message}"); throw; } }
                    catch (Exception ex) { status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ 清理旧目录 '{toolDir}' 时发生未知错误: {ex.Message}"); throw; }
                }

                CopyDirectory(sourceDirForCopy, toolDir, status);
                File.WriteAllText(versionFile, newVer.ToString());
                status.Report($"[{DateTime.Now:HH:mm:ss}] ✅ {toolDisplayName} 更新完成 (版本 {newVer})。");
            }
            catch (Exception ex)
            {
                status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ 处理 {toolDisplayName} 时发生错误: {ex.Message}");
                throw;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (Exception ex) { status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ 清理临时目录 '{tempDir}' 时出错: {ex.Message}"); }
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, IProgress<string> status)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) { throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDir}"); }
            Directory.CreateDirectory(destinationDir);
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destinationDir, file.Name);
                try
                {
                    file.CopyTo(tempPath, true);
                }
                catch (IOException ioEx)
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ " + GetHelperLocalizedString("ErrorCopyingFileIO", $"复制文件 '{file.Name}' 时发生IO错误: {ioEx.Message}"));
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ⚠️ " + GetHelperLocalizedString("ErrorCopyingFileAuth", $"复制文件 '{file.Name}' 时权限不足: {uaEx.Message}"));
                }
                catch (Exception ex)
                {
                    status.Report($"[{DateTime.Now:HH:mm:ss}] ❌ " + GetHelperLocalizedString("ErrorCopyingFileUnknown", $"复制文件 '{file.Name}' 时发生未知错误: {ex.Message}"));
                }
            }
            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destinationDir, subdir.Name);
                CopyDirectory(subdir.FullName, tempPath, status);
            }
        }

        private static Version ParseVersion(string name)
        {
            var m = Regex.Match(name, @"(\d+(\.\d+){1,3})");
            return m.Success && Version.TryParse(m.Value, out var v) ? v : new Version(0, 0, 0, 0);
        }
    }
}