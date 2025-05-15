// Core/MediaGenerator.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.Core
{
    public class MediaGenerator
    {
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly Random _random = new Random();

        public string FFmpegPath => _ffmpegPath;
        public string FFprobePath => _ffprobePath;

        public MediaGenerator(string ffmpegPath, string ffprobePath)
        {
            _ffmpegPath = ffmpegPath;
            _ffprobePath = ffprobePath;
        }

        public record MediaResult(bool Success, string? OutputFilePath = null, string? ErrorMessage = null, double DurationSeconds = 0, int ExitCode = -1);

        public async Task<MediaResult> GenerateVideoFromImageAsync(
            string sourceImagePath,
            string outputFilePath,
            ZoompanSettings settings,
            string outputResolutionTag,
            OutputFormat format,
            IProgress<string>? statusProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath))
                return new MediaResult(false, ErrorMessage: "FFmpeg executable not found.", ExitCode: -1);
            if (string.IsNullOrWhiteSpace(_ffprobePath) || !File.Exists(_ffprobePath))
                return new MediaResult(false, ErrorMessage: "FFprobe executable not found.", ExitCode: -1);

            statusProgress?.Report($"获取 '{Path.GetFileName(sourceImagePath)}' 分辨率...");
            Stopwatch stopwatch = Stopwatch.StartNew();
            string tempPalettePath = "";

            try
            {
                if (!File.Exists(sourceImagePath)) return new MediaResult(false, ErrorMessage: $"Source image not found: {sourceImagePath}");
                string? destDir = Path.GetDirectoryName(outputFilePath);
                if (destDir != null && !Directory.Exists(destDir))
                {
                    try { Directory.CreateDirectory(destDir); }
                    catch (Exception dirEx) { return new MediaResult(false, ErrorMessage: $"Failed to create output directory '{destDir}': {dirEx.Message}"); }
                }

                string? sourceResolution = await GetImageResolutionAsync(sourceImagePath, cancellationToken);
                if (string.IsNullOrEmpty(sourceResolution)) return new MediaResult(false, ErrorMessage: $"无法获取 '{Path.GetFileName(sourceImagePath)}' 的分辨率。");
                statusProgress?.Report($"源分辨率: {sourceResolution}");

                string[] sourceDims = sourceResolution.Split('x');
                if (sourceDims.Length != 2 || !int.TryParse(sourceDims[0], out int srcWidth) || !int.TryParse(sourceDims[1], out int srcHeight) || srcWidth <= 0 || srcHeight <= 0)
                    return new MediaResult(false, ErrorMessage: $"解析源分辨率失败: '{sourceResolution}'");

                string outputResolution = outputResolutionTag == "source" ? sourceResolution : outputResolutionTag;
                ZoompanEffectType effectToApply = settings.EffectType;
                if (effectToApply == ZoompanEffectType.RandomPreset)
                {
                    var availablePresets = Enum.GetValues(typeof(ZoompanEffectType)).Cast<ZoompanEffectType>()
                                            .Where(et => et != ZoompanEffectType.Custom && et != ZoompanEffectType.CustomExpression && et != ZoompanEffectType.RandomPreset)
                                            .ToList();
                    if (availablePresets.Any())
                    {
                        effectToApply = availablePresets[_random.Next(availablePresets.Count)];
                        statusProgress?.Report($"随机选择效果: {ZoompanSettings.GetEnumDescription(effectToApply)}");
                    }
                    else
                    {
                        effectToApply = ZoompanEffectType.ZoomInCenterSlow;
                        statusProgress?.Report($"随机选择失败，使用默认效果: {ZoompanSettings.GetEnumDescription(effectToApply)}");
                    }
                }

                string filterGraph = BuildZoompanFilter(effectToApply, settings, outputResolution, srcWidth, srcHeight);
                string args;
                string durationArg = settings.DurationSeconds.ToString(CultureInfo.InvariantCulture);
                string fpsArg = settings.Fps.ToString(CultureInfo.InvariantCulture);
                string filterArg = string.IsNullOrWhiteSpace(filterGraph) ? "" : $"-vf \"{filterGraph}\" ";
                string codec = "", preset = "", crf = "", pixFmt = "", tag = "";
                // bool requiresTwoPass = false; // Not needed at this scope

                switch (format)
                {
                    case OutputFormat.MP4:
                        codec = "libx264"; preset = "medium"; crf = "23"; pixFmt = "yuv420p";
                        args = $"-y -loop 1 -i \"{sourceImagePath}\" {filterArg}-c:v {codec} -preset {preset} -crf {crf} -pix_fmt {pixFmt} -r {fpsArg} -t {durationArg} \"{outputFilePath}\"";
                        break;
                    case OutputFormat.GIF:
                        // requiresTwoPass = true; // This flag is local to the GIF case
                        tempPalettePath = Path.Combine(Path.GetTempPath(), $"palette_{Guid.NewGuid()}.png");
                        string paletteVf = filterGraph.Replace(",format=pix_fmts=yuv420p", "");
                        if (string.IsNullOrWhiteSpace(paletteVf)) paletteVf = $"fps={fpsArg},scale=640:-1:flags=lanczos,palettegen=stats_mode=diff";
                        else paletteVf += $",fps={fpsArg},scale=640:-1:flags=lanczos,palettegen=stats_mode=diff";
                        string paletteArgs = $"-y -loop 1 -i \"{sourceImagePath}\" -vf \"{paletteVf}\" -t {durationArg} \"{tempPalettePath}\"";
                        statusProgress?.Report("生成 GIF 调色板...");
                        var paletteResult = await RunExternalProcessAsync(_ffmpegPath, paletteArgs, cancellationToken);
                        if (!paletteResult.Success || !File.Exists(tempPalettePath))
                        {
                            string error = $"GIF 调色板生成失败 (Code {paletteResult.ExitCode}). Error: {paletteResult.ErrorOutput}";
                            return new MediaResult(false, ErrorMessage: error, ExitCode: paletteResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds);
                        }
                        statusProgress?.Report("编码 GIF...");
                        string encodeVf = filterGraph.Replace(",format=pix_fmts=yuv420p", "");
                        if (string.IsNullOrWhiteSpace(encodeVf)) encodeVf = $"fps={fpsArg},scale=640:-1:flags=lanczos [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";
                        else encodeVf += $",fps={fpsArg},scale=640:-1:flags=lanczos [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";
                        args = $"-y -loop 1 -i \"{sourceImagePath}\" -i \"{tempPalettePath}\" -lavfi \"{encodeVf}\" -t {durationArg} -f gif \"{outputFilePath}\"";
                        break;
                    case OutputFormat.MOV:
                        codec = "libx265"; preset = "medium"; crf = "25"; pixFmt = "yuv420p"; tag = "-tag:v hvc1 ";
                        args = $"-y -loop 1 -i \"{sourceImagePath}\" {filterArg}-c:v {codec} -preset {preset} -crf {crf} {tag}-pix_fmt {pixFmt} -r {fpsArg} -t {durationArg} \"{outputFilePath}\"";
                        break;
                    default:
                        codec = "libx264"; preset = "medium"; crf = "23"; pixFmt = "yuv420p";
                        args = $"-y -loop 1 -i \"{sourceImagePath}\" {filterArg}-c:v {codec} -preset {preset} -crf {crf} -pix_fmt {pixFmt} -r {fpsArg} -t {durationArg} \"{outputFilePath}\"";
                        break;
                }
                var finalResult = await RunExternalProcessAsync(_ffmpegPath, args, cancellationToken);
                stopwatch.Stop();
                if (finalResult.Success && File.Exists(outputFilePath))
                {
                    return new MediaResult(true, OutputFilePath: outputFilePath, DurationSeconds: stopwatch.Elapsed.TotalSeconds, ExitCode: finalResult.ExitCode);
                }
                else
                {
                    string error = $"FFmpeg failed generating {format} (Code {finalResult.ExitCode}). Error: {finalResult.ErrorOutput}";
                    try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { }
                    return new MediaResult(false, ErrorMessage: error, ExitCode: finalResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { }
                if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { }
                if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { }
                return new MediaResult(false, ErrorMessage: $"Unexpected error: {ex.Message}", DurationSeconds: stopwatch.Elapsed.TotalSeconds);
            }
            finally
            {
                if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { }
            }
        }

        public async Task<MediaResult> GenerateBurstMediaAsync(
            string inputFolderPath, string outputFilePath, int framerate, OutputFormat format,
            IProgress<string>? statusProgress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath)) return new MediaResult(false, ErrorMessage: "FFmpeg executable not found.", ExitCode: -1);
            Stopwatch stopwatch = Stopwatch.StartNew();
            string fileListPath = Path.Combine(Path.GetTempPath(), $"burst_list_{Guid.NewGuid()}.txt");
            string tempPalettePath = "";
            try
            {
                statusProgress?.Report("查找图片文件...");
                var imageFiles = Directory.EnumerateFiles(inputFolderPath)
                                         .Where(f => FileDialogHelper.IsSupportedImageExtension(f))
                                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                         .ToList();
                if (!imageFiles.Any()) return new MediaResult(false, ErrorMessage: "未在源文件夹中找到支持的图片。");
                statusProgress?.Report($"找到 {imageFiles.Count} 张图片，正在生成文件列表...");
                await File.WriteAllLinesAsync(fileListPath, imageFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"), Encoding.UTF8, cancellationToken);
                string args; string fpsArg = framerate.ToString(CultureInfo.InvariantCulture);
                string? destDir = Path.GetDirectoryName(outputFilePath);
                if (destDir != null && !Directory.Exists(destDir)) { try { Directory.CreateDirectory(destDir); } catch (Exception dirEx) { return new MediaResult(false, ErrorMessage: $"Failed to create output directory '{destDir}': {dirEx.Message}"); } }

                if (format == OutputFormat.GIF)
                {
                    tempPalettePath = Path.Combine(Path.GetTempPath(), $"palette_burst_{Guid.NewGuid()}.png");
                    string paletteArgs = $"-y -f concat -safe 0 -r {fpsArg} -i \"{fileListPath}\" -vf \"fps={fpsArg},scale=640:-1:flags=lanczos,palettegen=stats_mode=diff\" \"{tempPalettePath}\"";
                    statusProgress?.Report("生成 GIF 调色板 (Burst)...");
                    var paletteResult = await RunExternalProcessAsync(_ffmpegPath, paletteArgs, cancellationToken);
                    if (!paletteResult.Success || !File.Exists(tempPalettePath))
                    {
                        string error = $"Burst GIF 调色板生成失败 (Code {paletteResult.ExitCode}). Error: {paletteResult.ErrorOutput}";
                        return new MediaResult(false, ErrorMessage: error, ExitCode: paletteResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds);
                    }
                    statusProgress?.Report("编码 GIF (Burst)...");
                    string encodeVf = $"fps={fpsArg},scale=640:-1:flags=lanczos [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";
                    args = $"-y -f concat -safe 0 -r {fpsArg} -i \"{fileListPath}\" -i \"{tempPalettePath}\" -lavfi \"{encodeVf}\" -f gif \"{outputFilePath}\"";
                    var finalResult = await RunExternalProcessAsync(_ffmpegPath, args, cancellationToken);
                    if (finalResult.Success && File.Exists(outputFilePath)) { stopwatch.Stop(); return new MediaResult(true, OutputFilePath: outputFilePath, DurationSeconds: stopwatch.Elapsed.TotalSeconds, ExitCode: finalResult.ExitCode); }
                    else { string error = $"FFmpeg failed burst GIF generation (Code {finalResult.ExitCode}). Error: {finalResult.ErrorOutput}"; try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { } stopwatch.Stop(); return new MediaResult(false, ErrorMessage: error, ExitCode: finalResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
                }
                else
                {
                    string codec, preset, crf, pixFmt, tag = "";
                    if (format == OutputFormat.MP4) { codec = "libx264"; preset = "medium"; crf = "23"; pixFmt = "yuv420p"; }
                    else { codec = "libx265"; preset = "medium"; crf = "25"; pixFmt = "yuv420p"; tag = "-tag:v hvc1 "; }
                    args = $"-y -f concat -safe 0 -r {fpsArg} -i \"{fileListPath}\" -c:v {codec} -preset {preset} -crf {crf} {tag}-pix_fmt {pixFmt} \"{outputFilePath}\"";
                    statusProgress?.Report($"编码 {format} (Burst)...");
                    var finalResult = await RunExternalProcessAsync(_ffmpegPath, args, cancellationToken);
                    if (finalResult.Success && File.Exists(outputFilePath)) { stopwatch.Stop(); return new MediaResult(true, OutputFilePath: outputFilePath, DurationSeconds: stopwatch.Elapsed.TotalSeconds, ExitCode: finalResult.ExitCode); }
                    else { string error = $"FFmpeg failed burst generation (Code {finalResult.ExitCode}). Error: {finalResult.ErrorOutput}"; try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { } stopwatch.Stop(); return new MediaResult(false, ErrorMessage: error, ExitCode: finalResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
                }
            }
            catch (OperationCanceledException) { stopwatch.Stop(); if (File.Exists(fileListPath)) try { File.Delete(fileListPath); } catch { } if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { } if (File.Exists(outputFilePath)) try { File.Delete(outputFilePath); } catch { } throw; }
            catch (Exception ex) { stopwatch.Stop(); if (File.Exists(fileListPath)) try { File.Delete(fileListPath); } catch { } if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { } if (File.Exists(outputFilePath)) try { File.Delete(outputFilePath); } catch { } return new MediaResult(false, ErrorMessage: $"Unexpected error during burst generation: {ex.Message}", DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
            finally { if (File.Exists(fileListPath)) try { File.Delete(fileListPath); } catch (Exception ex) { Debug.WriteLine($"Failed to delete temp file {fileListPath}: {ex.Message}"); } if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch (Exception ex) { Debug.WriteLine($"Failed to delete temp file {tempPalettePath}: {ex.Message}"); } }
        }

        public async Task<MediaResult> GenerateAnimationFromFilesAsync(
           string fileListPath, string outputFilePath, double frameDurationSeconds, OutputFormat format,
           bool loopAnimation, IProgress<string>? statusProgress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath)) return new MediaResult(false, ErrorMessage: "FFmpeg executable not found.", ExitCode: -1);
            Stopwatch stopwatch = Stopwatch.StartNew();
            string tempPalettePath = "";
            try
            {
                if (!File.Exists(fileListPath)) return new MediaResult(false, ErrorMessage: $"Temporary file list not found: {fileListPath}");
                string? destDir = Path.GetDirectoryName(outputFilePath);
                if (destDir != null && !Directory.Exists(destDir)) { try { Directory.CreateDirectory(destDir); } catch (Exception dirEx) { return new MediaResult(false, ErrorMessage: $"Failed to create output directory '{destDir}': {dirEx.Message}"); } }
                double fps = (frameDurationSeconds > 0) ? 1.0 / frameDurationSeconds : 10;
                string args;
                if (format == OutputFormat.GIF)
                {
                    tempPalettePath = Path.Combine(Path.GetTempPath(), $"anim_palette_{Guid.NewGuid()}.png");
                    string paletteArgs = $"-y -f concat -safe 0 -r {fps:F2} -i \"{fileListPath}\" -vf \"fps={fps:F2},scale=640:-1:flags=lanczos,palettegen=stats_mode=diff\" \"{tempPalettePath}\"";
                    statusProgress?.Report("生成 GIF 调色板...");
                    var paletteResult = await RunExternalProcessAsync(_ffmpegPath, paletteArgs, cancellationToken);
                    if (!paletteResult.Success || !File.Exists(tempPalettePath))
                    {
                        string error = $"动画 GIF 调色板生成失败 (Code {paletteResult.ExitCode}). Error: {paletteResult.ErrorOutput}";
                        return new MediaResult(false, ErrorMessage: error, ExitCode: paletteResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds);
                    }
                    statusProgress?.Report("编码 GIF...");
                    string encodeVf = $"fps={fps:F2},scale=640:-1:flags=lanczos [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";
                    string loopArg = loopAnimation ? "0" : "-1";
                    args = $"-y -f concat -safe 0 -r {fps:F2} -i \"{fileListPath}\" -i \"{tempPalettePath}\" -lavfi \"{encodeVf}\" -f gif -loop {loopArg} \"{outputFilePath}\"";
                    var finalResult = await RunExternalProcessAsync(_ffmpegPath, args, cancellationToken);
                    if (finalResult.Success && File.Exists(outputFilePath)) { stopwatch.Stop(); return new MediaResult(true, OutputFilePath: outputFilePath, DurationSeconds: stopwatch.Elapsed.TotalSeconds, ExitCode: finalResult.ExitCode); }
                    else { string error = $"FFmpeg failed animation GIF generation (Code {finalResult.ExitCode}). Error: {finalResult.ErrorOutput}"; try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { } stopwatch.Stop(); return new MediaResult(false, ErrorMessage: error, ExitCode: finalResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
                }
                else
                {
                    string codec = "libx264"; string preset = "medium"; string crf = "23"; string pixFmt = "yuv420p";
                    args = $"-y -f concat -safe 0 -r {fps:F2} -i \"{fileListPath}\" -c:v {codec} -preset {preset} -crf {crf} -pix_fmt {pixFmt} \"{outputFilePath}\"";
                    statusProgress?.Report($"编码 {format} (动画)...");
                    var finalResult = await RunExternalProcessAsync(_ffmpegPath, args, cancellationToken);
                    if (finalResult.Success && File.Exists(outputFilePath)) { stopwatch.Stop(); return new MediaResult(true, OutputFilePath: outputFilePath, DurationSeconds: stopwatch.Elapsed.TotalSeconds, ExitCode: finalResult.ExitCode); }
                    else { string error = $"FFmpeg failed animation generation (Code {finalResult.ExitCode}). Error: {finalResult.ErrorOutput}"; try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch { } stopwatch.Stop(); return new MediaResult(false, ErrorMessage: error, ExitCode: finalResult.ExitCode, DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
                }
            }
            catch (OperationCanceledException) { stopwatch.Stop(); if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { } if (File.Exists(outputFilePath)) try { File.Delete(outputFilePath); } catch { } throw; }
            catch (Exception ex) { stopwatch.Stop(); if (File.Exists(tempPalettePath)) try { File.Delete(tempPalettePath); } catch { } if (File.Exists(outputFilePath)) try { File.Delete(outputFilePath); } catch { } return new MediaResult(false, ErrorMessage: $"Unexpected error during animation generation: {ex.Message}", DurationSeconds: stopwatch.Elapsed.TotalSeconds); }
        }

        public string BuildZoompanFilter(ZoompanEffectType effectType, ZoompanSettings settings, string outputResolution, int sourceWidth, int sourceHeight)
        {
            string outWidthStr, outHeightStr;
            string[] outDims = outputResolution.Split('x');
            if (outDims.Length == 2 && int.TryParse(outDims[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ow) && int.TryParse(outDims[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int oh) && ow > 0 && oh > 0)
            { outWidthStr = ow.ToString(CultureInfo.InvariantCulture); outHeightStr = oh.ToString(CultureInfo.InvariantCulture); }
            else { outWidthStr = "iw"; outHeightStr = "ih"; }
            int fps = Math.Max(1, settings.Fps); double duration = Math.Max(0.1, settings.DurationSeconds);
            int totalFrames = Math.Max(1, (int)Math.Ceiling(duration * fps));
            string dStr = totalFrames.ToString(CultureInfo.InvariantCulture); string sStr = $"{outWidthStr}x{outHeightStr}"; string fpsStr = fps.ToString(CultureInfo.InvariantCulture);
            string zoomExpr = $"'min(zoom+{0.0015 / (fps / 30.0)}, {settings.TargetZoom.ToString("F4", CultureInfo.InvariantCulture)})'";
            string xExpr = "'iw/2-(iw/zoom/2)'"; string yExpr = "'ih/2-(ih/zoom/2)'";
            switch (effectType)
            {
                case ZoompanEffectType.ZoomInCenterSlow: zoomExpr = $"'min(zoom+{0.0010 / (fps / 30.0)}, 1.5)'"; break;
                case ZoompanEffectType.ZoomInCenterFast: zoomExpr = $"'min(zoom+{0.0020 / (fps / 30.0)}, 1.8)'"; break;
                case ZoompanEffectType.ZoomOutCenter: zoomExpr = $"'max(1.5 - on/(({dStr})-1) * 0.5, 1.0)'"; break;
                case ZoompanEffectType.PanRight: zoomExpr = "'1.01'"; xExpr = $"'on*{30.0 / fps:G6}'"; yExpr = $"(ih-({outHeightStr}))/2"; break;
                case ZoompanEffectType.PanLeft: zoomExpr = "'1.01'"; xExpr = $"'iw - on*{30.0 / fps:G6} - ({outWidthStr})'"; yExpr = $"(ih-({outHeightStr}))/2"; break;
                case ZoompanEffectType.PanUp: zoomExpr = "'1.01'"; xExpr = $"(iw-({outWidthStr}))/2"; yExpr = $"'ih - on*{30.0 / fps:G6} - ({outHeightStr})'"; break;
                case ZoompanEffectType.PanDown: zoomExpr = "'1.01'"; xExpr = $"(iw-({outWidthStr}))/2"; yExpr = $"'on*{30.0 / fps:G6}'"; break;
                case ZoompanEffectType.ZoomInPanTopRight: zoomExpr = "'min(zoom+0.0015, 1.6)'"; xExpr = $"'iw/2-(iw/zoom/2)+(on*{30.0 / fps * 0.7:G6})'"; yExpr = $"'ih/2-(ih/zoom/2)-(on*{30.0 / fps * 0.7:G6})'"; break;
                case ZoompanEffectType.ZoomInPanBottomLeft: zoomExpr = "'min(zoom+0.0015, 1.6)'"; xExpr = $"'iw/2-(iw/zoom/2)-(on*{30.0 / fps * 0.7:G6})'"; yExpr = $"'ih/2-(ih/zoom/2)+(on*{30.0 / fps * 0.7:G6})'"; break;
                case ZoompanEffectType.IphoneStyle: zoomExpr = "'min(zoom+0.0010, 1.25)'"; xExpr = $"'iw/2-(iw/zoom/2)+(on*{30.0 / fps * 0.3:G6})'"; yExpr = $"'ih/2-(ih/zoom/2)+(on*{30.0 / fps * 0.2:G6})'"; break;
                case ZoompanEffectType.Custom:
                    double zoomStart = 1.0; double zoomEnd = settings.TargetZoom;
                    if (Math.Abs(zoomEnd - zoomStart) < 0.001) zoomEnd = zoomStart + 0.01 * Math.Sign(zoomEnd - zoomStart);
                    double zoomRate = (totalFrames > 1) ? (zoomEnd - zoomStart) / (totalFrames - 1) : 0;
                    string zoomRateStr = zoomRate.ToString("F10", CultureInfo.InvariantCulture);
                    string zoomEndStr = zoomEnd.ToString("F4", CultureInfo.InvariantCulture);
                    string zoomStartStr = zoomStart.ToString("F4", CultureInfo.InvariantCulture);
                    if (zoomRate > 0) zoomExpr = $"'min(max({zoomStartStr},zoom)+{zoomRateStr}*(on-1),{zoomEndStr})'";
                    else if (zoomRate < 0) zoomExpr = $"'max(min(zoom,{zoomStartStr})+{zoomRateStr}*(on-1),{zoomEndStr})'";
                    else zoomExpr = $"'{zoomStartStr}'";
                    double panPixelsPerSecond = 30; double panPixelsPerFrame = (fps > 0) ? panPixelsPerSecond / fps : 0; string panStepStr = panPixelsPerFrame.ToString("G6", CultureInfo.InvariantCulture);
                    switch (settings.PanDirection)
                    {
                        case PanDirection.Up: yExpr = $"'ih/2-(ih/zoom/2)-on*{panStepStr}'"; break;
                        case PanDirection.Down: yExpr = $"'ih/2-(ih/zoom/2)+on*{panStepStr}'"; break;
                        case PanDirection.Left: xExpr = $"'iw/2-(iw/zoom/2)-on*{panStepStr}'"; break;
                        case PanDirection.Right: xExpr = $"'iw/2-(iw/zoom/2)+on*{panStepStr}'"; break;
                    }
                    break;
                case ZoompanEffectType.CustomExpression: return string.IsNullOrWhiteSpace(settings.CustomFilterExpression) ? "null" : settings.CustomFilterExpression;
            }
            if (settings.EnableJitter && effectType != ZoompanEffectType.CustomExpression)
            {
                string jitterAmount = "1";
                xExpr = $"'{xExpr} + (random(0)*{jitterAmount} - {jitterAmount}/2)'";
                yExpr = $"'{yExpr} + (random(1)*{jitterAmount} - {jitterAmount}/2)'";
            }
            string filter = $"zoompan=z={zoomExpr}:x={xExpr}:y={yExpr}:d={dStr}:s={sStr}:fps={fpsStr},format=pix_fmts=yuv420p";
            return filter;
        }

        public async Task<(bool Success, int ExitCode, string ErrorOutput, string StandardOutput)> RunExternalProcessAsync(
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
                var errorOutput = new StringBuilder(); var standardOutput = new StringBuilder();
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };
                if (readStdOut) { process.OutputDataReceived += (sender, e) => { if (e.Data != null) standardOutput.AppendLine(e.Data); }; }
                try
                {
                    process.Start(); process.BeginErrorReadLine(); if (readStdOut) process.BeginOutputReadLine();
                    await process.WaitForExitAsync(cancellationToken); process.WaitForExit();
                    bool success = process.ExitCode == 0;
                    return (success, process.ExitCode, errorOutput.ToString().Trim(), standardOutput.ToString().Trim());
                }
                catch (OperationCanceledException) { try { if (!process.HasExited) process.Kill(); } catch { } throw; }
                catch (Exception ex) { try { if (!process.HasExited) process.Kill(); } catch { } throw new Exception($"Error running external process '{fileName}': {ex.Message}", ex); }
            }
        }

        public async Task<string?> GetImageResolutionAsync(string imagePath, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(_ffprobePath) || !File.Exists(_ffprobePath)) return null;
            string args = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 \"{imagePath}\"";
            var (success, _, stdOut, _) = await RunExternalProcessAsync(_ffprobePath, args, token, true);
            if (success && !string.IsNullOrWhiteSpace(stdOut))
            {
                string resolution = stdOut.Trim();
                if (resolution.Contains('x')) return resolution;
            }
            return null;
        }
    }
}