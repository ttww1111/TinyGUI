using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TinyGUI.Models;
using TinyGUI.Properties;
using TinyGUI.Services;
using TinyGUI.Views;

namespace TinyGUI.ViewModels
{
    public class MainModel : ViewModelBase
    {
        // 每次开始压缩都用设置里最新的 Key 重建，避免改了 Key 还拿旧列表去请求
        private ImageCompressor _compressor;

        /// <summary>构造期间为 true：此时给 LanguageIndex 赋值只是同步数据，绝不触发窗口重建</summary>
        private bool _initializing = true;

        public MainModel(ImageCompressor compressor)
        {
            _compressor = compressor;

            // 初始化设置相关属性
            var s = AppSettings.Current;

            // 必须先建 Key 列表：下面每个 setter 都会触发 SyncToMemory()（它会用 KeyItems
            // 重写 s.ApiKeys 并立即落盘）。若此时 KeyItems 还是空的，ApiKeys 会被清成空列表
            // 并写进 settings.json —— 用户存的 Key 就全没了。
            LoadKeyItems();

            // 从磁盘恢复上次的额度使用记录（跨重启记住剩余次数）
            foreach (var kv in s.KeyUsage ?? new Dictionary<string, uint>())
                _keyUsage[kv.Key] = kv.Value;

            Concurrency = s.Concurrency;
            LanguageIndex = s.LanguageIndex;
            MetaCopyright = s.MetaCopyright;
            MetaLocation = s.MetaLocation;
            MetaCreationTime = s.MetaCreationTime;
            ReplaceAskMode = s.ReplaceAskMode;
            SmartCutEnabled = s.SmartCutEnabled;
            ImageWidth = s.ImageWidth ?? string.Empty;
            ImageHeight = s.ImageHeight ?? string.Empty;
            ResizeModeIndex = s.ResizeModeIndex;

            StartCommand = new RelayCommand(async _ => await StartAsync(_pendingPaths));
            OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder());
            ClearListCommand = new RelayCommand(_ => ClearList());
            AddKeyCommand = new RelayCommand(_ => AddKey());
            RemoveKeyCommand = new RelayCommand(RemoveKey);

            // 列表增删时同步 HasImages（决定文件列表框显不显示）
            Images.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(HasImages));
            // Key 行增删时同步总剩余额度
            KeyItems.CollectionChanged += (s, e) => RefreshKeyItems();

            // 切换语言时让「剩余额度 / 待填写 / 剩 N 次」等状态文字原地刷新
            Loc.Instance.PropertyChanged += (s, e) => RefreshKeyItems();

