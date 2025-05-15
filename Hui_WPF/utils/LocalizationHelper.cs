// Utils/LocalizationHelper.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using Hui_WPF.Models; // Added using

namespace Hui_WPF.Utils
{
    public static class LocalizationHelper
    {
        private static Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>();
        private static string _currentLanguage = "zh";
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // In Utils/LocalizationHelper.cs -> Initialize()
            // ... (existing translations) ...
            AddTranslation("Nav_CreateDirectory", "Create Directory", "创建目录");
            AddTranslation("Nav_DirectRename", "Direct Rename", "直接命名");
            AddTranslation("Nav_FileProcessing", "FILE PROCESSING", "文件处理"); // Separator
            AddTranslation("Nav_ExifRemove", "EXIF Remove", "EXIF 移除");
            AddTranslation("Nav_ExifWrite", "EXIF Write", "EXIF 写入");
            AddTranslation("Nav_MediaGeneration", "MEDIA GENERATION", "媒体生成"); // Separator
            AddTranslation("Nav_GenerateVideo", "Generate Video", "生成视频");
            AddTranslation("Nav_GenerateBurst", "Generate Burst", "生成连拍");
            AddTranslation("Nav_GenerateAnimation", "Generate Animation", "生成动画");

            AddTranslation("Tooltip_FolderPath", "Selected source folder or files path", "已选源文件夹或文件路径");
            AddTranslation("Tooltip_BrowseFolder", "Browse for source files or folders", "浏览源文件或文件夹");
            AddTranslation("Tooltip_LanguageSelector", "Select application language", "选择应用程序语言");
            AddTranslation("Tooltip_NavigationPane", "Select a processing task", "选择处理任务");
            AddTranslation("Tooltip_StartProcessing", "Start the selected processing task", "开始选定的处理任务");
            AddTranslation("Tooltip_CancelAction", "Cancel the current processing task", "取消当前处理任务");
            AddTranslation("Tooltip_DragDropPanel", "Drag and drop source files or folders here", "拖放源文件或文件夹到此处");
            AddTranslation("Tooltip_Log", "Processing log messages", "处理日志信息");
            // ...
            AddTranslation("WindowTitle", "ExifDog - EXIF And Video Tool", "ExifDog - EXIF 和 视频工具");
            AddTranslation("SelectFolderLabel", "Select Folder", "📂 选择文件夹");
            AddTranslation("SelectImagesLabel", "Select Images", "🖼️ 选择源图片");
            AddTranslation("BrowseFolderLabel", "Browse...", "浏览...");
            AddTranslation("StartProcessingLabel", "Start Processing", "开始处理");
            AddTranslation("CancelButtonLabel", "Cancel", "取消");
            AddTranslation("LogLabel", "Processing Log:", "处理日志:");
            AddTranslation("ClearLog", "Clear Log", "清除日志");
            AddTranslation("SaveLog", "Save Log", "保存日志");
            AddTranslation("ProgressHintLabel", "Progress:", "处理进度:");
            AddTranslation("ProcessStatusLabelInitial", "Ready", "就绪");
            AddTranslation("UnselectedState", "No folder/source images selected", "未选择文件夹/源图片");
            AddTranslation("Selected", "Selected: {0}", "已选择: {0}");
            AddTranslation("Folders", "Folders", "文件夹");
            AddTranslation("Files", "Files", "文件");
            AddTranslation("And", " and ", " 和 ");
            AddTranslation("InvalidItemsSelected", "Invalid items selected", "选择了无效的项目");
            AddTranslation("ProgressReady", "Ready to process", "准备处理");
            AddTranslation("SupportedImageFiles", "Supported Image Files", "支持的图片文件");
            AddTranslation("AllFiles", "All Files", "所有文件");
            AddTranslation("SelectFolder", "Select Folder", "选择文件夹");
            AddTranslation("SelectOneOrMoreImages", "Select One or More Images", "选择一个或多个图片");
            AddTranslation("SelectCustomBackupFolderTitle", "Select Custom Backup Folder", "选择自定义备份文件夹");
            AddTranslation("SelectFilesAndFoldersTitle", "Select Files and/or Folders", "选择文件和/或文件夹");
            AddTranslation("SelectItemsPrompt", "Select Items", "选择项目");
            AddTranslation("AllFilesFilter", "All Files (*.*)|*.*", "所有文件 (*.*)|*.*");
            AddTranslation("Tip", "Tip", "提示");
            AddTranslation("SaveErrorTitle", "Save Error", "保存错误");
            AddTranslation("DirectRename_ErrorTitle", "Direct Rename Error", "直接重命名错误");
            AddTranslation("ProcessingErrorTitle", "Processing Error", "处理错误");
            AddTranslation("RetryTitle", "Rename Access Denied", "重命名访问被拒绝");
            AddTranslation("TextFile", "Text File", "文本文件");
            AddTranslation("ZoompanSettingsTitle", "Effect Settings", "效果参数设置");
            AddTranslation("ZoompanGenerationErrorTitle", "Video/Animation Generation Error", "视频/动画生成错误");
            AddTranslation("ProcessingCompleted", "Processing Completed. Processed: {0}, Failed: {1}.", "处理完成。已处理: {0}, 失败: {1}。");
            AddTranslation("DirectRename_FinishedLog", "Direct Rename Finished. Processed: {0}, Failed: {1}.", "直接重命名完成。已处理: {0}, 失败: {1}。");
            AddTranslation("AllToolsReadyComplete", "[Complete] All tools are ready.", "【完成】所有工具已就绪。");
            AddTranslation("CheckingTools", "Checking tools...", "正在检查工具...");
            AddTranslation("ZipsHelperNotFoundWarn", "[Warning] ZipsHelper or EnsureAllToolsReady not found. Skipping tool check.", "【警告】ZipsHelper 或 EnsureAllToolsReady 未找到。跳过工具检查。");
            AddTranslation("ToolCheckCancelled", "[Cancelled] Tool check operation cancelled.", "【取消】工具检查操作已取消。");
            AddTranslation("ToolCheckError", "[Tool Check Error] {0}", "【工具检查错误】{0}");
            AddTranslation("FormatErrorLog", "ERR Fmt Key '{0}': {1}. Base='{2}' Args='{3}'", "错误格式化键'{0}':{1}.基础='{2}'参数='{3}'");
            AddTranslation("RenamedDefaultPrefix", "Renamed", "已重命名");
            AddTranslation("BackupDefaultPrefix", "Backup", "备份");
            AddTranslation("NoStackTrace", "No stack trace available", "无可用堆栈跟踪");
            AddTranslation("NoSpecificError", "(No specific error message)", "(无具体错误信息)");
            AddTranslation("CustomPathReverting", "Reverting to default backup logic.", "恢复为默认备份逻辑。");
            AddTranslation("RetryAttempt", "Retry", "重试");
            AddTranslation("DropProcessingStart", "--- Processing dropped items", "--- 处理拖放的项目");
            AddTranslation("DropAddedItems", "Added {0} new valid items from drop.", "从拖放中添加了 {0} 个新的有效项目。");
            AddTranslation("DropNoNewItems", "No new valid items added from drop (duplicates or invalid).", "未从拖放中添加新的有效项目 (重复或无效)。");
            AddTranslation("IgnoringInvalidPath", "Ignoring invalid or non-existent path: {0}", "忽略无效或不存在的路径: {0}");
            AddTranslation("IgnoringDuplicateItem", "Ignoring duplicate item: {0}", "忽略重复项目: {0}");
            AddTranslation("AddedFolder", "Added Folder: {0}", "已添加文件夹: {0}");
            AddTranslation("AddedImageFile", "Added Image: {0}", "已添加图片: {0}");
            AddTranslation("AddedNonImageFile", "Added File: {0}", "已添加文件: {0}");
            AddTranslation("ErrorAddingPath", "Error: Failed to add path '{0}': {1}", "错误: 添加路径 '{0}' 失败: {1}");
            AddTranslation("TextBoxPathSelected", "Path selected via text box: {0}", "通过文本框选择路径: {0}");
            AddTranslation("TextBoxCleared", "Input cleared.", "输入已清除。");
            AddTranslation("ErrorProcessingTextBoxPath", "Error: Processing path from text box '{0}' failed: {1}", "错误: 处理文本框路径 '{0}' 失败: {1}");
            AddTranslation("FolderSelectionComplete", "Added {0} folder(s).", "添加了 {0} 个文件夹。");
            AddTranslation("FolderSelectionCancelled", "Folder selection cancelled.", "文件夹选择已取消。");
            AddTranslation("ImageSelectionStart", "--- Image Selection ---", "--- 图片选择 ---");
            AddTranslation("ImageSelectionComplete", "Added {0} file(s).", "添加了 {0} 个文件。");
            AddTranslation("ImageSelectionCancelled", "Image selection cancelled.", "图片选择已取消。");
            AddTranslation("SetInitialPathError", "Error setting initial path: {0}", "设置初始路径时出错: {0}");
            AddTranslation("NoFilesSelected", "No folder/images selected.", "未选择文件夹/图片。");
            AddTranslation("CustomPathInvalid", "Custom path invalid: {0}.", "自定义路径无效: {0}。");
            AddTranslation("CustomPathValid", "Using custom path: {0}", "使用自定义路径: {0}");
            AddTranslation("CustomPathVerifyError", "Error verifying custom path '{0}': {1}", "验证自定义路径“{0}”时出错：{1}");
            AddTranslation("CustomPathEmptyWarning", "Warn: Custom path empty. Using default.", "警告:自定义路径为空，使用默认。");
            AddTranslation("CustomOutputPathInvalid", "Custom output path invalid: {0}.", "自定义输出路径无效: {0}。");
            AddTranslation("CustomOutputPathValid", "Using custom output path: {0}", "使用自定义输出路径: {0}");
            AddTranslation("CustomOutputPathVerifyError", "Error verifying custom path '{0}': {1}", "验证自定义路径“{0}”时出错：{1}");
            AddTranslation("CustomOutputPathEmptyWarning", "Warning: Custom output path is empty. Using default location.", "警告：自定义输出路径为空。将使用默认位置。");
            AddTranslation("CustomPathCreateAttempt", "Creating custom backup dir: {0}", "创建自定义备份:{0}");
            AddTranslation("CustomOutputPathCreateAttempt", "Attempting to create custom output directory: {0}", "尝试创建自定义输出目录: {0}");
            AddTranslation("ErrorCreatingCustomBackupDir", "ERROR creating custom backup dir '{0}': {1}", "错误：创建自定义备份:{0}失败：{1}");
            AddTranslation("ErrorCreatingCustomOutputDir", "ERROR creating custom output directory '{0}': {1}", "错误：创建自定义输出目录 '{0}' 时出错：{1}");
            AddTranslation("DirectRename_StartLog", "--- Direct Rename Start ---", "--- 开始直接重命名 ---");
            AddTranslation("DirectRename_OptionFolder", "Mode: Folders Only", "模式: 仅文件夹");
            AddTranslation("DirectRename_OptionFolderWithPrefix", "Mode: Folders Only (Prefix: '{0}')", "模式: 仅文件夹(前缀:'{0}')");
            AddTranslation("DirectRename_OptionFile", "Mode: Files Only (Prefix: '{0}')", "模式: 仅文件(前缀:'{0}')");
            AddTranslation("DirectRename_DefaultFilePrefixInfo", "Info: Using default file prefix '{0}'.", "提示:使用默认文件前缀'{0}'。");
            AddTranslation("DirectRename_FolderPrefixEmptyInfo", "Info: Folder prefix empty.", "提示:文件夹前缀为空。");
            AddTranslation("ExifMode_StartLog", "--- EXIF Clean/Rename Start ---", "--- 开始EXIF清理/重命名 ---");
            AddTranslation("ExifMode_BackupEnabled", "Backup: Enabled (Default Path Logic)", "备份:启用(默认路径逻辑)");
            AddTranslation("ExifMode_BackupEnabledCustom", "Backup: Enabled (Custom Path: '{0}')", "备份:启用(自定义路径:'{0}')");
            AddTranslation("ExifMode_BackupDisabled", "Backup: Disabled", "备份:禁用");
            AddTranslation("ProcessingReady", "Processing ready...", "准备处理...");
            AddTranslation("ProcessingCancelled", "Processing cancelled.", "处理已取消。");
            AddTranslation("DirectRename_FatalError", "FATAL RENAME ERROR: {0}\n{1}", "严重重命名错误:{0}\n{1}");
            AddTranslation("FatalProcessingError", "FATAL PROCESSING ERROR: {0}\n{1}", "严重处理错误:{0}\n{1}");
            AddTranslation("NoImagesFound", "No supported images found.", "未找到支持的图片。");
            AddTranslation("OpenFolderComplete", "Opening folder: {0}", "正在打开: {0}");
            AddTranslation("OpenFolderFailed", "Could not open folder '{0}': {1}", "无法打开'{0}': {1}");
            AddTranslation("OpenFolderFallback", "Target invalid, opening fallback: {1}", "目标无效,打开备用:{1}");
            AddTranslation("OpenFolderFallbackFailed", "Target & fallback invalid: {0}", "目标和备用均无效:{0}");
            AddTranslation("CollectingFiles", "Collecting files...", "收集文件...");
            AddTranslation("ScanningFolder", "Scanning: {0}", "扫描中: {0}");
            AddTranslation("WarningScanningFolder", "Warn scan '{0}': {1}-{2}", "警告扫描'{0}': {1}-{2}");
            AddTranslation("CollectionComplete", "Found {0} files.", "找到 {0} 文件。");
            AddTranslation("StartingProcessing", "Processing {0} items...", "处理 {0} 项目...");
            AddTranslation("ExifToolNotFound", "ExifTool not found: {0}. Check 'exiftool'.", "找不到ExifTool:{0}。检查'exiftool'。");
            AddTranslation("ImageMagickNotFound", "ImageMagick not found: {0}. Check 'ImageMagick'.", "找不到 ImageMagick: {0}。检查 'ImageMagick' 子目录。");
            AddTranslation("FFprobeNotFound", "Error: ffprobe.exe not found. Ensure ffmpeg tools are in 'ffmpeg/bin'.", "错误：找不到 ffprobe.exe。请确保 ffmpeg 工具位于 'ffmpeg/bin' 子目录中。");
            AddTranslation("FFmpegNotFound", "Error: ffmpeg.exe not found. Ensure ffmpeg tools are in 'ffmpeg/bin'.", "错误：找不到 ffmpeg.exe。请确保 ffmpeg 工具位于 'ffmpeg/bin' 子目录中。");
            AddTranslation("ProcessingFile", "Processing(B): {0}", "处理中(备):{0}");
            AddTranslation("ProcessingFileNoBackup", "Processing: {0}", "处理中:{0}");
            AddTranslation("ErrorDeterminingDirectory", "Cannot get directory for: {0}", "无法获取目录:{0}");
            AddTranslation("BackupFolderExists", "Warn: Backup target path exists: {0}. Cannot move source here.", "警告:备份目标路径已存在:{0}。无法移动源到此处。");
            AddTranslation("MovingFolderToBackup", "Moving folder to backup: '{0}' -> '{1}'", "移动文件夹到备份:'{0}'->'{1}'");
            AddTranslation("ErrorMovingFolder", "ERROR moving folder '{0}'->'{1}': {2}", "错误移动文件夹'{0}'->'{1}':{2}");
            AddTranslation("BackupFolderExpectedNotFound", "ERR: Expected backup folder not found: {0}. Skip '{1}'.", "错误:预期备份文件夹未找到:{0}.跳过'{1}'.");
            AddTranslation("BackupFileNotFound", "ERR: File not found in backup: {0}. Skip.", "错误:备份中无文件:{0}.跳过.");
            AddTranslation("CreatingBackupDirectory", "Creating file backup dir: {0}", "创建文件备份目录:{0}");
            AddTranslation("FileNotFoundBackup", "ERR: Original file not found for backup: {0}. Skip.", "错误:找不到原文件备份:{0}.跳过.");
            AddTranslation("MovingFileToBackup", "Moving file to backup: '{0}' -> '{1}'", "移动文件到备份:'{0}'->'{1}'");
            AddTranslation("ErrorMovingFile", "ERROR moving file '{0}'->'{1}': {2}. Skip.", "错误移动文件'{0}'->'{1}':{2}.跳过.");
            AddTranslation("ErrorCreatingOutputFolder", "ERROR creating output folder '{0}': {1}. Skip.", "错误创建输出'{0}':{1}.跳过.");
            AddTranslation("ExifToolSourceNotFound", "ERR: Source not found for tool: {0}.", "错误:工具源未找到:{0}。");
            AddTranslation("SuccessRename", "OK: Cleaned '{0}'->'{1}'", "成功:已清理'{0}'->'{1}'");
            AddTranslation("SuccessProcessed", "OK: Processed '{0}' -> '{1}'", "成功: 已处理 '{0}' -> '{1}'");
            AddTranslation("DeletingOriginalAfterSuccess", "Deleting original (no backup/in-place): {0}", "删除原文件(无备份/原地):{0}");
            AddTranslation("ErrorDeletingOriginal", "ERR delete original '{0}': {1}", "错误删除原文件'{0}':{1}");
            AddTranslation("ExifToolFailed", "FAIL ExifTool(Code {1}) for '{0}'. Err: {2}.", "失败 ExifTool(代码 {1})于'{0}'.错:{2}.");
            AddTranslation("ImageMagickFailed", "FAIL ImageMagick(Code {1}) for '{0}'. Err: {2}.", "失败 ImageMagick(代码 {1})于'{0}'.错:{2}.");
            AddTranslation("UnexpectedErrorProcessingFile", "UNEXPECTED ERR process '{0}': {1}-{2}.", "意外错误处理'{0}':{1}-{2}.");
            AddTranslation("ProcessedCounts", "Done: {0}, Fail: {1}, Total: {2}", "完成:{0},失败:{1},总计:{2}");
            AddTranslation("ProgressCounts", "Prog: {0}/{1}", "进度:{0}/{1}");
            AddTranslation("ErrorMatchingInputPath", "Warn match path '{0}' vs '{1}': {2}", "警告匹配'{0}'和'{1}':{2}");
            AddTranslation("ErrorNoInputContext", "ERR no input context for '{0}'.", "错误:无'{0}'上下文");
            AddTranslation("ErrorCheckingFolderPath", "ERR check path '{0}': {1}", "错误检查路径'{0}':{1}");
            AddTranslation("ClearLogMessage", "Log Cleared.", "日志已清除。");
            AddTranslation("LogSaved", "Log saved: {0}", "日志已保存:{0}");
            AddTranslation("ErrorSavingLog", "ERR save log: {0}", "错误保存日志:{0}");
            AddTranslation("CancelRequested", "Cancel requested...", "请求取消...");
            AddTranslation("DirectRename_Preparing", "Direct Rename: Prepare...", "直接重命名:准备...");
            AddTranslation("DirectRename_FoundFolders", "Direct Rename: Found {0} folders.", "直接重命名:找到{0}文件夹。");
            AddTranslation("DirectRename_FoundFiles", "Direct Rename: Found {0} files.", "直接重命名:找到{0}文件。");
            AddTranslation("DirectRename_StartFolders", "Direct Rename: Renaming folders...", "直接重命名:重命名文件夹...");
            AddTranslation("DirectRename_FolderStatus", "Folder: {0} ({1}/{2})", "文件夹:{0}({1}/{2})");
            AddTranslation("DirectRename_FolderNotFound", "ERR Rename: Folder not found: {0} (At {1})", "错误重命名:文件夹未找到:{0}(在{1})");
            AddTranslation("DirectRename_ParentError", "ERR Rename: Cannot get parent dir for '{0}'.", "错误重命名:无法获取'{0}'父目录");
            AddTranslation("DirectRename_AttemptFolder", "Rename Folder: '{0}' -> '{1}'", "重命名文件夹:'{0}'->'{1}'");
            AddTranslation("DirectRename_FolderSuccess", "OK Rename Folder: '{0}' -> '{1}'", "成功重命名文件夹:'{0}'->'{1}'");
            AddTranslation("DirectRename_SubDirFindError", "Warn find subdirs in '{0}': {1}", "警告查找子目录于'{0}':{1}");
            AddTranslation("DirectRename_AccessDeniedWarning", "Warn Rename Folder: Access denied '{0}'.", "警告重命名文件夹:访问被拒'{0}'.");
            AddTranslation("DirectRename_RetryPromptMessage", "Cannot rename:\n'{0}'\n\nIn use? Close Explorer/Program and Retry.", "无法重命名:\n'{0}'\n\n可能被占用?请关闭资源管理器/程序后重试。");
            AddTranslation("DirectRename_RetryPromptButton", "Retry", "重试");
            AddTranslation("DirectRename_RetryLog", "User Retry. Retry in {0}ms...", "用户重试。{0}ms后重试...");
            AddTranslation("DirectRename_UserCancelledRetry", "User cancelled rename for '{0}'.", "用户取消重命名'{0}'.");
            AddTranslation("DirectRename_MaxRetriesReached", "FAIL Rename Folder: Access denied '{0}' (max retries).", "失败重命名文件夹:访问被拒'{0}'(已达上限).");
            AddTranslation("DirectRename_ErrorFolder", "ERR Rename Folder '{0}' to '{1}': {2}", "错误重命名文件夹'{0}'到'{1}':{2}");
            AddTranslation("DirectRename_FolderComplete", "Direct Rename: Folders done.", "直接重命名:文件夹完成.");
            AddTranslation("DirectRename_StartFiles", "Direct Rename: Renaming files...", "直接重命名:重命名文件...");
            AddTranslation("DirectRename_FileStatus", "File: {0} ({1}/{2})", "文件:{0}({1}/{2})");
            AddTranslation("DirectRename_FileNotFound", "ERR Rename: File not found: {0}", "错误重命名:文件未找到:{0}");
            AddTranslation("DirectRename_FileDirError", "ERR Rename: Dir '{1}' for file '{0}' not exist. Skip.", "错误重命名:文件'{0}'目录'{1}'不存在.跳过.");
            AddTranslation("DirectRename_AttemptFile", "Rename File: '{0}' -> '{1}'", "重命名文件:'{0}'->'{1}'");
            AddTranslation("DirectRename_FileSuccess", "OK Rename File: '{0}' -> '{1}'", "成功重命名文件:'{0}'->'{1}'");
            AddTranslation("DirectRename_FileAccessDenied", "FAIL Rename File: Access denied '{0}'.", "失败重命名文件:访问被拒'{0}'.");
            AddTranslation("DirectRename_ErrorFile", "ERR Rename File '{0}' to '{1}': {2}", "错误重命名文件'{0}'到'{1}':{2}");
            AddTranslation("DirectRename_FileComplete", "Direct Rename: Files done.", "直接重命名:文件完成.");
            AddTranslation("DirectRename_NothingSelected", "Direct Rename: No items selected for mode.", "直接重命名:无项目被选中.");
            AddTranslation("RelativePathError", "Warn calc relative path '{0}' (parent '{1}'->'{2}'): {3}", "警告计算相对路径'{0}'(父'{1}'->'{2}'):{3}");
            AddTranslation("MissingKeyLog", "Missing Key '{0}' in lang '{1}'", "语言'{1}'缺失键'{0}'");
            AddTranslation("SaveLogDialogError", "Log unavailable.", "日志不可用.");
            AddTranslation("ZoompanSettingsUpdatedMsg", "Effect settings updated.", "效果设置已更新。");
            AddTranslation("ZoompanGenerationComplete", "Video/Animation generation complete. Success: {0}, Fail: {1}.", "视频/动画生成完成。成功: {0}, 失败: {1}.");
            AddTranslation("SelectionStartedLog", "Starting to add selection...", "开始添加选择...");
            AddTranslation("SelectionCompleteLog", "Selection complete. Added {0}.", "选择完成。已添加 {0}。");
            AddTranslation("NoValidItemsSelectedLog", "No valid items were selected or added.", "未选择或添加任何有效项目。");
            AddTranslation("NoValidItemsAddedLog", "No valid items were added (invalid paths, permissions?).", "未添加任何有效的文件或文件夹（可能由于无效路径或权限问题）。");
            AddTranslation("SelectionCancelled", "Selection cancelled.", "选择已取消。");
            AddTranslation("StartingZoompanGeneration", "Starting Video/Animation generation for {0} items...", "开始为 {0} 个项目生成 视频/动画 ...");
            AddTranslation("ErrorGeneratingZoompan", "Error during Video/Animation generation: {0}\n{1}", "生成 视频/动画 时发生错误: {0}\n{1}");
            AddTranslation("ZoompanStatusProcessing", "Processing (Video/Anim): {0} ({1}/{2})", "处理中 (视频/动画): {0} ({1}/{2})");
            AddTranslation("ErrorGettingResolution", "Failed to get resolution for '{0}', skipping. [{1}/{2}]", "无法获取 '{0}' 的分辨率，跳过。 [{1}/{2}]");
            AddTranslation("SuccessZoompan", "OK (Video/Anim): '{0}' -> '{1}' (Took: {2:F2}s)", "成功 (视频/动画): '{0}' -> '{1}' (耗时: {2:F2}s)");
            AddTranslation("FailedZoompan", "FAIL (Video/Anim): '{0}' (Code {1}). Err: {2} (Took: {3:F2}s)", "失败 (视频/动画): '{0}' (Code {1}). Err: {2} (耗时: {3:F2}s)");
            AddTranslation("BurstModeLabel", "Burst Mode (Images to Video/GIF)", "连拍模式 (多图转单视频/GIF)");
            AddTranslation("OutputFormatLabel", "Output Format:", "输出格式:");
            AddTranslation("OutputFormatMOV", "MOV (H.265)", "MOV (H.265)");
            AddTranslation("OutputFormatMP4", "MP4 (H.264)", "MP4 (H.264)");
            AddTranslation("OutputFormatGIF", "GIF", "GIF");
            AddTranslation("BurstModeWarning", "Burst Mode requires selecting a single folder containing only images.", "连拍模式需要选择一个仅包含图片的文件夹。");
            AddTranslation("BurstModeNoImages", "No images found in the selected folder for Burst Mode.", "在所选文件夹中未找到用于连拍模式的图片。");
            AddTranslation("BurstModeSingleFile", "Error: Burst Mode cannot process single file selections.", "错误：连拍模式无法处理单个文件选择。");
            AddTranslation("StartingBurstGeneration", "Starting Burst Mode generation for folder '{0}'...", "开始为文件夹 '{0}' 生成连拍模式文件...");
            AddTranslation("SuccessBurst", "OK (Burst): Folder '{0}' -> '{1}' (Took: {2:F2}s)", "成功 (连拍): 文件夹 '{0}' -> '{1}' (耗时: {2:F2}s)");
            AddTranslation("FailedBurst", "FAIL (Burst): Folder '{0}' (Code {1}). Err: {2} (Took: {3:F2}s)", "失败 (连拍): 文件夹 '{0}' (Code {1}). Err: {2} (耗时: {3:F2}s)");
            AddTranslation("GeneratingPalette", "Generating optimal GIF palette...", "正在生成最佳 GIF 调色板...");
            AddTranslation("EncodingGIF", "Encoding GIF using palette...", "正在使用调色板编码 GIF...");
            AddTranslation("PaletteGenFailed", "FAIL (Burst/GIF): Palette generation failed. Code {0}. Err: {1}", "失败 (连拍/GIF): 调色板生成失败。代码 {0}。错误: {1}");
            AddTranslation("GIFEncodingFailed", "FAIL (Burst/GIF): Final GIF encoding failed. Code {0}. Err: {1}", "失败 (连拍/GIF): 最终 GIF 编码失败。代码 {0}。错误: {1}");
            AddTranslation("BurstOutputLabel", "Output Filename (Burst Mode):", "输出文件名 (连拍模式):");
            AddTranslation("StatusBar_Start", "Start:", "开始:");
            AddTranslation("StatusBar_End", "End:", "结束:");
            AddTranslation("StatusBar_Elapsed", "Elapsed:", "耗时:");
            AddTranslation("StatusBar_Total", "Total:", "总计:");
            AddTranslation("StatusBar_Concurrent", "Concurrent:", "并发:");
            AddTranslation("Debug_TaskCancelledGeneric", "DEBUG: Task cancelled for {0}.", "调试：文件 {0} 的任务已取消。");
            AddTranslation("Debug_ParallelProcessingFinished", "DEBUG: Parallel processing finished. Processed: {0}, Failed: {1}", "调试：并行处理完成。成功: {0}, 失败: {1}");
            AddTranslation("Debug_WhenAllCaughtCancellation", "DEBUG: Task.WhenAll caught OperationCanceledException (processing cancelled).", "调试：Task.WhenAll 捕获到 OperationCanceledException (处理已取消)。");
            AddTranslation("Debug_WhenAllCaughtError", "ERROR: Unexpected error during Task.WhenAll: {0} - {1}", "错误：Task.WhenAll 期间发生意外错误: {0} - {1}");
            AddTranslation("Debug_WhenAllInnerError", "-- Inner Exception: {0} - {1}", "-- 内部异常: {0} - {1}");
            AddTranslation("EnableTimestampLabel", "Enable Timestamp", "启用时间戳");
            AddTranslation("EnableCounterLabel", "Enable Counter", "启用计数器");
            AddTranslation("TimestampFormatLabel", "Timestamp Format:", "时间戳格式:");
            AddTranslation("CounterStartValueLabel", "Counter Start Value:", "计数器起始值:");
            AddTranslation("CounterFormatLabel", "Counter Format:", "计数器格式:");
            AddTranslation("WarnInvalidTimestampFormat", "Warning: Invalid timestamp format '{0}'. Reverting to default '{1}'.", "警告：无效的时间戳格式 '{0}'。将恢复为默认格式 '{1}'。");
            AddTranslation("WarnProblematicTimestampFormat", "Warning: Problematic timestamp format '{0}': {1}. Reverting to default '{2}'.", "警告：时间戳格式 '{0}' 可能存在问题：{1}。将恢复为默认格式 '{2}'。");
            AddTranslation("WarnTimestampFormatProducesEmpty", "Warning: Timestamp format '{0}' resulted in an empty string. Using default.", "警告：时间戳格式 '{0}' 产生了空字符串。将使用默认格式。");
            AddTranslation("WarnTimestampFormatProducesInvalid", "Warning: Timestamp format '{0}' produced output '{1}' with invalid characters or ending. Using default.", "警告：时间戳格式“{0}”产生的输出“{1}”包含无效字符或以点/空格结尾。将使用默认格式。");
            AddTranslation("WarnTimestampFormatProducesInvalidFinal", "Warning: Timestamp format '{0}' produced invalid output '{1}'. Default format '{2}' also problematic? Using default.", "警告：时间戳格式 '{0}' 产生了无效输出 '{1}'。默认格式 '{2}' 也存在问题？将使用默认格式。");
            AddTranslation("WarnTimestampFormatInvalidChars", "Warning: Timestamp format '{0}' contains potentially invalid characters for filenames/folders or ends with dot/space. Using default.", "警告：时间戳格式 '{0}' 包含对文件名/文件夹无效的字符，或以点/空格结尾。将使用默认格式。");
            AddTranslation("WarnInvalidCounterFormat", "Warning: Invalid counter format '{0}'. Using default '{1}'.", "警告：无效的计数器格式 '{0}'。将使用默认格式 '{1}'。");
            AddTranslation("WarnInvalidCounterStartValue", "Warning: Invalid counter start value '{0}'. Using default '{1}'.", "警告：无效的计数器起始值 '{0}'。将使用默认值 '{1}'。");
            AddTranslation("WarnCounterFormatProducesEmpty", "Warning: Counter format '{0}' resulted in an empty string. Using default.", "警告：计数器格式 '{0}' 产生了空字符串。将使用默认格式。");
            AddTranslation("WarnCounterFormatProducesEmptyFinal", "Warning: Counter format '{0}' produced empty output. Default format '{1}' also problematic? Using simple string.", "警告：计数器格式 '{0}' 产生了空输出。默认格式 '{1}' 也存在问题？将使用简单字符串。");

