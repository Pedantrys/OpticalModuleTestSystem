using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 86100D 模块槽位配置（根据你们实验室实际硬件固定配置）
    /// </summary>
    public class ScopeModuleConfig
    {
        /// <summary>10G 模块槽位起始通道（86105C 在左槽 = 1）</summary>
        public int Module10GBaseChannel { get; set; } = 1;  // CH1/CH2

        /// <summary>25G 模块槽位起始通道（86105D 在右槽 = 3）</summary>
        public int Module25GBaseChannel { get; set; } = 3;  // CH3/CH4

        /// <summary>Tx 在模块内的偏移（0=第一个通道，1=第二个通道）</summary>
        public int TxOffset { get; set; } = 0;  // 1A/3A

        /// <summary>Rx 在模块内的偏移（0=第一个通道，1=第二个通道）</summary>
        public int RxOffset { get; set; } = 1;  // 2A/4A
    }
}
