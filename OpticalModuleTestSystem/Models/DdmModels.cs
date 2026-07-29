using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpticalModuleTestSystem.Models
{
    /// <summary>
    /// 文件名称：DdmModels.cs
    /// 功能描述：模块上报信息类
    /// </summary>
    /// <author>hui.chen</author>
    /// <createDate>2026.7.8</createDate>
    /// <version>1.0.0</version>
    
    /// <summary>
    /// 光模块基础信息（A0页）
    /// </summary>
    public class DdmModuleInfo
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string DateCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// DDM实时监控值
    /// </summary>
    public class DdmRealTimeData
    {
        public double Temperature { get; set; }
        public double Voltage { get; set; }
        public double BiasCurrent { get; set; }
        public double TxPower { get; set; }
        public double RxPower { get; set; }
    }

    /// <summary>
    /// DDM告警阈值与状态标志
    /// </summary>
    public class DdmAlarmData
    {
        // 阈值：索引0-3分别对应 高告警、高警告、低警告、低告警
        public double[] TempThresholds { get; set; } = new double[4];
        public double[] VoltageThresholds { get; set; } = new double[4];
        public double[] BiasThresholds { get; set; } = new double[4];
        public double[] TxPowerThresholds { get; set; } = new double[4];
        public double[] RxPowerThresholds { get; set; } = new double[4];

        // 告警状态灯：共20位，true=告警(红)，false=正常(绿)
        public bool[] AlarmFlags { get; set; } = new bool[20];
    }

    /// <summary>
    /// 中兴扩展告警（A2页自定义区域）
    /// </summary>
    public class DdmExtAlarmData
    {
        public bool IsSupported { get; set; }
        public double Temp { get; set; }
        public double Bias { get; set; }
        public double TxPower { get; set; }
        public double RxPower { get; set; }

        public string TempFlag { get; set; } = "未知";
        public string BiasFlag { get; set; } = "未知";
        public string TxPowerFlag { get; set; } = "未知";
        public string RxPowerFlag { get; set; } = "未知";

        public Brush TempFlagBrush { get; set; } = Brushes.Black;
        public Brush BiasFlagBrush { get; set; } = Brushes.Black;
        public Brush TxPowerFlagBrush { get; set; } = Brushes.Black;
        public Brush RxPowerFlagBrush { get; set; } = Brushes.Black;
    }

}
