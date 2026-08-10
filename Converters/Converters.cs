using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoClicker.Converters
{
    /// <summary>
    /// 枚举转布尔值转换器 - 用于 RadioButton 绑定
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
                return parameter;
            return System.Windows.Data.Binding.DoNothing;
        }
    }

    /// <summary>
    /// 枚举转可见性转换器 - 用于面板切换
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return System.Windows.Visibility.Collapsed;
            return value.Equals(parameter) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 布尔取反转换器
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return System.Windows.Data.Binding.DoNothing;
        }
    }

    /// <summary>
    /// 布尔转不透明度转换器 - 用于热键提示置灰
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? 1.0 : 0.5;
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 字符串为空转可见性转换器
    /// </summary>
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
                return System.Windows.Visibility.Visible;
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 布尔转画刷转换器 - 用于状态指示点颜色
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen)
                         : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 布尔转可见性转换器 - 用于 TreeViewItem 可见性绑定
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 16进制字符串转画刷 - 用于 WorkflowActionViewModel.BackgroundHex 绑定
    /// </summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(s);
                    return new System.Windows.Media.SolidColorBrush(color);
                }
                catch
                {
                    // 解析失败返回透明画刷
                }
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 整数取反布尔 - 用于 "未选中时禁用按钮" 等场景
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
