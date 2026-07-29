using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Configs
{
    /// <summary>
    /// 系统全局配置，速率映射、校准系数、仪器模板均在此处统一维护
    /// </summary>
    public static class SystemConfig
    {
        // ====================== 光谱仪模板配置（预留，确认名称后直接修改）======================
        /// <summary>
        /// 光谱分析仪默认测试模板名称
        /// </summary>
        public const string OsaDefaultTemplate = "DEFAULT_OPTICAL_MASK";

        // ====================== 模块速率 → 物理线速率映射（标准值）======================
        /// <summary>
        /// 模块速率等级 → 实际物理线速率（单位：Gbps）
        /// 10G  → 10.3125 （64b/66b编码标准）
        /// 25G  → 25.78125（64b/66b编码标准）
        /// 100G → 103.125 （预留扩展）
        /// </summary>
        public static readonly Dictionary<string, double> ModuleRateMap = new()
        {
            ["10G"] = 10.3125,
            ["25G"] = 25.78125,
            ["100G"] = 103.125
        };

        // ====================== 光功率计校准系数（数据调正）======================
        /// <summary>
        /// 不同速率下的光功率计校准补偿系数（数据调正）
        /// </summary>
        public static readonly Dictionary<string, double> PowerMeterCalFactor = new()
        {
            ["10G"] = 1.02,
            ["25G"] = 1.05,
            ["100G"] = 1.08
        };
    }
}
