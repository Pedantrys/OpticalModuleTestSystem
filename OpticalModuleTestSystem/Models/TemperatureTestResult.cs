using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    public class TemperatureTestResult
    {
        /// <summary>
        /// 测试温度类型（常温/低温/高温）
        /// </summary>
        public string TempType { get; set; } = string.Empty;

        /// <summary>
        /// 目标温度（℃）
        /// </summary>
        public double TargetTemp { get; set; }

        /// <summary>
        /// 稳定后实际温度（℃）
        /// </summary>
        public double StableTemp { get; set; }

        /// <summary>
        /// 实时监测值-温度（℃）
        /// </summary>
        public double Temp { get; set; }

        /// <summary>
        /// 实时监测值-电压（V）
        /// </summary>
        public double Volt { get; set; }

        /// <summary>
        /// 实时监测值-偏置电流（mA）
        /// </summary>
        public double Bias { get; set; }

        /// <summary>
        /// 实时监测值-发射光功率（dBm）
        /// </summary>
        public double TxPower { get; set; }

        /// <summary>
        /// 实时监测值-接收光功率（dBm）
        /// </summary>
        public double RxPower { get; set; }

        /// <summary>
        /// Keysight 86100D 发射端（Tx）眼图数据
        /// </summary>
        public string TxEyeDiagramData { get; set; } = string.Empty;

        /// <summary>
        /// Keysight 86100D 接收端（Rx）眼图数据
        /// </summary>
        public string RxEyeDiagramData { get; set; } = string.Empty;

        /// <summary>
        /// Anritsu MS9740A 光谱数据
        /// </summary>
        public string SpectrumData { get; set; } = string.Empty;

        /// <summary>
        /// 测试时间
        /// </summary>
        public DateTime TestTime { get; set; }

        // ---------------- EXFO IQS-610P 衰减测试数据 ----------------
        /// <summary>
        /// 0dB衰减时：模块上报RxPower (dBm)
        /// </summary>
        public double RxPower_0dB_Module { get; set; }
        /// <summary>
        /// 0dB衰减时：EXFO设备读取值 (dBm)
        /// </summary>
        public double RxPower_0dB_EXFO { get; set; }

        /// <summary>
        /// 3.5dB衰减时：模块上报RxPower (dBm)
        /// </summary>
        public double RxPower_3_5dB_Module { get; set; }
        /// <summary>
        /// 3.5dB衰减时：EXFO设备读取值 (dBm)
        /// </summary>
        public double RxPower_3_5dB_EXFO { get; set; }

        /// <summary>
        /// 7dB衰减时：模块上报RxPower (dBm)
        /// </summary>
        public double RxPower_7dB_Module { get; set; }
        /// <summary>
        /// 7dB衰减时：EXFO设备读取值 (dBm)
        /// </summary>
        public double RxPower_7dB_EXFO { get; set; }

        // ---------------- Anritsu MP1900A 误码仪测试数据 ----------------
        /// <summary>
        /// 误码率=5E-5 时的EXFO光功率 (dBm)
        /// </summary>
        public double Ber_5E5_EXFO_Power { get; set; }
        /// <summary>
        /// 误码率消失时的EXFO光功率 (dBm)
        /// </summary>
        public double Ber_Disappear_EXFO_Power { get; set; }
        /// <summary>
        /// 误码率重现时的EXFO光功率 (dBm)
        /// </summary>
        public double Ber_Reappear_EXFO_Power { get; set; }
    }
}
