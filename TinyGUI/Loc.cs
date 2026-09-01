using System.ComponentModel;
using TinyGUI.Properties;

namespace TinyGUI
{
    /// <summary>
    /// 本地化代理。
    ///
    /// XAML 里写 {x:Static p:Resources.Xxx} 是编译期静态绑定，运行时改
    /// Resources.Culture 不会让界面刷新——以前只能靠「关掉窗口再 new 一个」来生效，
    /// 表现就是切换语言时程序像重启了一样。
    ///
    /// 这里把资源包一层可通知的 Binding 源：切语言时调 Refresh()，
    /// 用 PropertyChanged(null) 通知 WPF「所有属性都变了」，界面原地更新、不重建窗口。
    ///
    /// 注：本类直接走 ResourceManager.GetString，不依赖 Resources.Designer.cs 重新生成，
    /// 因此往 .resx 加新 key 后无需手动改 Designer.cs 也能立即生效。
    /// </summary>
    public class Loc : INotifyPropertyChanged
    {
        public static Loc Instance { get; } = new Loc();

        private static string S(string name) => Resources.ResourceManager.GetString(name, Resources.Culture);

        public string Compression => S("Compression");
        public string Settings => S("Settings");
        public string DropImageHere => S("DropImageHere");
        public string PreserveMetadata => S("PreserveMetadata");
        public string Copyright => S("Copyright");
        public string Location => S("Location");
        public string CreationTime => S("CreationTime");
        public string Lang => S("Lang");

        // —— 压缩区 ——
        public string DropHint => S("DropHint");
        public string OpenOutputFolder => S("OpenOutputFolder");
        public string ClearList => S("ClearList");
        public string LogDetails => S("LogDetails");
        public string CopyAllLogs => S("CopyAllLogs");

        // —— 设置区 ——
        public string ApiKeysHeader => S("ApiKeysHeader");
        public string ApiKeysTip => S("ApiKeysTip");
        public string ApiKeyTip => S("ApiKeyTip");
        public string QuotaLabel => S("QuotaLabel");
        public string Concurrency => S("Concurrency");
        public string CompressResult => S("CompressResult");
        public string AskEachTime => S("AskEachTime");
        public string AskEachTimeTip => S("AskEachTimeTip");
        public string ReplaceOriginal => S("ReplaceOriginal");
        public string ReplaceOriginalTip => S("ReplaceOriginalTip");
        public string KeepNewFile => S("KeepNewFile");
        public string KeepNewFileTip => S("KeepNewFileTip");
        public string SmartCut => S("SmartCut");
        public string WidthLabel => S("WidthLabel");
        public string HeightLabel => S("HeightLabel");

        // —— 弹窗 / 状态 ——
        public string Tip => S("Tip");
        public string BusyAdding => S("BusyAdding");
        public string NoImageFound => S("NoImageFound");
        public string NoApiKey => S("NoApiKey");
        public string SelectImagesTitle => S("SelectImagesTitle");
        public string NoLogs => S("NoLogs");
        public string CopyFailed => S("CopyFailed");
        public string BusyClearList => S("BusyClearList");
        public string NotFilled => S("NotFilled");
        public string RemainingTimes => S("RemainingTimes");
        public string NotSet => S("NotSet");

        // —— 替换询问弹窗 ——
        public string ReplaceAskTitle => S("ReplaceAskTitle");
        public string ReplaceAskText => S("ReplaceAskText");
        public string RememberChoice => S("RememberChoice");
        public string ReplaceKeepButton => S("ReplaceKeepButton");
        public string ReplaceOverwriteButton => S("ReplaceOverwriteButton");

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>通知界面：所有文案都变了（null 表示全部属性）</summary>
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
