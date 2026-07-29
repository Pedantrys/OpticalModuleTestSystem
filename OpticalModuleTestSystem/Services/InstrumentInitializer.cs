using OpticalModuleTestSystem.Configs;
using OpticalModuleTestSystem.Drivers;
using OpticalModuleTestSystem.Models;
using System;

namespace OpticalModuleTestSystem.Services
{
    public class InstrumentInitializer
    {
        public bool Initialize(InstrumentInfo instrument)
        {
            if (instrument == null) return false;
            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(instrument.GpibAddress)) return false;

            // 根据仪器型号自动匹配初始化逻辑
            string model = instrument.Model.ToUpper();
            try
            {
                if (model.Contains("AST-545"))
                {
                    return gpib.InitTempControllerTo25C();
                }
                // 其他仪器通用初始化可在此扩展
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 按模块速率等级，批量初始化所有已扫描到的仪器
        /// </summary>
        /// <param name="instruments">已扫描的仪器列表</param>
        /// <param name="moduleRateLevel">模块速率等级：10G/25G/100G</param>
        /// <returns>初始化结果日志</returns>
        public async Task<(bool AllSuccess, List<string> Logs)> InitializeAllByRateAsync(
            IEnumerable<InstrumentInfo> instruments,
            string moduleRateLevel)
        {
            List<string> logs = new();
            bool allSuccess = true;

            // 参数校验 + 映射匹配
            if (!SystemConfig.ModuleRateMap.TryGetValue(moduleRateLevel, out double lineRate))
            {
                logs.Add($"不支持的模块速率等级：{moduleRateLevel}");
                return (false, logs);
            }
            SystemConfig.PowerMeterCalFactor.TryGetValue(moduleRateLevel, out double calFactor);

            await Task.Run(() =>
            {
                foreach (var inst in instruments)
                {
                    if (!inst.IsTargetDevice) continue;

                    using var gpib = new GpibCommunicator();
                    if (!gpib.Connect(inst.GpibAddress))
                    {
                        logs.Add($"{inst.Name} 连接失败，跳过初始化");
                        allSuccess = false;
                        continue;
                    }

                    string model = inst.Model.ToUpper();
                    bool ok = true;

                    // 1. 温控平台 → 初始化到25℃
                    if (model.Contains("AST-545"))
                    {
                        ok = gpib.InitTempControllerTo25C();
                        logs.Add(ok ? $"{inst.Name} 初始化完成：目标温度25℃" : $"{inst.Name} 温控初始化失败");
                    }
                    // 2. EXFO光功率/衰减器 → 速率+校准系数
                    else if (model.Contains("IQS-3150") || model.Contains("IQS600"))
                    {
                        ok = gpib.InitPowerMeter(lineRate, calFactor);
                        logs.Add(ok ? $"{inst.Name} 初始化完成：速率{lineRate:F4}Gbps，校准系数{calFactor:F3}" : $"{inst.Name} 光功率模块初始化失败");
                    }
                    // 3. 误码仪 → 速率+灵敏度校准
                    else if (model.Contains("MP1900A"))
                    {
                        ok = gpib.InitBert(lineRate);
                        logs.Add(ok ? $"{inst.Name} 初始化完成：速率{lineRate:F4}Gbps，灵敏度已校准" : $"{inst.Name} 误码仪初始化失败");
                    }
                    // 4. 光谱仪 → 速率带宽+模板
                    else if (model.Contains("MS9740A"))
                    {
                        ok = gpib.InitSpectrumAnalyzer(lineRate, SystemConfig.OsaDefaultTemplate);
                        logs.Add(ok ? $"{inst.Name} 初始化完成：带宽匹配速率，模板已加载" : $"{inst.Name} 光谱仪初始化失败");
                    }
                    // 5. 示波器 → 基础复位
                    else if (model.Contains("86100D"))
                    {
                        gpib.Write("*RST");
                        logs.Add($"{inst.Name} 复位完成");
                    }

                    if (!ok) allSuccess = false;
                }
            });

            return (allSuccess, logs);
        }
    }
}
