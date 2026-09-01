using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TinyGUI.Properties
{
    /// <summary>
    /// 应用设置（JSON 持久化，替代旧的 Settings.Designer）。
    /// API Key 按用户要求明文存储。
    /// </summary>
    public class AppSettings
    {
        // 便携模式：优先写 exe 同目录；若该目录不可写（如安装到 Program Files）则回退 AppData
        private static readonly string PortablePath =
            Path.Combine(AppContext.BaseDirectory, "settings.json");
        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TinyGUI", "settings.json");

        public List<string> ApiKeys { get; set; } = new List<string>();

        /// <summary>并发数（Parallel.ForEachAsync 的 MaxDegreeOfParallelism）</summary>
        public int Concurrency { get; set; } = 4;

        /// <summary>
        /// 压缩后如何处理原文件：0=每次询问（默认） 1=总是替换原文件 2=总是保留为新文件。
        /// 用户在询问窗口勾选「记住我的选择」后才会变成 1 或 2。
        /// </summary>
        public int ReplaceAskMode { get; set; }

        public int LanguageIndex { get; set; } = -1;
        public bool MetaCopyright { get; set; }
        public bool MetaLocation { get; set; }
        public bool MetaCreationTime { get; set; }

        /// <summary>是否启用智能裁切（原独立 Tab，按用户要求并入设置面板）</summary>
        public bool SmartCutEnabled { get; set; }

        public string ImageWidth { get; set; } = string.Empty;
        public string ImageHeight { get; set; } = string.Empty;

        /// <summary>裁切模式索引：0=Scale 1=Fit 2=Cover 3=Thumb</summary>
        public int ResizeModeIndex { get; set; }

        /// <summary>
        /// 每个 API Key 本月的已用次数（持久化，跨重启保留）。
        /// Key 为 API Key 明文，Value 为最近一次压缩成功时服务端返回的累计已用次数。
        /// 只有成功压缩过的 Key 才会有记录。
        /// </summary>
        public Dictionary<string, uint> KeyUsage { get; set; } = new Dictionary<string, uint>();

        private static AppSettings _current = new AppSettings();
        public static AppSettings Current
        {
            get => _current;
            set => _current = value;
        }

        public static void Load()
        {
            try
            {
                string json = null;
                if (File.Exists(PortablePath))
                    json = File.ReadAllText(PortablePath);
                else if (File.Exists(FilePath))
                    json = File.ReadAllText(FilePath);

                if (json != null)
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) _current = loaded;
                }
            }
            catch
            {
                // 损坏则忽略，使用默认值
                _current = new AppSettings();
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            // 优先写 exe 同目录（便携）；失败（如 Program Files 不可写）则回退 AppData
            if (TryWrite(PortablePath, json)) return;
            TryWrite(FilePath, json);
        }

        private static bool TryWrite(string path, string json)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
