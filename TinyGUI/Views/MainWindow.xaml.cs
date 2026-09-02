using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TinyGUI.Properties;
using TinyGUI.ViewModels;
using TinyGUI.Services;

namespace TinyGUI.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainModel _mainModel;

        public MainWindow()
        {
            // 先造好 ViewModel 并挂上 DataContext，再 InitializeComponent。
            // 反过来的话，InitializeComponent 期间 DataContext 还是 null，控件停在默认值
            // （CheckBox 是 false）；等 DataContext 一挂上，双向绑定会把控件的假值回推给
            // ViewModel，把从磁盘读出来的真实设置冲掉。
            var pool = new KeyPool(AppSettings.Current.ApiKeys);
            _mainModel = new MainModel(new ImageCompressor(pool));
            DataContext = _mainModel;
            InitializeComponent();
            // 标题栏带版本号，方便用户/客服一眼确认当前版本。
            // 必须在 InitializeComponent 之后设，否则会被 XAML 的 Title="TinyGUI" 覆盖。
            Title = $"TinyGUI v{App.Version}";

            if (AppSettings.Current.ApiKeys.Count == 0)
            {
                _mainModel.SettingRadioButtonIsChecked = true;
            }

            // 新日志追加时自动滚动到底部
            _mainModel.LogAppended += () =>
            {
                LogTextBox.ScrollToEnd();
                LogTextBox.CaretIndex = LogTextBox.Text.Length;
            };

            // 切语言后文案长度会变，让窗口高度重新贴合内容
            Loc.Instance.PropertyChanged += (s, e) => RefreshHeight();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 确保设置已落盘
            AppSettings.Current.Save();
            base.OnClosed(e);
        }

        /// <summary>
        /// 切换 Tab 或展开/折叠日志后，让窗口高度重新贴合内容。
        /// 窗口用 SizeToContent="Height" 自适应，尺寸变化后需要手动触发一次重新计算。
        /// </summary>
        private void RefreshHeight()
        {
            SizeToContent = SizeToContent.Manual;
            UpdateLayout();
            SizeToContent = SizeToContent.Height;
        }

        private void ModeRadioButton_OnChecked(object sender, RoutedEventArgs e) => RefreshHeight();

        private void LogExpander_OnExpanded(object sender, RoutedEventArgs e) => RefreshHeight();

        private void StartWith(IEnumerable<string> paths)
        {
            if (_mainModel.IsBusy)
            {
                MessageBox.Show(Loc.Instance.BusyAdding, Loc.Instance.Tip);
                return;
            }

            var images = CollectImages(paths);
            if (images.Count == 0)
            {
                MessageBox.Show(Loc.Instance.NoImageFound, Loc.Instance.Tip);
                return;
            }

            if (AppSettings.Current.ApiKeys.Count == 0)
            {
                MessageBox.Show(Loc.Instance.NoApiKey, Loc.Instance.Tip);
                _mainModel.SettingRadioButtonIsChecked = true;
                return;
            }

            _mainModel.PrepareStart(images);
            _mainModel.StartCommand.Execute(null);
        }

        /// <summary>把拖入/选中的路径展开为图片列表：目录会递归扫描子目录</summary>
        private static List<string> CollectImages(IEnumerable<string> paths)
        {
            var result = new List<string>();
            if (paths == null) return result;

            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                        result.AddRange(Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories).Where(IsImage));
                    else if (File.Exists(p) && IsImage(p))
                        result.Add(p);
                }
                catch
                {
                    // 无权限访问的子目录直接跳过，不中断整体扫描
                }
            }
            return result;
        }

        /// <summary>单击拖拽区 = 点了一个「选择图片」按钮</summary>
        private void DropArea_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            e.Handled = true;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = Loc.Instance.SelectImagesTitle,
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp|所有文件|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
                StartWith(dlg.FileNames);
        }

        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (dropped != null && dropped.Length > 0)
                    StartWith(dropped);
            }
        }

        private static bool IsImage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".webp" || ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        }

        private void OpenOutputButton_OnClick(object sender, RoutedEventArgs e)
            => _mainModel.OpenOutputFolderCommand.Execute(null);

        private void VersionHyperlink_OnClick(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/ttww1111/TinyGUI") { UseShellExecute = true });

        private void TinifyHyperlink_OnClick(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(TinyGUI.Properties.Resources.KeyUrl) { UseShellExecute = true });

        private void RedisantHyperlink_OnClick(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(TinyGUI.Properties.Resources.Redisant) { UseShellExecute = true });

        private void CopyLogButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = _mainModel.LogsText;
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(Loc.Instance.NoLogs, Loc.Instance.Tip);
                return;
            }
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                MessageBox.Show(Loc.Instance.CopyFailed, Loc.Instance.Tip);
            }
        }
    }
}
