using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace OpticalModuleTestSystem.ViewModels
{
    /// <summary>
    /// MultiValueConverter：根据DDM告警标志与实时告警值，返回对应颜色。
    /// 参数："Alarm" 返回红色，"Warn" 返回橙色；优先级：实时超阈值 > 告警标志
    /// </summary>
    public class AlarmAggregateMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            bool realtime = false;

            if (values != null && values.Length >= 1)
            {
                if (values[0] is bool b0) flag = b0;
                if (values.Length >= 2 && values[1] is bool b1) realtime = b1;
            }

            // 实时告警优先：红色
            if (realtime)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)); // DangerColor

            if (flag)
            {
                var p = parameter as string;
                if (string.Equals(p, "Warn", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00)); // WarningColor
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)); // Alarm -> Danger
            }

            // 默认正常颜色
            return new SolidColorBrush(Color.FromRgb(0x4C, 0xD9, 0x64)); // SuccessColor
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