            AddTranslation("ErrorGeneratingTimestamp", "Error generating timestamp with format '{0}': {1}. Using default.", "使用格式 '{0}' 生成时间戳时出错：{1}。将使用默认格式。");
            AddTranslation("ErrorGeneratingTimestampFolder", "Error generating timestamp folder name with format '{0}': {1}. Using default.", "使用格式 '{0}' 生成时间戳文件夹名称时出错：{1}。将使用默认格式。");
            AddTranslation("WarnTimestampFormatInvalidFolder", "Warning: Timestamp format '{0}' produced invalid folder name '{1}'. Using default.", "警告：时间戳格式 '{0}' 生成了无效的文件夹名称 '{1}'。将使用默认格式。");
            AddTranslation("ErrorFormattingCounter", "Error formatting counter '{0}' with format '{1}'. Using default.", "使用格式 '{1}' 格式化计数器 '{0}' 时出错。将使用默认格式。");
            AddTranslation("UseCustomImageOutputPathLabel", "Custom Image Output Path", "自定义图像输出路径");
            AddTranslation("UseCustomVideoOutputPathLabel", "Custom Video Output Path", "自定义视频输出路径");
            AddTranslation("SelectCustomImageOutputPathLabel", "Browse Image Output Folder", "浏览图像输出文件夹");
            AddTranslation("SelectCustomVideoOutputPathLabel", "Browse Video Output Folder", "浏览视频输出文件夹");
            AddTranslation("StartingBackup", "Starting backup pre-processing...", "开始备份预处理...");
            AddTranslation("PerformingBackup", "Performing backup...", "正在执行备份...");
            AddTranslation("BackupFailedAbort", "ERROR: Backup pre-processing failed. Aborting operation.", "错误：备份预处理失败。中止操作。");
            AddTranslation("BackupComplete", "Backup pre-processing completed.", "备份预处理完成。");
            AddTranslation("NoSourcesForBackup", "No valid sources found for backup.", "未找到用于备份的有效源。");
            AddTranslation("BackupErrorCreateBase", "ERROR: Failed to create custom backup base directory '{0}': {1}", "错误：创建自定义备份基础目录“{0}”失败：{1}");
            AddTranslation("BackupErrorCreateRoot", "ERROR: Failed to create backup root directory '{0}': {1}", "错误：创建备份根目录“{0}”失败：{1}");
            AddTranslation("BackupRootExists", "Warn: Backup target path exists: {0}. Cannot move source here.", "警告：备份目标路径已存在：{0}。无法移动源到此处。");
            AddTranslation("BackupErrorParentRoot", "Warning: Cannot determine default backup parent for root path '{0}'. Skipping backup for this item.", "警告：无法确定根路径“{0}”的默认备份父目录。跳过此项目的备份。");
            AddTranslation("BackupErrorRootMapping", "Warning: Could not find backup root mapping for '{0}'. Skipping backup.", "警告：找不到“{0}”的备份根目录映射。跳过备份。");
            AddTranslation("BackupErrorLogicFailed", "ERROR: Backup path logic failed for '{0}'. Skipping backup.", "错误：“{0}”的备份路径逻辑失败。跳过备份。");
            AddTranslation("BackupErrorCreateSubdir", "ERROR: Failed to create source sub-directory in backup '{0}': {1}", "错误：在备份“{0}”中创建源子目录失败：{1}");
            AddTranslation("BackupCopying", "Backing up '{0}' to '{1}'...", "正在备份“{0}”到“{1}”...");
            AddTranslation("BackupErrorCopyDir", "ERROR: Failed to recursively copy directory '{0}' to backup.", "错误：递归复制目录“{0}”到备份失败。");
            AddTranslation("BackupErrorCopyFile", "ERROR: Failed to copy file '{0}' to backup location '{1}': {2}", "错误：复制文件“{0}”到备份位置“{1}”失败：{2}");
            AddTranslation("BackupCancelled", "Backup operation cancelled.", "备份操作已取消。");
            AddTranslation("BackupFatalError", "FATAL ERROR during backup pre-processing: {0}", "备份预处理期间发生严重错误：{0}");
            AddTranslation("CreatedOutputDir", "Created output directory: {0}", "已创建输出目录：{0}");
            AddTranslation("WarnRelativePath", "Warning: Could not calculate relative path for '{0}' relative to '{1}': {2}.", "警告：无法计算“{0}”相对于“{1}”的相对路径：{2}。");
            AddTranslation("WarnRelativePathVideo", "Warning: Could not calculate relative path for video '{0}': {1}", "警告：无法计算视频“{0}”的相对路径：{1}");
            AddTranslation("ErrorUniqueFile", "Error: Could not generate unique filename for {0} in {1}. Skipping.", "错误：无法在 {1} 中为 {0} 生成唯一文件名。跳过。");
            AddTranslation("ErrorUniqueFolder", "Error: Could not find unique folder name for {0}. Skipping.", "错误：找不到 {0} 的唯一文件夹名称。跳过。");
            AddTranslation("ErrorUniqueVideo", "Error: Could not generate unique video filename for {0}. Skipping.", "错误：无法为 {0} 生成唯一的视频文件名。跳过。");
            AddTranslation("ErrorUniqueBurst", "Error: Could not generate unique burst filename for {0}. Skipping.", "错误：无法为 {0} 生成唯一的连拍文件名。跳过。");
            AddTranslation("ErrorUniqueAnimation", "Error: Could not generate unique animation filename for {0}. Skipping.", "错误：无法为 {0} 生成唯一的动画文件名。跳过。");
            AddTranslation("CopyDirPlaceholderLog", "Placeholder: Recursively copying '{0}' to '{1}'", "占位符：递归复制“{0}”到“{1}”");
            AddTranslation("CopyDirError", "ERROR during recursive copy '{0}' -> '{1}': {2}", "递归复制“{0}”->“{1}”时出错：{2}");
            AddTranslation("BackupStrategySingleRename", "Identified single folder input. Will rename '{0}' to '{1}'.", "检测到单个文件夹输入。将重命名“{0}”为“{1}”。");
            AddTranslation("BackupStrategyMultiFolderContainer", "Identified multiple folders from same parent '{0}'. Creating container '{1}' and moving folders into it.", "检测到来自同一父目录“{0}”的多个文件夹。正在创建容器“{1}”并将文件夹移入其中。");
            AddTranslation("BackupStrategyFallback", "Using standard recursive move backup logic for current input.", "对当前输入使用标准递归移动备份逻辑。");
            AddTranslation("BackupCreateContainer", "Creating container backup folder: {0}", "正在创建容器备份文件夹：{0}");
            AddTranslation("BackupMoveIntoContainer", "Moving '{0}' into '{1}'...", "正在移动“{0}”到“{1}”中...");
            AddTranslation("BackupItemExistsInContainer", "Warning: Item '{0}' already exists in backup container '{1}'. Skipping move for this item.", "警告：项目“{0}”已存在于备份容器“{1}”中。跳过此项目的移动。");
            AddTranslation("BackupMultiMoveComplete", "Multiple folders moved into backup container successfully.", "已成功将多个文件夹移至备份容器。");
            AddTranslation("BackupMultiMoveError", "ERROR: Failed during multi-folder backup to container '{0}': {1}", "错误：在多文件夹备份到容器“{0}”期间失败：{1}");
            AddTranslation("BackupAttemptRename", "Attempting to rename '{0}' to '{1}'...", "尝试重命名“{0}”为“{1}”...");
            AddTranslation("BackupRenameError", "ERROR: Failed to rename folder '{0}' to '{1}': {2}", "错误：重命名文件夹“{0}”为“{1}”失败：{2}");
            AddTranslation("BackupRenameSuccess", "Folder renamed successfully.", "文件夹重命名成功。");
            AddTranslation("BackupCannotGetParent", "ERROR: Cannot get parent directory for single folder '{0}'.", "错误：无法获取单个文件夹“{0}”的父目录。");
            AddTranslation("BackupMoveToCustom", "Moving items into custom backup folder: {0}", "正在将项目移动到自定义备份文件夹：{0}");
            AddTranslation("BackupMoveItemToCustom", "Moving '{0}' to '{1}'...", "正在移动“{0}”到“{1}”中...");
            AddTranslation("BackupItemExistsInCustom", "Warning: Item '{0}' already exists in custom backup target '{1}'. Skipping move.", "警告：项目“{0}”已存在于自定义备份目标“{1}”中。跳过移动。");
            AddTranslation("BackupMoveCustomComplete", "Items moved to custom backup location.", "项目已移动到自定义备份位置。");
            AddTranslation("BackupMoveCustomError", "ERROR: Failed moving items to custom backup location '{0}': {1}", "错误：将项目移动到自定义备份位置“{0}”失败：{1}");
            AddTranslation("BackupErrorNoStrategy", "ERROR: No backup strategy executed. This indicates a logic error.", "错误：未执行备份策略。这表示存在逻辑错误。");
            AddTranslation("BackupFileInBackupNotFound", "Warning: File '{0}' expected in backup location '{1}' but not found. Skipping.", "警告：预期文件“{0}”位于备份位置“{1}”，但未找到。跳过。");
            AddTranslation("BackupMapEntryNotFound", "Warning: Original input '{0}' not found in backup map. Skipping collection for this item.", "警告：在备份映射中找不到原始输入“{0}”。跳过此项目的收集。");
            AddTranslation("BackupMapEntrySourceNotFound", "Warning: Could not find backup map entry for source of '{0}'.", "警告：找不到“{0}”源的备份映射条目。");
            AddTranslation("BackupProcessingFromBackup", "Collecting files from backup location(s).", "正在从备份位置收集文件。");
            AddTranslation("BackupProcessingOriginals", "Collecting files from original location(s).", "正在从原始位置收集文件。");
            AddTranslation("SourceNotFoundForMove", "ERROR: Source {0} '{1}' not found for move.", "错误：用于移动的源 {0} “{1}”未找到。");
            AddTranslation("AccessDeniedMoveBackup", "ERROR: Access denied moving '{0}' to backup: {1}", "错误：移动“{0}”到备份时访问被拒绝：{1}");
            AddTranslation("ErrorMoveBackupGeneral", "ERROR: Failed to move '{0}' to backup location '{1}': {2}", "错误：将“{0}”移动到备份位置“{1}”失败：{2}");
            AddTranslation("ErrorCreatingBackupParent", "ERROR: Failed creating parent of backup target '{0}': {1}", "错误：创建备份目标“{0}”的父目录失败：{1}");
            AddTranslation("ErrorCreatingNestedBackupRoot", "ERROR: Failed creating nested backup root '{0}': {1}", "错误：创建嵌套备份根“{0}”失败：{1}");
            AddTranslation("ParamsResetMsg", "Parameters have been reset to the last saved state or default values.", "参数已重置为上次保存的状态或默认值。");
            AddTranslation("ParamsResetTitle", "Parameters Reset", "参数重置");
            AddTranslation("LanguageChanged", "Language changed.", "语言已更改。");
            AddTranslation("ToolNotFoundTitle", "Tool Not Found", "未找到工具");
            AddTranslation("SuccessAnimation", "OK (Animation): '{0}' -> '{1}' (Took: {2:F2}s)", "成功 (动画): '{0}' -> '{1}' (耗时: {2:F2}s)");
            AddTranslation("FailedAnimation", "FAIL (Animation): '{0}' (Code {1}). Err: {2} (Took: {3:F2}s)", "失败 (动画): '{0}' (Code {1}). Err: {2} (耗时: {3:F2}s)");
            AddTranslation("PresetSavedMsg", "Preset saved: {0}", "预设已保存: {0}");
            AddTranslation("PresetDeletedMsg", "Preset deleted: {0}", "预设已删除: {0}");
            AddTranslation("PresetOverwriteConfirm", "A preset named '{0}' already exists.\nDo you want to overwrite it?", "名为“{0}”的预设已存在。\n要覆盖它吗？");
            AddTranslation("PresetOverwriteTitle", "Overwrite Confirmation", "覆盖确认");
            AddTranslation("SelectPresetToDeleteMsg", "Please select a custom preset from the list on the right to delete.", "请先从右侧列表中选择一个要删除的自定义预设。");
            AddTranslation("SelectPresetToDeleteTitle", "No Preset Selected", "未选择预设");
            AddTranslation("NewPresetDefaultName", "New Preset Name", "新预设名称");
            AddTranslation("DeletedPresetWasActiveMsg", "The deleted preset was the active expression. Switched back to default preset.", "已删除的预设是当前活动的表达式。已恢复为默认预设。");
            AddTranslation("ConstructNameHelper", "Helper: Constructing name for '{0}' with counter {1}", "辅助: 为 '{0}' 构建名称 (计数器 {1})");
            AddTranslation("MsgConfirmCreateRoot", "根目录 '{0}' 不存在。\n是否要创建它？", "根目录“{0}”不存在。\n是否要创建它？");
            AddTranslation("TitleConfirmCreateRoot", "确认创建根目录", "确认创建根目录");
            AddTranslation("ErrorCreatingRoot", "创建根目录失败：\n{0}", "创建根目录失败：\n{0}");
            AddTranslation("ErrorValidBasePathRequired", "请输入有效的根目录路径！", "请输入有效的根目录路径！");
            AddTranslation("ErrorBasePathInvalid", "(根目录路径格式无效)", "(根目录路径格式无效)");
            AddTranslation("PlaceholderEnterValidBasePath", "(示例根目录 - 请选择实际路径)", "(示例根目录 - 请选择实际路径)");
            AddTranslation("SuccessGeneration", "成功生成 {0} 个主目录和 {1} 个子目录。", "成功生成 {0} 个主目录和 {1} 个子目录。");
            AddTranslation("SuccessTitle", "成功", "成功");
            AddTranslation("ErrorAuthGeneration", "创建目录时权限不足：{0}", "创建目录时权限不足：{0}");
            AddTranslation("ErrorAuthTitle", "权限错误", "权限错误");
            AddTranslation("ErrorIOGeneration", "创建目录时发生IO错误：{0}", "创建目录时发生IO错误：{0}");
            AddTranslation("ErrorIOTitle", "IO错误", "IO错误");
            AddTranslation("ErrorUnknownGeneration", "发生未知错误：{0}", "发生未知错误：{0}");
            AddTranslation("Nav_创建目录", "Create Directory", "创建目录");
            AddTranslation("Nav_直接命名", "Direct Rename", "直接命名");
            AddTranslation("Nav_文件处理", "File Processing", "文件处理");
            AddTranslation("Nav_EXIF 移除", "EXIF Remove", "EXIF 移除");
            AddTranslation("Nav_EXIF 写入", "EXIF Write", "EXIF 写入");
            AddTranslation("Nav_媒体生成", "Media Generation", "媒体生成");
            AddTranslation("Nav_生成视频", "Generate Video", "生成视频");
            AddTranslation("Nav_生成连拍", "Generate Burst", "生成连拍");
            AddTranslation("Nav_生成动画", "Generate Animation", "生成动画");
            AddTranslation("ToolCheckErrorTitle", "Tool Check Error", "工具检查错误");
            AddTranslation("LoadErrorTitle", "Load Error", "加载错误");
            AddTranslation("StartingAnimationGeneration", "Starting Animation generation...", "开始生成动画...");

