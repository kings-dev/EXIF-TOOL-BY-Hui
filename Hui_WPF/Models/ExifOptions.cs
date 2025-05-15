using System;

namespace Hui_WPF.Models
{
    public record ExifRemovalOptions(
        bool RemoveAllMetadata = true,
        bool KeepDateTaken = false,
        bool KeepGps = false,
        bool KeepOrientation = false,
        bool KeepCameraInfo = false,
        bool KeepColorSpace = false,
        bool RemoveThumbnail = true
    );

    public record ExifWriteOptions(
       bool WriteCommonTags = false,
       string? Artist = null,
       string? Copyright = null,
       string? Comment = null,
       string? Description = null,
       int? Rating = null,
       bool WriteDateTaken = false,
       DateTime? DateTimeOriginal = null,
       bool WriteGps = false,
       double? Latitude = null,
       double? Longitude = null
    );
}