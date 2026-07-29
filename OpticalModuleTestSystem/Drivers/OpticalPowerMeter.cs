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
            // 1. 设置仪器速率档位
            _gpib.Write($"SENS:RATE {lineRate:F4}");

            // 2. 应用数据校准系数（数据调正）
            CurrentCalFactor = calibrationFactor;
            _gpib.Write($"SENS:CORR:GAIN {calibrationFactor:F3}");

            // 3. 基础配置：dBm单位 + 自动量程
            _gpib.Write("SENS:POW:UNIT DBM");
            _gpib.Write("SENS:POW:RANG:AUTO ON");
        }
    }
}
