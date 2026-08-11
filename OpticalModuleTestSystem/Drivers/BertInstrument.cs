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
            // 1. 设置工作速率（多数 BERT 使用 SOUR:RATE 或 RATE）
            _gpib.Write($"SOUR:RATE {lineRate:F4}");

            // 2. 初始化灵敏度/判决阈值，尝试一次自动校准
            _gpib.Write("SENS:THR:AUTO ONCE");

            // 3. 配置计数器与触发：复位计数器并等待单次触发
            _gpib.Write("STAT:COUNT:RESET");
            _gpib.Write("TRIG:MODE IMM");
            _gpib.Write("INIT:IMM");
        }
    }
}
