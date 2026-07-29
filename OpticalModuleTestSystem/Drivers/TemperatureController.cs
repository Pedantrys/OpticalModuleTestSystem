using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 温控平台驱动
    /// </summary>
    public class TemperatureController
    {
        private readonly GpibCommunicator _gpib;
        public double TargetTemp { get; private set; }

        public TemperatureController(GpibCommunicator gpib)
        {
            _gpib = gpib;
        }

        /// <summary>
        /// 初始化：设置目标温度25℃，开启TEC输出
        /// </summary>
        public void InitTo25C()
        {
            TargetTemp = 25.0;
            _gpib.Write($"SOUR:TEMP {TargetTemp:F1}");
            _gpib.Write("OUTP:TEC ON");
        }
    }
}
