using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 光谱分析仪驱动
    /// </summary>
    public class SpectrumAnalyzer
    {
        private readonly GpibCommunicator _gpib;

        public SpectrumAnalyzer(GpibCommunicator gpib)
        {
            _gpib = gpib;
        }

        /// <summary>
        /// 初始化：速率匹配带宽 + 加载测试模板
        /// </summary>
        /// <param name="lineRate">实际线速率</param>
        /// <param name="templateName">模板名称</param>
        public void Init(double lineRate, string templateName)
        {
            // 1. 根据速率自动匹配分辨率带宽
            string rbw = lineRate switch
            {
                >= 100 => "0.02NM",
                >= 25 => "0.05NM",
                >= 10 => "0.1NM",
                _ => "0.1NM"
            };
            _gpib.Write($"SENS:BAND:RES {rbw}");

            // 2. 加载指定测试模板 + 开启模板检测
            _gpib.Write($"CALC:MARK:TEMP:LOAD \"{templateName}\"");
            _gpib.Write("CALC:MARK:TEMP:STAT ON");

            // 3. 基础初始化：中心波长、参考电平
            _gpib.Write("SENS:WAV:CENT 1550NM");
            _gpib.Write("DISP:WIND:TRAC:Y:RLEV -10DBM");

        }
    }
}
