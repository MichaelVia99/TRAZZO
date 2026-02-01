using System;

namespace Bitacora.Models
{
    public class AttachmentItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string OriginalPath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public bool FromServer { get; set; }

        public string SizeMbText => $"{(FileSize / 1024.0 / 1024.0):F2} MB";
    }
}
