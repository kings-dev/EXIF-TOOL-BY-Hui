using System.ComponentModel;

namespace Hui_WPF.Models
{
    public enum OutputFormat { MOV, MP4, GIF }
    public enum PanDirection
    {
        [Description("无")]
        None,
        [Description("上")]
        Up,
        [Description("下")]
        Down,
        [Description("左")]
        Left,
        [Description("右")]
        Right
    }
    public enum ZoompanEffectType
    {
        [Description("自定义 (Custom)")]
        Custom,
        [Description("中心放大 (慢速)")]
        ZoomInCenterSlow,
        [Description("中心放大 (快速)")]
        ZoomInCenterFast,
        [Description("中心缩小")]
        ZoomOutCenter,
        [Description("向右平移")]
        PanRight,
        [Description("向左平移")]
        PanLeft,
        [Description("向上平移")]
        PanUp,
        [Description("向下平移")]
        PanDown,
        [Description("放大 + 右上平移")]
        ZoomInPanTopRight,
        [Description("放大 + 左下平移")]
        ZoomInPanBottomLeft,
        [Description("iPhone Ken Burns 风格")]
        IphoneStyle,
        [Description("随机 (每图随机预设)")]
        RandomPreset,
        [Description("<<< 自定义 FFmpeg 表达式 >>>")]
        CustomExpression
    }
    public enum SourceType { Directory, File }
}