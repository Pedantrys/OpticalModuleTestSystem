using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 误码仪驱动 
    /// </summary>
    public class BertInstrument
    {
        private readonly GpibCommunicator _gpib;

        public BertInstrument(GpibCommunicator gpib)
        {
            _gpib = gpib;
        }

        /// <summary>
        /// 初始化：设置速率 + 自动校准灵敏度判决阈值
        /// </summary>
        public void Init(double lineRate)
        {
            // 1. 设置工作速率
            _gpib.Write($"SOUR:RATE {lineRate:F4}");

            // 2. 初始化灵敏度：自动执行一次阈值校准
            _gpib.Write("SENS:THR:AUTO ONCE");

            // 3. 复位误码计数器
            _gpib.Write("SENS:COUNT:RES");
        }
    }
}