            _initializing = false;
        }

        #region 模式 Tab
        private bool _compressRadioButtonIsChecked = true;
        public bool CompressRadioButtonIsChecked
        {
            get => _compressRadioButtonIsChecked;
            set => SetField(ref _compressRadioButtonIsChecked, value, nameof(CompressRadioButtonIsChecked));
        }

        private bool _settingRadioButtonIsChecked;
        public bool SettingRadioButtonIsChecked
        {
            get => _settingRadioButtonIsChecked;
            set => SetField(ref _settingRadioButtonIsChecked, value, nameof(SettingRadioButtonIsChecked));
        }
        #endregion

        #region 智能裁切（并入设置面板，不再单独占用 Tab）
        private bool _smartCutEnabled;
        public bool SmartCutEnabled
        {
            get => _smartCutEnabled;
            set { if (SetField(ref _smartCutEnabled, value, nameof(SmartCutEnabled))) SyncToMemory(); }
        }

        private string _imageWidth = string.Empty;
        public string ImageWidth
        {
            get => _imageWidth;
            set { if (SetField(ref _imageWidth, value, nameof(ImageWidth))) SyncToMemory(); }
        }

        private string _imageHeight = string.Empty;
        public string ImageHeight
        {
            get => _imageHeight;
            set { if (SetField(ref _imageHeight, value, nameof(ImageHeight))) SyncToMemory(); }
        }

        /// <summary>裁切模式索引：0=Scale 1=Fit 2=Cover 3=Thumb</summary>
        private int _resizeModeIndex;
        public int ResizeModeIndex
        {
            get => _resizeModeIndex;
            set { if (SetField(ref _resizeModeIndex, value, nameof(ResizeModeIndex))) SyncToMemory(); }
        }
        #endregion

        #region 设置

        /// <summary>设置面板里的 API Key 列表（一行一个，右侧显示该 Key 的剩余额度）</summary>
        public ObservableCollection<ApiKeyItem> KeyItems { get; } = new ObservableCollection<ApiKeyItem>();

        /// <summary>添加一个空的 Key 行</summary>
        public RelayCommand AddKeyCommand { get; }

        /// <summary>删除某一行 Key（参数是 ApiKeyItem）</summary>
        public RelayCommand RemoveKeyCommand { get; }

        /// <summary>建一行 Key 并订阅输入变化：编辑时立即落盘 + 刷新额度</summary>
        private ApiKeyItem CreateKeyItem(string value)
        {
            var item = new ApiKeyItem { Value = value };
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ApiKeyItem.Value))
                {
                    SyncToMemory();
                    RefreshKeyItems();
                }
            };
            return item;
        }

        private void AddKey()
        {
            KeyItems.Add(CreateKeyItem(string.Empty));
            SyncToMemory();
            RefreshKeyItems();
        }

        private void RemoveKey(object parameter)
        {
            if (parameter is ApiKeyItem item && KeyItems.Contains(item))
            {
                KeyItems.Remove(item);
                SyncToMemory();
                RefreshKeyItems();
            }
        }

        /// <summary>
        /// 刷新每一行的剩余额度，并标出重复项。
        /// 重复的 Key 不参与轮换（KeyPool 会去重），等于白填，所以要显式标红提醒。
        /// </summary>
        private void RefreshKeyItems()
        {
            var seen = new HashSet<string>();
            foreach (var item in KeyItems)
            {
                string v = (item.Value ?? string.Empty).Trim();

                if (v.Length == 0)
                {
                    item.RemainingText = Loc.Instance.NotFilled;
                    item.Duplicate = false;
                    continue;
                }

                item.Duplicate = !seen.Add(v);

                item.RemainingText = _keyUsage.TryGetValue(v, out uint used)
                    ? string.Format(Loc.Instance.RemainingTimes, Math.Max(0, MonthlyQuota - (int)used))
                    : string.Empty;
            }
            RaisePropertyChanged(nameof(TotalRemaining));
            RaisePropertyChanged(nameof(TotalRemainingText));
            RaisePropertyChanged(nameof(QuotaText));
        }

        /// <summary>从设置里加载 Key 列表</summary>
        private void LoadKeyItems()
        {
            KeyItems.Clear();
            foreach (var k in AppSettings.Current.ApiKeys ?? new List<string>())
                KeyItems.Add(CreateKeyItem(k));

            // 至少留一行空的，否则用户看到的是一片空白、不知道该点哪
            if (KeyItems.Count == 0) KeyItems.Add(CreateKeyItem(string.Empty));

            RefreshKeyItems();
        }

        private int _concurrency = 4;
        /// <summary>并发数，钳制在 1~16：0 会让 Parallel.ForEachAsync 抛异常，过大只会触发限流</summary>
        public int Concurrency
        {
            get => _concurrency;
            set
            {
                int v = Math.Max(1, Math.Min(16, value));
                if (SetField(ref _concurrency, v, nameof(Concurrency))) SyncToMemory();
            }
        }

        /// <summary>TinyPNG 官方固定月度额度：免费版每个 Key 每月 500 次，不可修改。</summary>
        private const int MonthlyQuota = 500;

        /// <summary>压缩后如何处理原文件：0=每次询问 1=总是替换 2=总是保留为新文件</summary>
        private int _replaceAskMode;
        public int ReplaceAskMode
        {
            get => _replaceAskMode;
            set
            {
                int v = Math.Max(0, Math.Min(2, value));
                if (SetField(ref _replaceAskMode, v, nameof(ReplaceAskMode))) SyncToMemory();
            }
        }

        private int _languageIndex = -1;
        public int LanguageIndex
        {
            get => _languageIndex;
            set
            {
                if (SetField(ref _languageIndex, value, nameof(LanguageIndex)))
                {
                    SyncToMemory();
                    // 初始化阶段只同步数据，不触发任何界面刷新
                    if (_initializing) return;

                    // 实时切换：只换文化 + 通知绑定刷新，不重建窗口，
                    // 所以压缩过程中也能随便切，不会丢任务
                    App.ApplyLanguage(value);
                }
            }
        }

        private bool _metaCopyright;
        public bool MetaCopyright
        {
            get => _metaCopyright;
            set { if (SetField(ref _metaCopyright, value, nameof(MetaCopyright))) SyncToMemory(); }
        }

        private bool _metaLocation;
        public bool MetaLocation
        {
            get => _metaLocation;
            set { if (SetField(ref _metaLocation, value, nameof(MetaLocation))) SyncToMemory(); }
        }

        private bool _metaCreationTime;
        public bool MetaCreationTime
        {
            get => _metaCreationTime;
            set { if (SetField(ref _metaCreationTime, value, nameof(MetaCreationTime))) SyncToMemory(); }
        }
        #endregion

        /// <summary>
        /// 将当前 UI 设置同步到内存中的 AppSettings.Current，
        /// 这样切换语言重建窗口时不会丢失尚未点「保存设置」的输入框内容。
        /// </summary>
        private void SyncToMemory()
        {
            // 构造期间一律不碰 AppSettings。
            // 否则初始化第一个属性时就会用其余属性的「默认值」去覆盖刚从磁盘读出来的真实设置
            // （例如 Concurrency 赋值触发的同步，会把还没来得及初始化的 SmartCutEnabled 冲成 false）。
            if (_initializing) return;

            var s = AppSettings.Current;
            // Key 来自设置面板的列表（去重：KeyPool 本身也会去重，存重复的没有意义）
            s.ApiKeys = KeyItems
                .Select(k => (k.Value ?? string.Empty).Trim())
                .Where(k => k.Length > 0)
                .Distinct()
                .ToList();
            s.Concurrency = Math.Max(1, Concurrency);
            s.ReplaceAskMode = ReplaceAskMode;
            s.MetaCopyright = MetaCopyright;
            s.MetaLocation = MetaLocation;
            s.MetaCreationTime = MetaCreationTime;
            s.SmartCutEnabled = SmartCutEnabled;
            s.ImageWidth = ImageWidth;
            s.ImageHeight = ImageHeight;
            s.ResizeModeIndex = ResizeModeIndex;

            // 修改即落盘，因此不再需要单独的「保存设置」按钮
            s.Save();
        }

        #region 额度 / 进度

        /// <summary>每个 API Key 本月的已用次数。只有用过的 Key 才有记录。</summary>
        private readonly Dictionary<string, uint> _keyUsage = new Dictionary<string, uint>();

        /// <summary>记录某个 Key 的最新已用次数，并同步刷新设置面板里那一行的显示</summary>
        private void RecordKeyUsage(string key, uint? used)
        {
            if (string.IsNullOrEmpty(key) || !used.HasValue) return;
            // 服务端返回的是累计值，取最新的即可
            _keyUsage[key] = used.Value;
            // 持久化，跨重启记住剩余次数
            AppSettings.Current.KeyUsage[key] = used.Value;
            AppSettings.Current.Save();
            RefreshKeyItems();
        }

        /// <summary>设置里配置的有效 Key 列表（去重、去空）</summary>
        private static List<string> ConfiguredKeys
            => (AppSettings.Current.ApiKeys ?? new List<string>())
               .Select(k => (k ?? string.Empty).Trim())
               .Where(k => k.Length > 0)
               .Distinct()
               .ToList();

        /// <summary>所有 Key 的剩余额度合计（没用过的 Key 按满额 500 算）</summary>
        public int TotalRemaining
            => KeyItems
               .Select(k => (k.Value ?? string.Empty).Trim())
               .Where(k => k.Length > 0)
               .Distinct()
               .Sum(k => (int)Math.Max(0, MonthlyQuota - (_keyUsage.TryGetValue(k, out var used) ? used : 0)));

        /// <summary>是否有任意一个已填写的 Key 记录过使用量（压缩成功过才记）</summary>
        private bool AnyUsageRecorded
            => KeyItems.Any(k =>
            {
                var v = (k.Value ?? string.Empty).Trim();
                return v.Length > 0 && _keyUsage.ContainsKey(v);
            });

        /// <summary>设置面板底部「剩余额度」后的数字：没有任何使用记录时显示「未设置」</summary>
        public string TotalRemainingText
            => AnyUsageRecorded ? TotalRemaining.ToString() : Loc.Instance.NotSet;

        /// <summary>主窗口额度行：没压缩过时不显示（ShowQuota=false），压缩过才显示</summary>
        public string QuotaText
            => ConfiguredKeys.Count == 0
                ? $"{Loc.Instance.QuotaLabel} —"
                : (AnyUsageRecorded ? $"{Loc.Instance.QuotaLabel} {TotalRemaining}" : $"{Loc.Instance.QuotaLabel} {Loc.Instance.NotSet}");

        /// <summary>是否已经压缩过：初始为 false，主窗口不显示「剩余额度」；压缩过一次后置 true</summary>
        private bool _hasCompressed;
        public bool HasCompressed
        {
            get => _hasCompressed;
            set { if (SetField(ref _hasCompressed, value, nameof(HasCompressed))) RaisePropertyChanged(nameof(ShowQuota)); }
        }

        /// <summary>主窗口「剩余额度」行是否可见：只有压缩过才显示</summary>
        public bool ShowQuota => HasCompressed;

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetField(ref _totalCount, value, nameof(TotalCount));
        }

        private int _processedCount;
        public int ProcessedCount
        {
            get => _processedCount;
            set => SetField(ref _processedCount, value, nameof(ProcessedCount));
        }

        private int _succeededCount;
        public int SucceededCount
        {
            get => _succeededCount;
            set => SetField(ref _succeededCount, value, nameof(SucceededCount));
        }

        private int _failedCount;
        public int FailedCount
        {
            get => _failedCount;
            set => SetField(ref _failedCount, value, nameof(FailedCount));
        }

        public int RemainingCount => Math.Max(0, TotalCount - ProcessedCount);

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value, nameof(ProgressValue));
        }

        private bool _isIndeterminate;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetField(ref _isIndeterminate, value, nameof(IsIndeterminate));
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value, nameof(IsBusy));
        }

        private bool _canCancel;
        public bool CanCancel
        {
            get => _canCancel;
            set => SetField(ref _canCancel, value, nameof(CanCancel));
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value, nameof(StatusText));
        }

        private long _totalSavedBytes;
        public long TotalSavedBytes
        {
            get => _totalSavedBytes;
            set
            {
                if (SetField(ref _totalSavedBytes, value, nameof(TotalSavedBytes)))
                    RaisePropertyChanged(nameof(TotalSavedText));
            }
        }

        public string TotalSavedText => ImageItem.FormatSize(_totalSavedBytes);

        public ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();

        /// <summary>列表里有没有图片。没有时不显示文件列表框，避免空框占地方。</summary>
        public bool HasImages => Images.Count > 0;
        #endregion

        #region 日志
        private readonly List<string> _logs = new List<string>();
        private string _logsText = string.Empty;

        /// <summary>详细日志文本（用于 TextBox 绑定，支持选中复制）</summary>
        public string LogsText
        {
            get => _logsText;
            private set => SetField(ref _logsText, value, nameof(LogsText));
        }

        /// <summary>追加了一行日志（在 UI 线程触发），供界面滚动到底部</summary>
        public event Action LogAppended;

        /// <summary>
        /// 追加一行日志。可能从后台线程调用，因此统一切回 UI 线程再更新文本。
        /// 只保留最近 300 条，避免长时间批量压缩后内存膨胀。
        /// </summary>
        private void AddLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var app = Application.Current;
            if (app == null) return;
            try
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _logs.Add(line);
                    while (_logs.Count > 300) _logs.RemoveAt(0);
                    LogsText = string.Join(Environment.NewLine, _logs);
                    if (!_hasLogs) { _hasLogs = true; RaisePropertyChanged(nameof(HasLogs)); }
                    LogAppended?.Invoke();
                }));
            }
            catch { }
        }

        private bool _hasLogs;

        /// <summary>有没有产生过日志。没有就先不显示「详细日志」那一块。</summary>
        public bool HasLogs => _hasLogs;

        public void ClearLog()
        {
            _logs.Clear();
            LogsText = string.Empty;
            if (_hasLogs) { _hasLogs = false; RaisePropertyChanged(nameof(HasLogs)); }
        }
        #endregion

        #region 命令
        public RelayCommand StartCommand { get; }
        public RelayCommand OpenOutputFolderCommand { get; }
        public RelayCommand ClearListCommand { get; }
        #endregion

        private List<string> _pendingPaths = new List<string>();

        public void PrepareStart(IEnumerable<string> paths)
        {
            _pendingPaths = paths?.ToList() ?? new List<string>();
        }

        private CompressOptions BuildOptions()
        {
            var mode = CompressMode.Compress;
            uint width = 0;
            uint height = 0;

            // 勾了智能裁切但宽高都没填时，退回纯压缩。
            // 否则会把 width:0 发给 TinyPNG，接口直接报错。
            if (SmartCutEnabled)
            {
                uint.TryParse(ImageWidth, out width);
                uint.TryParse(ImageHeight, out height);
            }
            bool resizeRequested = SmartCutEnabled && (width > 0 || height > 0);

            if (resizeRequested)
            {
                switch (ResizeModeIndex)
                {
                    case 1: mode = CompressMode.Fit; break;
                    case 2: mode = CompressMode.Cover; break;
                    case 3: mode = CompressMode.Thumb; break;
                    default: mode = CompressMode.Scale; break;
                }
            }

            return new CompressOptions
            {
                Mode = mode,
                Width = width,
                Height = height,
                MetaCopyright = MetaCopyright,
                MetaLocation = MetaLocation,
                MetaCreationTime = MetaCreationTime
            };
        }

        /// <summary>
        /// 后台解码缩略图。限制并发数，避免一次拖入几百张图时解码把 CPU 打满、
        /// 拖慢压缩本身和界面响应。
        /// </summary>
        private static void LoadThumbnails(List<ImageItem> items)
        {
            if (items == null || items.Count == 0) return;
            Parallel.ForEach(items,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount)) },
                item => item.LoadThumbnail());
        }

        private async Task StartAsync(List<string> paths)
        {
            try
            {
                if (paths == null || paths.Count == 0) return;

                SyncToMemory(); // 保险：确保列表里最新的 Key 已写进 AppSettings
                var keys = ConfiguredKeys;
                if (keys.Count == 0)
                {
                    MessageBox.Show(Loc.Instance.NoApiKey, Loc.Instance.Tip);
                    SettingRadioButtonIsChecked = true;
                    return;
                }

                _compressor = new ImageCompressor(new KeyPool(keys));

                // Key 列表可能刚改过：让闸门忘掉旧 Key，下次请求强制重新赋值
                TinifyKeyGate.Reset();

                // 构建文件列表（UI 线程）
                Images.Clear();
                ClearLog();
                TotalSavedBytes = 0;
                foreach (var p in paths)
                {
                    var size = GetOriginalSize(p);
                    var item = new ImageItem(p) { OriginalSize = size, Status = ImageStatus.Processing };
                    Images.Add(item);
                    if (size > 5L * 1024 * 1024)
                        AddLog($"! {item.FileName} 约 {ImageItem.FormatSize(size)}，超出 TinyPNG 单文件 5MB 上限，可能会失败");
                }

                // 缩略图：丢到后台解码，不阻塞压缩启动。传快照避免列表被清空后取到空集合
                _ = Task.Run(() => LoadThumbnails(Images.ToList()));

                TotalCount = Images.Count;
                ProcessedCount = 0;
                SucceededCount = 0;
                FailedCount = 0;
                ProgressValue = 0;
                // 用真实进度（ProgressValue）而不是滚动动画，这样进度条能反映实际完成比例
                IsIndeterminate = false;
                IsBusy = true;
                CanCancel = true;
                StatusText = $"0 / {TotalCount}";
                AddLog($"本次共 {TotalCount} 张图片，并发 {Math.Max(1, Concurrency)}，可用 Key {keys.Count} 个");
                var sw = Stopwatch.StartNew();

                var options = BuildOptions();
                int succeeded = 0, failed = 0;

                // 收集「原文件 → 压缩后新文件」的配对，批次结束后再决定要不要替换原文件
                var done = new ConcurrentBag<(string Original, string Output)>();

                // 并发压缩：用 SemaphoreSlim 限流（net48 兼容，避免 Parallel.ForEachAsync 这个 net6+ API）
                var sem = new SemaphoreSlim(Math.Max(1, Concurrency));
                var tasks = Images.Select(async item =>
                {
                    await sem.WaitAsync();
                    try
                    {
                        var res = await _compressor.CompressAsync(item.FullPath, options, CancellationToken.None, AddLog);

                        // 回到 UI 线程更新集合项与统计
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.CompressedSize = res.CompressedSize;
                            item.SavedPercent = res.SavedPercent;
                            item.Status = res.Success ? ImageStatus.Success : ImageStatus.Failed;
                            item.ErrorMessage = res.Success ? string.Empty : res.ErrorMessage;

                            if (res.Success)
                            {
                                Interlocked.Increment(ref succeeded);
                                TotalSavedBytes += Math.Max(0, res.OriginalSize - res.CompressedSize);
                                if (!string.IsNullOrEmpty(res.OutputPath))
                                    done.Add((item.FullPath, res.OutputPath));
                            }
                            else
                            {
                                Interlocked.Increment(ref failed);
                            }

                            if (res.Success)
                                RecordKeyUsage(res.Key, res.CompressionCount);

                            ProcessedCount = succeeded + failed;
                            SucceededCount = succeeded;
                            FailedCount = failed;
                            ProgressValue = TotalCount > 0 ? (double)ProcessedCount / TotalCount : 0;
                            // 主体就是「已处理 / 总数」，失败数只在真的有失败时才追加，避免平时冗余
                            StatusText = failed > 0
                                ? $"{ProcessedCount} / {TotalCount} · 失败 {failed}"
                                : $"{ProcessedCount} / {TotalCount}";
                        });
                    }
                    finally
                    {
                        sem.Release();
                    }
                });
                await Task.WhenAll(tasks);

                // 收尾
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsIndeterminate = false;
                    IsBusy = false;
                    CanCancel = false;
                    ProgressValue = 1;

                    sw.Stop();
                    StatusText = $"✅ 全部完成：{succeeded} 张成功、{failed} 张失败，共节省 {TotalSavedText}";
                    AddLog($"全部完成：{succeeded} 张成功、{failed} 张失败，共节省 {TotalSavedText}（耗时 {sw.Elapsed.TotalSeconds:F1}s）");
                    // 压缩过一次，主窗口开始显示「剩余额度」
                    HasCompressed = true;
                });

                // 压缩完了再决定要不要替换原文件：此时用户能看到实际节省了多少
                await DecideReplaceAsync(done.ToList(), succeeded);

                // 短暂保留 100% 进度条（约 1.5s）后再隐藏
                try { await Task.Delay(1500, CancellationToken.None); } catch { }
                Application.Current.Dispatcher.Invoke(() => ProgressValue = 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
            }
            finally
            {
                IsBusy = false;
                CanCancel = false;
            }
        }

        /// <summary>
        /// 批次结束后决定：压缩结果是覆盖原文件，还是保留成新文件。
        /// 一定要在压缩完成之后才问，因为用户得先看到实际省了多少才做得了判断。
        /// ReplaceAskMode：0=每次询问 1=总是替换 2=总是保留为新文件。
        /// </summary>
        private async Task DecideReplaceAsync(List<(string Original, string Output)> done, int succeeded)
        {
            if (done.Count == 0) return;

            bool replace;
            if (ReplaceAskMode == 1)
            {
                replace = true;
            }
            else if (ReplaceAskMode == 2)
            {
                replace = false;
            }
            else
            {
                // 弹窗必须在 UI 线程
                replace = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var win = new ReplaceAskWindow();
                    win.ShowDialog();

                    if (win.RememberChoice)
                    {
                        // setter 会自动落盘，以后不再问
                        ReplaceAskMode = win.Result ? 1 : 2;
                        AddLog($"已记住选择：以后一律{(win.Result ? "替换原文件" : "保留为新文件")}，不再询问（设置里可改回来）");
                    }
                    return win.Result;
                });
            }

            await Task.Run(() => ApplyReplace(done, replace));
        }

        /// <summary>把压缩结果覆盖回原文件（或什么都不做，保留新文件）</summary>
        private void ApplyReplace(List<(string Original, string Output)> done, bool replace)
        {
            if (!replace)
            {
                AddLog($"已保留原文件，压缩结果另存为「文件名-时间戳」新文件（{done.Count} 个）");
                return;
            }

            int ok = 0;
            foreach (var pair in done)
            {
                try
                {
                    File.Copy(pair.Output, pair.Original, true);
                    TryDelete(pair.Output);
                    ok++;
                }
                catch (Exception ex)
                {
                    AddLog($"✗ 替换 {Path.GetFileName(pair.Original)} 失败：{ex.Message}");
                }
            }

            if (ok > 0)
                AddLog($"已用压缩结果替换 {ok} 个原文件（原文件无法恢复）");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }


        private void OpenOutputFolder()
        {
            if (Images.Count == 0) return;
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Images[0].FullPath);
                if (!string.IsNullOrEmpty(dir))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
            }
        }

        /// <summary>清空文件列表与统计（压缩进行中不允许，避免统计错乱）</summary>
        private void ClearList()
        {
            if (IsBusy)
            {
                MessageBox.Show(Loc.Instance.BusyClearList, Loc.Instance.Tip);
                return;
            }

            Images.Clear();
            TotalCount = 0;
            ProcessedCount = 0;
            SucceededCount = 0;
            FailedCount = 0;
            ProgressValue = 0;
            TotalSavedBytes = 0;
            StatusText = string.Empty;
        }

        private static long GetOriginalSize(string path)
        {
            try { return new System.IO.FileInfo(path).Length; }
            catch { return 0; }
        }
    }
}