            _isInitialized = true;
        }

        private static void AddTranslation(string key, string en, string zh)
        {
            if (!_translations.TryAdd(key, new Dictionary<string, string> { { "en", en }, { "zh", zh } }))
            {
                Debug.WriteLine($"Warning: Duplicate translation key '{key}'.");
            }
        }

        public static void SetLanguage(string langCode)
        {
            if (_currentLanguage.Equals(langCode, StringComparison.OrdinalIgnoreCase)) return;

            _currentLanguage = langCode;
            SetCurrentCulture(_currentLanguage);
        }

        private static void SetCurrentCulture(string langCode)
        {
            try
            {
                CultureInfo culture = new CultureInfo(langCode);
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

            }
            catch (CultureNotFoundException ex)
            {
                Debug.WriteLine($"Culture not found for '{langCode}': {ex.Message}. Defaulting to 'zh'.");
                _currentLanguage = "zh";
                SetCurrentCulture(_currentLanguage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting culture '{langCode}': {ex.Message}. Defaulting to 'zh'.");
                _currentLanguage = "zh";
                SetCurrentCulture(_currentLanguage);
            }
        }

        public static string GetLocalizedString(string key, string? fallback = null)
        {
            if (!_isInitialized)
            {
                Debug.WriteLine($"Warning: LocalizationHelper not initialized. Key '{key}' requested.");
                return fallback ?? $"<{key}>";
            }

            if (_translations.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(_currentLanguage, out var translation) && !string.IsNullOrEmpty(translation))
                {
                    return translation;
                }

                if (langDict.TryGetValue("en", out var enTranslation) && !string.IsNullOrEmpty(enTranslation))
                {
                    Debug.WriteLine($"Missing '{_currentLanguage}' translation for key '{key}'. Using 'en'.");
                    return enTranslation;
                }

                if (langDict.TryGetValue("zh", out var zhTranslation) && !string.IsNullOrEmpty(zhTranslation))
                {
                    Debug.WriteLine($"Missing '{_currentLanguage}' and 'en' translation for key '{key}'. Using 'zh'.");
                    return zhTranslation;
                }
            }

            Debug.WriteLine($"Missing translation key '{key}' for lang '{_currentLanguage}'.");
            return fallback ?? $"<{key}>";
        }

