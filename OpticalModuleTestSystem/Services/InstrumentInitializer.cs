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
            // 在连接后执行自检：读取 IDN、清除状态并查询错误
            try
            {
                var idnMain = gpib.Identify() ?? string.Empty;
                try
                {
                    App.Current.Dispatcher.Invoke(() => instrument.IdnString = idnMain);
                }
                catch
                {
                    instrument.IdnString = idnMain;
                }
                // 清除状态寄存器并检查系统错误队列
                gpib.ClearStatus();
                var sysErr = gpib.QuerySystemError();
                if (!string.IsNullOrWhiteSpace(sysErr) && !sysErr.StartsWith("0,"))
                {
                    // 如果有错误，记录到调试输出
                    System.Diagnostics.Debug.WriteLine($"Device {instrument.GpibAddress} reported error: {sysErr}");
                }

                // 根据仪器型号自动匹配初始化逻辑
                string model = instrument.Model.ToUpper();
                if (model.Contains("ATS-545"))
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
        public async Task<(bool AllSuccess, List<string> Logs, System.Collections.Generic.Dictionary<int, bool> PerInstrumentResult)> InitializeAllByRateAsync(
            IEnumerable<InstrumentInfo> instruments,
            string moduleRateLevel,
            string selectedPackage,
            string selectedProtocol,
            string selectedRate,
            string selectedModulation)
        {
            List<string> logs = new();
            bool allSuccess = true;
            var perResult = new System.Collections.Generic.Dictionary<int, bool>();

            // 参数校验 + 映射匹配
            if (!SystemConfig.ModuleRateMap.TryGetValue(moduleRateLevel, out double lineRate))
            {
                logs.Add($"不支持的模块速率等级：{moduleRateLevel}");
                return (false, logs, perResult);
            }
            SystemConfig.PowerMeterCalFactor.TryGetValue(moduleRateLevel, out double calFactor);

            // 异步批量初始化，包含重试机制以提高稳定性
            await Task.Run(() =>
            {
                const int MAX_TRIES = 3;
                const int RETRY_DELAY_MS = 800;

                foreach (var inst in instruments)
                {
                    if (!inst.IsTargetDevice) continue;

                    bool initOk = false;
                    string model = (inst.Model ?? string.Empty).ToUpper();

                    for (int attempt = 1; attempt <= MAX_TRIES && !initOk; attempt++)
                    {
                        using var gpib = new GpibCommunicator();
                        if (!gpib.Connect(inst.GpibAddress))
                        {
                            logs.Add($"{inst.Name} 第{attempt}次连接失败");
                            if (attempt < MAX_TRIES) System.Threading.Thread.Sleep(RETRY_DELAY_MS);
                            continue;
                        }

                        // 连接成功后先做自检：读取 IDN、清除状态并获取系统错误/状态字
                        try
                        {
                            var idn = gpib.Identify() ?? string.Empty;
                            try
                            {
                                App.Current.Dispatcher.Invoke(() => inst.IdnString = idn);
                            }
                            catch
                            {
                                inst.IdnString = idn;
                            }
                            logs.Add($"{inst.Name} IDN: {idn}");
                            gpib.ClearStatus();
                            var syserr = gpib.QuerySystemError();
                            if (!string.IsNullOrWhiteSpace(syserr) && !syserr.StartsWith("0,"))
                                logs.Add($"{inst.Name} 系统错误: {syserr}");
                            var stb = gpib.GetStatusByte();
                            if (stb >= 0) logs.Add($"{inst.Name} 状态字节: {stb}");
                        }
                        catch (Exception ex)
                        {
                            logs.Add($"{inst.Name} 自检异常: {ex.Message}");
                        }

                        bool ok = true;

                        try
                        {
                            if (model.Contains("ATS-545"))
                            {
                                ok = gpib.InitTempControllerTo25C();
                                logs.Add(ok ? $"{inst.Name} 初始化完成：目标温度25℃" : $"{inst.Name} 温控初始化失败 (尝试{attempt})");
                            }
                            else if (model.Contains("IQS-610P") || model.Contains("IQS600"))
                            {
                                ok = gpib.InitPowerMeter(lineRate, calFactor);
                                logs.Add(ok ? $"{inst.Name} 初始化完成：速率{lineRate:F4}Gbps，校准系数{calFactor:F3}" : $"{inst.Name} 光功率模块初始化失败 (尝试{attempt})");
                            }
                            else if (model.Contains("MP1900A"))
                            {
                                ok = gpib.InitBert(lineRate);
                                logs.Add(ok ? $"{inst.Name} 初始化完成：速率{lineRate:F4}Gbps，灵敏度已校准" : $"{inst.Name} 误码仪初始化失败 (尝试{attempt})");
                            }
                            else if (model.Contains("MS9740A"))
                            {
                                ok = gpib.InitSpectrumAnalyzer(lineRate, SystemConfig.OsaDefaultTemplate);
                                logs.Add(ok ? $"{inst.Name} 初始化完成：带宽匹配速率，模板已加载" : $"{inst.Name} 光谱仪初始化失败 (尝试{attempt})");
                            }
                            else if (model.Contains("86100D"))
                            {
                                // 86100D：根据 UI 选择的速率/调制/封装确定模板并加载
                                string template = SystemConfig.GetOscilloscopeTemplate(selectedRate, selectedModulation, selectedPackage);
                                try
                                {
                                    gpib.Write($":TEMPLATE:LOAD \"{template}\"");
                                    logs.Add($"{inst.Name} 模板加载：{template} (尝试{attempt})");
                                }
                                catch (Exception ex)
                                {
                                    logs.Add($"{inst.Name} 示波器模板加载失败：{ex.Message} (尝试{attempt})");
                                    ok = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logs.Add($"{inst.Name} 初始化异常：{ex.Message} (尝试{attempt})");
                            ok = false;
                        }

                        initOk = ok;
                        if (!initOk && attempt < MAX_TRIES)
                            System.Threading.Thread.Sleep(RETRY_DELAY_MS);
                    }

                    if (!initOk)
                    {
                        logs.Add($"{inst.Name} 初始化失败，已超过重试次数");
                        allSuccess = false;
                    }

                    // 记录单台结果，使用 GPIB 地址作为键
                    perResult[inst.GpibAddress] = initOk;
                }
            });

            return (allSuccess, logs, perResult);
        }
    }
}
