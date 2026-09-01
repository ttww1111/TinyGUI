using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace TinyGUI.Models
{
    /// <summary>
    /// 设置面板里的一行 API Key：可编辑的密钥 + 该 Key 的剩余额度。
    /// 用列表而不是多行文本框，是为了能在每个 Key 旁边直接显示它的剩余次数。
    /// </summary>
    public class ApiKeyItem : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        private string _remainingText = string.Empty;
        private bool _duplicate;

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(Masked));
                OnPropertyChanged(nameof(IsValidFormat));
                OnPropertyChanged(nameof(ValueColor));
                OnPropertyChanged(nameof(FormatHint));
            }
        }

        /// <summary>右侧显示的额度文字，由 MainModel 统一刷新</summary>
        public string RemainingText
        {
            get => _remainingText;
            set
            {
                if (_remainingText == value) return;
                _remainingText = value;
                OnPropertyChanged(nameof(RemainingText));
                OnPropertyChanged(nameof(RemainingHint));
            }
        }

        /// <summary>放进输入框右侧的提示文字：空 Key 或未使用时不显示</summary>
        public string RemainingHint
            => string.IsNullOrEmpty(_remainingText) || string.Equals(_remainingText, "待填写", StringComparison.Ordinal)
                ? string.Empty
                : _remainingText;

        /// <summary>和前面某一行重复了（重复项不会参与轮换，等于白填）</summary>
        public bool Duplicate
        {
            get => _duplicate;
            set
            {
                if (_duplicate == value) return;
                _duplicate = value;
                OnPropertyChanged(nameof(Duplicate));
                OnPropertyChanged(nameof(ValueColor));
            }
        }

        public bool IsValidFormat
        {
            get
            {
                var v = (_value ?? string.Empty).Trim();
                return v.Length == 0 || Regex.IsMatch(v, "^[a-zA-Z0-9]{32}$");
            }
        }

        public string ValueColor => Duplicate || !IsValidFormat ? "#E0533D" : "#2D3748";

        /// <summary>格式校验提示：非空且不是 32 位字母/数字时给出红字提示；其余情况为空</summary>
        public string FormatHint
        {
            get
            {
                var v = (_value ?? string.Empty).Trim();
                if (v.Length == 0) return string.Empty;
                return IsValidFormat ? string.Empty : "需 32 位字母和数字";
            }
        }

        /// <summary>日志/提示里用的脱敏形式，只留后 4 位</summary>
        public string Masked
        {
            get
            {
                var v = (_value ?? string.Empty).Trim();
                return v.Length == 0 ? "(空)" : (v.Length > 4 ? "****" + v.Substring(v.Length - 4) : v);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