        public static string GetLocalizedString(string key, params object?[]? args)
        {
            string baseStr = GetLocalizedString(key);

            if (args is { Length: > 0 } && baseStr.Contains('{') && !baseStr.StartsWith("<"))
            {
                try
                {
                    return string.Format(CultureInfo.CurrentUICulture, baseStr, args.Select(a => a ?? string.Empty).ToArray());
                }
                catch (FormatException ex)
                {
                    string argStr = string.Join(",", args.Select(a => a?.ToString() ?? "null"));
                    Debug.WriteLine($"Format Error for key '{key}': {ex.Message}. Base='{baseStr}' Args='{argStr}'");
                    return $"{baseStr}(FMT_ERR)";
                }
                catch (Exception ex)
                {
                    string argStr = string.Join(",", args.Select(a => a?.ToString() ?? "null"));
                    Debug.WriteLine($"Unexpected Error formatting key '{key}': {ex.Message}. Base='{baseStr}' Args='{argStr}'");
                    return $"{baseStr}(FMT_ERR)";
                }
            }

            return baseStr;
        }

        public static string GetCurrentLanguage() => _currentLanguage;

        public static List<LanguageItem> GetAvailableLanguages()
        {
            return new List<LanguageItem>
             {
                 new LanguageItem("en", "English"),
                 new LanguageItem("zh", "中文")
             };
        }
    }
}