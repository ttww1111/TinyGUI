using System.Windows;

namespace TinyGUI.Views
{
    /// <summary>
    /// 批次压缩结束后询问是否替换原文件。
    /// 关闭后读 Result（true=替换）与 RememberChoice（true=记进设置，以后不再问）。
    /// 直接关窗 / Esc / 点「保留为新文件」都等价于 Result=false。
    /// </summary>
    public partial class ReplaceAskWindow : Window
    {
        public bool Result { get; private set; }
        public bool RememberChoice { get; private set; }

        public ReplaceAskWindow()
        {
            InitializeComponent();
            Owner = Application.Current?.MainWindow;
        }

        private void ReplaceButton_OnClick(object sender, RoutedEventArgs e) => Finish(true);

        private void KeepButton_OnClick(object sender, RoutedEventArgs e) => Finish(false);

        private void Finish(bool replace)
        {
            Result = replace;
            RememberChoice = RememberCheckBox.IsChecked == true;
            Close();
        }
    }
}
