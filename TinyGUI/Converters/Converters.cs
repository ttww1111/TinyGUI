using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TinyGUI.Converters
{
    /// <summary>bool -> Visibility（true=Visible，false=Collapsed）</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>空字符串/Null -> Collapsed，否则 Visible</summary>
    public class EmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => null;
    }

    /// <summary>
    /// int == parameter(int) -> bool。
    /// 用于把分段选择器（RadioButton 组）的 int 索引绑定成每个按钮的 IsChecked。
    /// </summary>
    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return int.TryParse(value.ToString(), out int v)
                   && int.TryParse(parameter.ToString(), out int p)
                   && v == p;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value && parameter != null && int.TryParse(parameter.ToString(), out int p))
                return p;
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>bool -> double 透明度（true=1，false=0）。用于两个 Tab 共用同一格、用透明度切换显隐。</summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? 1d : 0d;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d && d > 0.5;
    }

    /// <summary>
    /// 把 Border 的 ActualWidth / ActualHeight / 圆角半径 转成圆角矩形几何（RectangleGeometry），
    /// 用来给分段胶囊做真正的圆角裁剪——WPF 的 ClipToBounds 只裁剪矩形边界、不按 CornerRadius 裁剪，
    /// 所以必须显式设置 Clip 才能把两端溢出直角裁成圆角。
    /// 用法：
    ///   <Border.Clip>
    ///     <MultiBinding Converter="{StaticResource RectClipConverter}">
    ///       <Binding RelativeSource="{RelativeSource Self}" Path="ActualWidth" />
    ///       <Binding RelativeSource="{RelativeSource Self}" Path="ActualHeight" />
    ///       <Binding Source="20" />
    ///     </MultiBinding>
    ///   </Border.Clip>
    /// </summary>
    public class RectClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3) return null;
            if (!double.TryParse(values[0]?.ToString(), out double w) ||
                !double.TryParse(values[1]?.ToString(), out double h) ||
                !double.TryParse(values[2]?.ToString(), out double r))
                return null;
            if (w <= 0 || h <= 0) return null;
            return new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => new object[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
    }

    /// <summary>分段按钮的位置（First/Middle/Last）-> 圆角。直接绑到 Border.CornerRadius，
    /// 比用 ControlTemplate.Trigger 比较附加属性更可靠。</summary>
}
