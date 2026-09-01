using System;
using System.ComponentModel;

namespace TinyGUI.Models
{
    public enum ImageStatus
    {
        Pending,
        Processing,
        Success,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 文件列表中的单张图片及其压缩结果。
    /// </summary>
    public class ImageItem : INotifyPropertyChanged
    {
        private long _originalSize;
        private long _compressedSize;
        private ImageStatus _status = ImageStatus.Pending;
        private string _errorMessage = string.Empty;
        private double _savedPercent;

        public string FullPath { get; }
        public string FileName { get; }

        public long OriginalSize
        {
            get => _originalSize;
            set { _originalSize = value; OnPropertyChanged(nameof(OriginalSize)); OnPropertyChanged(nameof(OriginalSizeText)); }
        }

        public long CompressedSize
        {
            get => _compressedSize;
            set { _compressedSize = value; OnPropertyChanged(nameof(CompressedSize)); OnPropertyChanged(nameof(CompressedSizeText)); }
        }

        public ImageStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public double SavedPercent
        {
            get => _savedPercent;
            set { _savedPercent = value; OnPropertyChanged(nameof(SavedPercent)); OnPropertyChanged(nameof(SavedText)); }
        }

        public string OriginalSizeText => FormatSize(_originalSize);
        public string CompressedSizeText => FormatSize(_compressedSize);

        public string SavedText => _savedPercent > 0 ? $"-{_savedPercent:F1}%" : string.Empty;

        public string StatusText => _status switch
        {
            ImageStatus.Pending => "等待中",
            ImageStatus.Processing => "处理中…",
            ImageStatus.Success => "完成",
            ImageStatus.Failed => "失败",
            ImageStatus.Cancelled => "已取消",
            _ => string.Empty
        };

        public string StatusColor => _status switch
        {
            ImageStatus.Success => "#2BB673",
            ImageStatus.Failed => "#E0533D",
            ImageStatus.Cancelled => "#9AA0A6",
            _ => "#6B7280"
        };

        public ImageItem(string fullPath)
        {
            FullPath = fullPath;
            FileName = System.IO.Path.GetFileName(fullPath);
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";
            string[] units = { "B", "KB", "MB", "GB" };
            int i = 0;
            double value = bytes;
            while (value >= 1024 && i < units.Length - 1)
            {
                value /= 1024;
                i++;
            }
            return $"{value:F1} {units[i]}";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
