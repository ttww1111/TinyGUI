using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        /// <summary>缩略图解码宽度。实际显示为 40px，按 2 倍解码保证高清屏不发虚。</summary>
        private const int ThumbnailDecodeWidth = 80;

        private ImageSource _thumbnail;

        /// <summary>
        /// 列表缩略图。后台线程解码得到；解码失败（非图片/损坏/无权限）时保持 null，
        /// 界面上退化为一个浅色占位块，不影响该文件的正常压缩。
        /// </summary>
        public ImageSource Thumbnail
        {
            get => _thumbnail;
            private set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        /// <summary>
        /// 从磁盘解码一张小缩略图。
        /// 刻意先整份读进内存流再解码、且解码后 Freeze()，原因有两个：
        /// 1) 解码完立刻释放文件句柄，全程不占用/锁住原图 —— 压缩结束若勾选「替换原文件」要覆盖写回；
        /// 2) Freeze 后的 BitmapImage 可跨线程安全交给 UI 线程绑定显示。
        /// </summary>
        public void LoadThumbnail()
        {
            if (_thumbnail != null) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(FullPath);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.DecodePixelWidth = ThumbnailDecodeWidth;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    Thumbnail = bmp;
                }
            }
            catch
            {
                // 解不出来就留空占位，不打断整批处理
            }
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
