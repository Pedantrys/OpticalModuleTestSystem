using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 光功率计驱动
    /// </summary>
    public class OpticalPowerMeter
    {
        private readonly GpibCommunicator _gpib;
        public double CurrentCalFactor { get; private set; } = 1.0;

        public OpticalPowerMeter(GpibCommunicator gpib)
        {
            _gpib = gpib;
        }

        /// <summary>
        /// 初始化：设置线速率 + 应用校准系数（数据调正）
        /// </summary>
        /// <param name="lineRate">实际线速率，单位Gbps</param>
        /// <param name="calibrationFactor">校准补偿系数</param>
        public void Init(double lineRate, double calibrationFactor)
        {
            // 1. 设置仪器速率档位（部分 OPM 使用 SENS:RATE 或 SENS:WAV:RATE）
            _gpib.Write($"SENS:RATE {lineRate:F4}");

            // 2. 应用数据校准系数（数据调正），并记录当前系数
            CurrentCalFactor = calibrationFactor;
            // 大多数 OPM 使用 CORR:GAIN 或 CORR:FACT，根据设备手册都可接受
            _gpib.Write($"SENS:CORR:GAIN {calibrationFactor:F3}");

            // 3. 基础配置：单位设为 dBm，自动量程/自动灵敏度开启
            _gpib.Write("SENS:POW:UNIT DBM");
            _gpib.Write("SENS:POW:RANG:AUTO ON");

            // 4. 触发/扫描配置：确保仪器处于单次测量模式并准备就绪
            _gpib.Write("INIT:CONT OFF"); // 关闭连续采集
            _gpib.Write("TRIG:COUN 1");
            _gpib.Write("INIT:IMM");
        }

        /// <summary>
        /// 读取当前功率（dBm），并应用校准系数
        /// </summary>
        /// <returns>dBm 值，读取失败返回 NaN</returns>
        public double ReadPowerDbm()
        {
            string s = _gpib.Query("READ:POW?");
            if (string.IsNullOrWhiteSpace(s)) s = _gpib.Query(":POW?");
            if (double.TryParse(s, out double val))
            {
                return val + Math.Log10(CurrentCalFactor) * 10; // 将校准因子应用为 dB 调整
            }
            return double.NaN;
        }
    }
}
