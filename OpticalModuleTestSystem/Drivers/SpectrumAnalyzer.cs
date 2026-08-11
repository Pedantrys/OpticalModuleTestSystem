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
            // 适配常见设备的分辨率带宽命令（部分设备使用 SENS:BAND:RES，部分使用 SENS:BW）
            _gpib.Write($"SENS:BAND:RES {rbw}");

            // 2. 加载指定测试模板并开启模板检测
            // 兼容不同设备的模板命令前缀（如 MS9740A 等使用 CALC:TEMP:...）
            _gpib.Write($"CALC:TEMP:LOAD \"{templateName}\"");
            _gpib.Write("CALC:TEMP:STAT ON");

            // 3. 基础初始化：中心波长、参考电平、扫描带宽（跨度）
            _gpib.Write("SENS:WAV:CENT 1550NM");
            _gpib.Write("SENS:WAV:SPAN 20NM");
            _gpib.Write("DISP:WIND:TRAC:Y:RLEV -10DBM");

            // 4. 触发与扫描控制：关闭连续扫描，使用立即触发并执行一次扫描，确保测量可重复/同步
            _gpib.Write("INIT:CONT OFF");
            _gpib.Write("TRIG:MODE IMM");
            _gpib.Write("INIT:IMM");

        }
    }
}
