using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace OpticalModuleTestSystem.ViewModels
{
    /// <summary>
    /// 布尔值转告警状态颜色转换器
    /// </summary>
    public class BoolToAlarmBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool triggered && triggered)
            {
                // ConverterParameter="Alarm" → 红色, "Warn" → 橙色
                return parameter?.ToString() == "Alarm"
                    ? new SolidColorBrush(Colors.Red)
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00)); // WarningColor
            }
            // 正常 → LawnGreen / SuccessColor
            return new SolidColorBrush(Color.FromRgb(0x4C, 0xD9, 0x64));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
