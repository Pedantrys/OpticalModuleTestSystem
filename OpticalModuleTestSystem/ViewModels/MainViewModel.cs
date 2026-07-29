using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticalModuleTestSystem.Configs;
using OpticalModuleTestSystem.Drivers;
using OpticalModuleTestSystem.Models;
using OpticalModuleTestSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpticalModuleTestSystem.ViewModels
{
    /// <summary>
    /// 主ViewModel - 完全遵循MVVM模式（修复版）
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        //扫描仪器
        private readonly InstrumentScanner _scanner = new();
        private readonly InstrumentInitializer _initializer = new();

        //通讯面板
        private readonly IICCom _iicCom;
        private CancellationTokenSource _testCts;

        #region === Observable Properties ===

        [ObservableProperty]
        private ObservableCollection<InstrumentInfo> _instruments = new();

        [ObservableProperty]
        private string _log = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        // 新增可观察属性：测试结果、当前测试状态
        [ObservableProperty]
        private ObservableCollection<TemperatureTestResult> _testResults = new();

        [ObservableProperty]
        private string _testStatus = "就绪";

        [ObservableProperty]
        private Brush _testStatusColor = new SolidColorBrush(Colors.Gray);

        // 温度设定值（绑定到UI，替代直接访问TextBox）
        [ObservableProperty]
        private double _tempHighSetting = 85.0;   // 高温默认85℃

        [ObservableProperty]
        private double _tempLowSetting = -40.0;   // 低温默认-40℃

        [ObservableProperty]
        private double _tempRoomSetting = 25.0;   // 常温25℃

        [ObservableProperty]
        private double _currentTemperature;       // 当前实时温度（来自DDM）

        [ObservableProperty]
        private double _currentRxPower;           // 当前实时接收功率

        [ObservableProperty]
        private string _selectedPackage = "SFP+"; // 光模块封装选择

        [ObservableProperty]
        private string _selectedProtocol = "电口"; // 通信协议选择

        [ObservableProperty]
        private string _selectedRate = "10G";     // 速率选择

        [ObservableProperty]
        private bool _isElectricPort = true;      // 电口/光口切换

        [ObservableProperty]
        private string _selectedTempGrade = "商业级"; // 温度等级

        // DDM数据绑定
        public DdmRealTime RealTime { get; } = new DdmRealTime();
        public DdmThresholds Thresholds { get; } = new DdmThresholds();
        public ModuleInfo ModuleInfo { get; } = new ModuleInfo();
        public AlarmStatus Alarm { get; } = new AlarmStatus();

        // 仪器连接状态
        [ObservableProperty]
        private bool _isOscilloscopeConnected;

        [ObservableProperty]
        private bool _isSpectrumAnalyzerConnected;

        [ObservableProperty]
        private bool _isOpticalSwitchConnected;

        [ObservableProperty]
        private bool _isAttenuatorConnected;

        [ObservableProperty]
        private bool _isPlatformConnected;

        [ObservableProperty]
        private bool _isThermalFlowConnected;

        [ObservableProperty]
        private bool _isTempControllerConnected;

        [ObservableProperty]
        private bool _isBertConnected;

        // DEBUG区数据
        [ObservableProperty]
        private string _a0LowByte = "00";

        [ObservableProperty]
        private string _a0HighByte = "00";

        [ObservableProperty]
        private string _a2LowByte = "00";

        [ObservableProperty]
        private string _a2HighByte = "00";

        [ObservableProperty]
        private string _debugPassword = "";

        [ObservableProperty]
        private string _debugPage = "A0";

        [ObservableProperty]
        private int _debugAddress;

        [ObservableProperty]
        private byte _debugValue;

        [ObservableProperty]
        private bool _hostFecEnabled;

        [ObservableProperty]
        private bool _mediaFecEnabled;

        [ObservableProperty]
        private bool _mediaPrbsEnabled;

        [ObservableProperty]
        private bool _isHighPowerMode = true;

        #endregion

        public MainViewModel(IICCom iicCom)
        {
            _iicCom = iicCom ?? throw new ArgumentNullException(nameof(iicCom));
        }

        #region ====================== 仪器扫描 ======================

        [RelayCommand]
        public void ScanInstruments()
        {
            if (IsBusy) return;
            IsBusy = true;
            AddLog("开始扫描GPIB仪器...");

            Task.Run(() =>
            {
                try
                {
                    //1.扫描操作在后台线程执行（耗时操作）
                    var list = _scanner.ScanAll();

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Instruments.Clear();
                        foreach (var item in list)
                        {
                            Instruments.Add(item);
                            AddLog($"找到：{item.Name} {item.Model} @{item.GpibAddress}");
                        }
                        UpdateInstrumentConnectionStatus(list);
                        AddLog("扫描完成");
                    });
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() => AddLog($"扫描异常：{ex.Message}"));
                }
                finally
                {
                    App.Current.Dispatcher.Invoke(() => IsBusy = false);
                }
            });
        }

        private void UpdateInstrumentConnectionStatus(List<InstrumentInfo> list)
        {
            IsOscilloscopeConnected = list.Any(i => i.Model.Contains("86100D"));
            IsSpectrumAnalyzerConnected = list.Any(i => i.Model.Contains("MS9740A"));
            IsAttenuatorConnected = list.Any(i => i.Model.Contains("IQS-3150") || i.Model.Contains("IQS600"));
            IsTempControllerConnected = list.Any(i => i.Model.Contains("AST-545"));
            IsBertConnected = list.Any(i => i.Model.Contains("MP1900A"));
        }

        #endregion

        #region ====================== 一键初始化 ======================

        [RelayCommand]
        public async Task InitializeAll()
        {
            if (IsBusy) return;
            IsBusy = true;
            AddLog("开始一键初始化所有仪器...");

            try
            {
                // 1. 自动识别模块速率
                string rateLevel = GetModuleRateLevel();
                if (string.IsNullOrEmpty(rateLevel))
                {
                    AddLog("❌ 无法识别模块速率，请先读取DDM模块信息");
                    return;
                }
                AddLog($"识别模块速率：{rateLevel}，对应线速率：{SystemConfig.ModuleRateMap[rateLevel]:F4} Gbps");

                // 2. 批量初始化所有仪器
                var (allSuccess, logs) = await _initializer.InitializeAllByRateAsync(Instruments, rateLevel);

                // 3. 回写日志
                foreach (var log in logs)
                {
                    AddLog(log);
                }

                // 4. 更新仪器状态
                foreach (var inst in Instruments)
                {
                    if (inst.IsTargetDevice)
                    {
                        inst.Status = allSuccess ? ConnectStatus.Ready : ConnectStatus.Error;
                        inst.StatusColor = allSuccess ? "#4CD964" : "#FF3B30";
                    }
                }

                AddLog(allSuccess ? "✅ 全部仪器初始化完成" : "⚠️ 部分仪器初始化失败");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 初始化异常：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 从模块型号自动识别速率等级
        /// </summary>
        private string GetModuleRateLevel()
        {
            string model = ModuleInfo.Model?.ToUpper() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model))
            {
                // 如果模块信息未读取，使用UI选择的速率
                return SelectedRate;
            }

            if (model.Contains("25G")) return "25G";
            if (model.Contains("10G")) return "10G";
            if (model.Contains("100G")) return "100G";
            return SelectedRate;
        }

        #endregion

        #region ====================== 全温度段测试 ======================
        /// <summary>
        /// 执行全温度段测试（常温→低温→高温）
        /// </summary>
        [RelayCommand]
        public async Task RunFullTemperatureTest()
        {
            if (IsBusy) return;
            IsBusy = true;
            //
            _testCts = new CancellationTokenSource();
            var token = _testCts.Token;

            // 初始状态：橙色
            UpdateTestStatus("开始全温度段测试...", Colors.Orange);
            AddLog("启动全温度段测试流程");

            try
            {
                // ---------------- 1. 常温测试 ----------------
                UpdateTestStatus("【1/4】正在进行常温测试...", Colors.Orange);
                await RunSingleTempTest("常温", TempRoomSetting, token);
                if (token.IsCancellationRequested) return;

                // ---------------- 2. 低温测试 ----------------
                UpdateTestStatus("【2/4】正在进行低温测试...", Colors.Orange);
                // 线程安全访问UI元素：获取低温阈值
                double lowTemp = Thresholds.TempLowAlarm;
                await RunSingleTempTest("低温", TempLowSetting, token);
                if (token.IsCancellationRequested) return;

                // ---------------- 3. 高温测试 ----------------
                UpdateTestStatus("【3/4】正在进行高温测试...", Colors.Orange);
                // 线程安全访问UI元素：获取高温阈值
                double highTemp = Thresholds.TempHighAlarm;
                await RunSingleTempTest("高温", TempHighSetting, token);
                if (token.IsCancellationRequested) return;

                // ---------------- 4. 恢复常温 ----------------
                UpdateTestStatus("【4/4】正在恢复常温...", Colors.Orange);
                await ReturnToRoomTemp(token);

                // 最终状态：绿色
                UpdateTestStatus("✅ 全温度段测试完成，模块已恢复常温", Colors.Green);
                AddLog("全温度段测试流程结束，模块已恢复常温");
            }
            catch (OperationCanceledException)
            {
                UpdateTestStatus("⏹️ 测试已取消", Colors.Gray);
                AddLog("测试已被用户取消");
            }
            catch (Exception ex)
            {
                UpdateTestStatus($"❌ 测试异常：{ex.Message}", Colors.Red);
                AddLog($"测试异常：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                _testCts?.Dispose();
                _testCts = null;
            }

        }

        [RelayCommand]
        public void StopTest()
        {
            _testCts?.Cancel();
            AddLog("用户请求停止测试");
        }

        /// <summary>
        /// 将模块恢复到25℃常温并等待稳定
        /// </summary>
        private async Task ReturnToRoomTemp(CancellationToken token)
        {
            int ast545Addr = GetInstrumentAddress("AST-545");
            if (ast545Addr == -1)
            {
                AddLog("未找到温控平台，跳过常温恢复");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(ast545Addr))
            {
                AddLog("温控平台连接失败，跳过常温恢复");
                return;
            }

            // 设置目标温度25℃
            gpib.SetTemperature((int)TempRoomSetting);
            AddLog($"开始恢复常温：目标{TempRoomSetting}℃");

            // 等待温度稳定（±1℃内持续60秒）
            bool isStable = await WaitTempStable(TempRoomSetting, token);
            if (isStable)
            {
                AddLog("常温恢复完成，温度已稳定");
            }
            else
            {
                AddLog("常温恢复超时，但仍停止温控");
            }

            // 停止温控
            gpib.StopTemperatureControl();
            gpib.Disconnect();
        }

        /// <summary>
        /// 线程安全更新测试状态（文字+颜色）
        /// </summary>
        /// <param name="statusText">状态文字</param>
        /// <param name="statusColor">状态颜色</param>
        private void UpdateTestStatus(string statusText, Color statusColor)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                TestStatus = statusText;
                // 新增可观察属性：TestStatusColor（类型为Brush）
                TestStatusColor = new SolidColorBrush(statusColor);
            });
        }

        /// <summary>
        /// 执行单温度段测试
        /// </summary>
        /// <param name="tempType">温度类型（常温/低温/高温）</param>
        /// <param name="targetTemp">目标温度</param>
        private async Task RunSingleTempTest(string tempType, double targetTemp, CancellationToken token)
        {
            // 获取温控平台GPIB地址
            int ast545Addr = GetInstrumentAddress("AST-545");
            if (ast545Addr == -1)
            {
                AddLog($"未找到Temptronic AST-545温控平台，{tempType}测试跳过");
                return;
            }

            // 1. 连接温控平台并设置目标温度
            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(ast545Addr))
            {
                AddLog($"温控平台连接失败，{tempType}测试跳过");
                return;
            }

            UpdateTestStatus($"{tempType}测试：设置目标温度 {targetTemp}℃", Colors.Orange);
            gpib.SetTemperature((int)targetTemp);
            AddLog($"{tempType}测试：目标温度设置为 {targetTemp}℃");

            // 2. 等待温度稳定（±1℃内持续1分钟）
            UpdateTestStatus($"{tempType}测试：等待温度稳定...", Colors.Orange);
            bool isStable = await WaitTempStable(targetTemp, token);
            if (!isStable)
            {
                AddLog($"{tempType}测试：温度未稳定，测试终止");
                return;
            }

            // 3. 温度稳定后，记录实时监测值
            UpdateTestStatus($"{tempType}测试：采集模块实时数据...", Colors.Orange);
            var result = new TemperatureTestResult
            {
                TempType = tempType,
                TargetTemp = targetTemp,
                StableTemp = CurrentTemperature,
                Temp = CurrentTemperature,
                Volt = RealTime.Voltage,
                Bias = RealTime.BiasCurrent,
                TxPower = RealTime.TxPower,
                RxPower = RealTime.RxPower,
                TestTime = DateTime.Now
            };

            // 4. 采集Keysight 86100D眼图数据
            UpdateTestStatus($"{tempType}测试：采集86100D Tx/Rx眼图数据...", Colors.Orange);
            var (txData, rxData) = await Get86100DEyeDiagramDataAsync(token);
            result.TxEyeDiagramData = txData;
            result.RxEyeDiagramData = rxData;

            // 5. 采集Anritsu MS9740A光谱数据
            UpdateTestStatus($"{tempType}测试：采集MS9740A光谱数据...", Colors.Orange);
            result.SpectrumData = await GetMS9740ASpectrumDataAsync(token);

            // 6. 新增：EXFO IQS-3150 衰减测试（0dB/3.5dB/7dB）
            UpdateTestStatus($"{tempType}测试：开始EXFO衰减测试...", Colors.Orange);
            await RunEXFOAttenuationTestAsync(result, token);

            // 7. 新增：Anritsu MP1900A 误码仪测试
            UpdateTestStatus($"{tempType}测试：开始误码仪测试...", Colors.Orange);
            await RunMP1900ABerTestAsync(result, token);

            // 8. 停止温控，保存结果
            gpib.StopTemperatureControl();
            App.Current.Dispatcher.Invoke(() => TestResults.Add(result));
            AddLog($"{tempType}测试完成：稳定温度 {result.StableTemp:F2}℃");
        }

        /// <summary>
        /// 等待温度稳定（目标温度±1℃，持续60秒）
        /// </summary>
        /// <param name="targetTemp">目标温度</param>
        /// <returns>是否稳定</returns>
        private async Task<bool> WaitTempStable(double targetTemp, CancellationToken token)
        {
            int stableSeconds = 0;
            var mainWindow = App.Current.MainWindow as Views.MainWindow;

            while (stableSeconds < 60)
            {
                await Task.Delay(1000, token);// 每秒检测一次
                //if (!double.TryParse(mainWindow.Txt_Temp.Text.Replace("℃", "").Trim(), out double currentTemp))
                //{
                //    stableSeconds = 0;
                //    continue;
                //}
        
                // ✅ 使用 CurrentTemperature 属性（已在解析 DDM 时更新）
                double currentTemp = CurrentTemperature;
                // 判断当前温度是否在目标温度±1℃范围内
                if (Math.Abs(currentTemp - targetTemp) <= 1)
                {
                    stableSeconds++;
                    Log += $"[{DateTime.Now:HH:mm:ss}] 温度稳定计时：{stableSeconds}/60秒（当前{currentTemp}℃）\r\n";
                }
                else
                {
                    stableSeconds = 0; // 温度波动，重置计时
                    Log += $"[{DateTime.Now:HH:mm:ss}] 温度未稳定：当前{currentTemp}℃，目标{targetTemp}℃\r\n";
                }
            }
            return true;
        }

        #endregion

        #region ====================== 仪器数据采集（Async版本）======================
        /// <summary>
        /// 获取Keysight 86100D眼图数据
        /// </summary>
        /// <returns>眼图关键参数（如眼高、眼宽、抖动等）</returns>
        /// <summary>
        /// 获取Keysight 86100D发射端+接收端眼图数据
        /// </summary>
        /// <returns>Tx眼图数据, Rx眼图数据</returns>
        private async Task<(string txData, string rxData)> Get86100DEyeDiagramDataAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                int addr = GetInstrumentAddress("86100D");
                if (addr == -1) return ("未找到86100D设备", "未找到86100D设备");

                using var gpib = new GpibCommunicator();
                if (!gpib.Connect(addr)) return ("86100D连接失败", "86100D连接失败");

                string txResult, rxResult;

                // ---------------- 1. 采集发射端（Tx）眼图数据 ----------------
                if (gpib.Switch86100DChannel("Tx"))
                {
                    Thread.Sleep(500);
                    token.ThrowIfCancellationRequested();
                    string txEyeHeight = gpib.Query(":EYE:HEIGHT?");
                    string txEyeWidth = gpib.Query(":EYE:WIDTH?");
                    string txJitter = gpib.Query(":EYE:JITTER?");
                    txResult = $"眼高：{txEyeHeight} mV | 眼宽：{txEyeWidth} ps | 抖动：{txJitter} ps";
                }
                else txResult = "Tx通道切换失败";

                // ---------------- 2. 采集接收端（Rx）眼图数据 ----------------
                if (gpib.Switch86100DChannel("Rx"))
                {
                    Thread.Sleep(500);
                    token.ThrowIfCancellationRequested();
                    string rxEyeHeight = gpib.Query(":EYE:HEIGHT?");
                    string rxEyeWidth = gpib.Query(":EYE:WIDTH?");
                    string rxJitter = gpib.Query(":EYE:JITTER?");
                    rxResult = $"眼高：{rxEyeHeight} mV | 眼宽：{rxEyeWidth} ps | 抖动：{rxJitter} ps";
                }
                else rxResult = "Rx通道切换失败";

                return (txResult, rxResult);
            }, token);
        }

        /// <summary>
        /// 获取Anritsu MS9740A光谱数据
        /// </summary>
        /// <returns>光谱中心波长、功率等</returns>
        private async Task<string> GetMS9740ASpectrumDataAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                int addr = GetInstrumentAddress("MS9740A");
                if (addr == -1) return "未找到MS9740A设备";

                using var gpib = new GpibCommunicator();
                if (!gpib.Connect(addr)) return "MS9740A连接失败";

                token.ThrowIfCancellationRequested();
                string centerWavelength = gpib.Query(":SENS:CENT?");
                token.ThrowIfCancellationRequested();
                string peakPower = gpib.Query(":CALC:PEAK:POW?");

                return $"中心波长：{centerWavelength} nm | 峰值功率：{peakPower} dBm";
            }, token);
        }

        /// <summary>
        /// 执行EXFO IQS-3150 衰减测试（0dB/3.5dB/7dB）
        /// </summary>
        /// <param name="result">测试结果对象（用于写入数据）</param>
        private async Task RunEXFOAttenuationTestAsync(TemperatureTestResult result, CancellationToken token)
        {
            int exfoAddr = GetInstrumentAddress("IQS-3150");
            if (exfoAddr == -1)
            {
                AddLog("未找到EXFO IQS-3150，衰减测试跳过");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(exfoAddr))
            {
                AddLog("EXFO IQS-3150连接失败，衰减测试跳过");
                return;
            }

            // ---------------- 测试1：0dB 衰减 ----------------
            UpdateTestStatus("EXFO测试：设置 0dB 衰减...", Colors.Orange);
            if (gpib.SetEXFOAttenuation(0))
            {
                await Task.Delay(1000, token); // 等待衰减稳定
                result.RxPower_0dB_Module = CurrentRxPower;
                result.RxPower_0dB_EXFO = gpib.ReadEXFOPower();
                AddLog($"EXFO 0dB：模块={result.RxPower_0dB_Module:F2} dBm，设备={result.RxPower_0dB_EXFO:F2} dBm");
            }

            // ---------------- 测试2：3.5dB 衰减 ----------------
            UpdateTestStatus("EXFO测试：设置 3.5dB 衰减...", Colors.Orange);
            if (gpib.SetEXFOAttenuation(3.5))
            {
                await Task.Delay(1000, token);
                result.RxPower_3_5dB_Module = CurrentRxPower;
                result.RxPower_3_5dB_EXFO = gpib.ReadEXFOPower();
                AddLog($"EXFO 3.5dB：模块={result.RxPower_3_5dB_Module:F2} dBm，设备={result.RxPower_3_5dB_EXFO:F2} dBm");
            }

            // ---------------- 测试3：7dB 衰减 ----------------
            TestStatus = "EXFO测试：设置 7dB 衰减...";
            UpdateTestStatus("EXFO测试：设置 7dB 衰减...", Colors.Orange);
            if (gpib.SetEXFOAttenuation(7))
            {
                await Task.Delay(1000, token);
                result.RxPower_7dB_Module = CurrentRxPower;
                result.RxPower_7dB_EXFO = gpib.ReadEXFOPower();
                AddLog($"EXFO 7dB：模块={result.RxPower_7dB_Module:F2} dBm，设备={result.RxPower_7dB_EXFO:F2} dBm");
            }

            // 测试结束，恢复0dB衰减
            gpib.SetEXFOAttenuation(0);
            UpdateTestStatus("EXFO衰减测试完成", Colors.Orange);
        }

        /// <summary>
        /// 执行Anritsu MP1900A误码仪测试流程
        /// </summary>
        /// <param name="result">测试结果对象（用于写入数据）</param>
        private async Task RunMP1900ABerTestAsync(TemperatureTestResult result, CancellationToken token)
        {
            // 1. 检查设备连接
            int mp1900Addr = GetInstrumentAddress("MP1900A");
            int exfoAddr = GetInstrumentAddress("IQS-3150");
            if (mp1900Addr == -1 || exfoAddr == -1)
            {
                AddLog("未找到MP1900A或EXFO，误码仪测试跳过");
                return;
            }

            using var mp1900Gpib = new GpibCommunicator();
            using var exfoGpib = new GpibCommunicator();
            if (!mp1900Gpib.Connect(mp1900Addr) || !exfoGpib.Connect(exfoAddr))
            {
                AddLog("MP1900A或EXFO连接失败，误码仪测试跳过");
                return;
            }

            // 2. 初始化误码仪
            mp1900Gpib.ResetMP1900ABer();
            mp1900Gpib.StartMP1900ATest();
            await Task.Delay(1000, token); // 等待误码仪启动

            // ---------------- 步骤1：衰减到误码率=5E-5 ----------------
            UpdateTestStatus("误码仪测试：衰减到误码率5E-5...", Colors.Orange);
            double currentAtten = 0;
            exfoGpib.SetEXFOAttenuation(currentAtten);
            await Task.Delay(500, token);

            bool found5E5 = false;
            while (currentAtten < 30) // 最大衰减保护
            {
                token.ThrowIfCancellationRequested();
                double ber = mp1900Gpib.ReadMP1900ABer();
                if (ber >= 5e-5)
                {
                    // 误码率达标，记录EXFO光功率
                    result.Ber_5E5_EXFO_Power = exfoGpib.ReadEXFOPower();
                    AddLog($"误码率5E-5：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_5E5_EXFO_Power:F2}dBm");
                    found5E5 = true;
                    break;
                }

                // 未达标，增加衰减（先大后小，提高效率）
                double step = (5e-5 - ber) > 1e-4 ? 2.0 : 0.5;
                currentAtten += step;
                exfoGpib.SetEXFOAttenuation(currentAtten);
                await Task.Delay(800, token); // 等待衰减和误码率稳定
            }

            if (!found5E5)
            {
                AddLog("未达到误码率5E-5，后续步骤跳过");
                exfoGpib.SetEXFOAttenuation(0);
                return;
            }

            // ---------------- 步骤2：继续衰减直到误码率消失 ----------------
            UpdateTestStatus("误码仪测试：衰减到误码率消失...", Colors.Orange);
            int disappearCount = 0;
            while (currentAtten < 35)
            {
                token.ThrowIfCancellationRequested();
                double ber = mp1900Gpib.ReadMP1900ABer();
                if (double.IsNaN(ber) || ber < 1e-12) // 误码率消失判断标准
                {
                    if (++disappearCount >= 3)
                    {
                        result.Ber_Disappear_EXFO_Power = exfoGpib.ReadEXFOPower();
                        AddLog($"误码率消失：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_Disappear_EXFO_Power:F2}dBm");
                        break;
                    }
                }
                else disappearCount = 0;

                // 继续增加衰减
                currentAtten += 0.5;
                exfoGpib.SetEXFOAttenuation(currentAtten);
                await Task.Delay(800, token);
            }

            // ---------------- 步骤3：往回衰减直到误码率重现 ----------------
            UpdateTestStatus("误码仪测试：往回衰减到误码率重现...", Colors.Orange);
            int reappearCount = 0;
            while (currentAtten > 0)
            {
                token.ThrowIfCancellationRequested();
                double ber = mp1900Gpib.ReadMP1900ABer();
                if (!double.IsNaN(ber) && ber >= 1e-10) // 误码率重现判断标准
                {
                    if (++reappearCount >= 2)
                    {
                        result.Ber_Reappear_EXFO_Power = exfoGpib.ReadEXFOPower();
                        AddLog($"误码率重现：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_Reappear_EXFO_Power:F2}dBm");
                        break;
                    }
                }
                else reappearCount = 0;

                // 往回减小衰减
                currentAtten -= 0.2;
                exfoGpib.SetEXFOAttenuation(currentAtten);
                await Task.Delay(800, token);
            }

            exfoGpib.SetEXFOAttenuation(0);
            UpdateTestStatus("误码仪测试完成", Colors.Orange);
        }

        #endregion

        #region ====================== DDM数据解析 ======================

        /// <summary>
        /// 统一刷新所有DDM数据、模块信息、告警状态
        /// </summary>
        public void RefreshAllDdm()
        {
            Task.Run(() =>
            {
                try
                {
                    _iicCom.ReadPage("A0", 256);
                    _iicCom.ReadPage("A2", 256);
                    // 解析数据（注意线程安全）
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ParseRealTimeData(); // 使用校准解析
                        ParseThresholds();
                        ParseModuleInfo();
                        ParseAlarmStatus(true);

                        // 同步到可观察属性
                        CurrentTemperature = RealTime.Temperature;
                        CurrentRxPower = RealTime.RxPower;

                        Alarm.RunStatus = "✅ 数据刷新正常";
                    });
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() =>
                        Alarm.RunStatus = $"❌ 刷新异常: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 1. 实时值解析（对齐原VB算法，补全dBm转换）
        /// </summary>
        private void ParseRealTimeData()
        {
            byte[] a2 = new byte[256];
            for (int i = 0; i < 256; i++) a2[i] = _iicCom.ReadA2Byte(i);

            // 使用 DdmParser 的校准解析
            var ext = DdmParser.ParseDdmExt(a2);
            RealTime.Temperature = ext.Temperature;
            RealTime.Voltage = ext.Voltage;
            RealTime.BiasCurrent = ext.BiasCurrent;
            RealTime.TxPower = ext.TxPower;
            RealTime.RxPower = ext.RxPower;
        }

        /// <summary>
        /// 2. 告警阈值解析
        /// </summary>
        private void ParseThresholds()
        {
            byte[] a2 = new byte[256];
            for (int i = 0; i < 256; i++) a2[i] = _iicCom.ReadA2Byte(i);

            var alarm = DdmParser.ParseAlarmThresholds(a2, true);

            // ========== 温度阈值（4级：高告警/高警告/低警告/低告警）==========
            Thresholds.TempHighAlarm = alarm.TempThresholds[0];   // 高告警
            Thresholds.TempHighWarning = alarm.TempThresholds[1];   // 高警告
            Thresholds.TempLowWarning = alarm.TempThresholds[2];   // 低警告
            Thresholds.TempLowAlarm = alarm.TempThresholds[3];   // 低告警

            // ========== 电压阈值 ==========
            Thresholds.VccHighAlarm = alarm.VoltageThresholds[0];
            Thresholds.VccHighWarning = alarm.VoltageThresholds[1];
            Thresholds.VccLowWarning = alarm.VoltageThresholds[2];
            Thresholds.VccLowAlarm = alarm.VoltageThresholds[3];

            // ========== 偏置电流阈值 ==========
            Thresholds.BiasHighAlarm = alarm.BiasThresholds[0];
            Thresholds.BiasHighWarning = alarm.BiasThresholds[1];
            Thresholds.BiasLowWarning = alarm.BiasThresholds[2];
            Thresholds.BiasLowAlarm = alarm.BiasThresholds[3];

            // ========== 发射功率阈值 ==========
            Thresholds.TxPowerHighAlarm = alarm.TxPowerThresholds[0];
            Thresholds.TxPowerHighWarning = alarm.TxPowerThresholds[1];
            Thresholds.TxPowerLowWarning = alarm.TxPowerThresholds[2];
            Thresholds.TxPowerLowAlarm = alarm.TxPowerThresholds[3];

            // ========== 接收功率阈值 ==========
            Thresholds.RxPowerHighAlarm = alarm.RxPowerThresholds[0];
            Thresholds.RxPowerHighWarning = alarm.RxPowerThresholds[1];
            Thresholds.RxPowerLowWarning = alarm.RxPowerThresholds[2];
            Thresholds.RxPowerLowAlarm = alarm.RxPowerThresholds[3];
        }

        /// <summary>
        /// 3. 模块信息解析（A0页）
        /// </summary>
        private void ParseModuleInfo()
        {
            byte[] a0 = new byte[256];
            for (int i = 0; i < 256; i++) a0[i] = _iicCom.ReadA0Byte(i);
            var info = DdmParser.ParseModuleInfo(a0);
            ModuleInfo.Manufacturer = info.Manufacturer;
            ModuleInfo.Model = info.Model;
            ModuleInfo.SerialNumber = info.SerialNumber;
            ModuleInfo.DateCode = info.DateCode;
        }

        /// <summary>
        /// 4. 告警状态解析（对齐原VB Alarm_Warning）
        /// </summary>
        /// <param name="enable"></param>
        private void ParseAlarmStatus(bool enable)
        {
            // 内外模式判断：A2页偏移92
            byte flagByte = _iicCom.ReadA2Byte(92);
            Alarm.IsInternalMode = (flagByte & 32) == 0;

            // 告警位：A2页偏移110
            byte alarmByte = _iicCom.ReadA2Byte(110);
            Alarm.TxFault = (alarmByte & 2) != 0;
            Alarm.RxLos = (alarmByte & 4) != 0;
        }

        #endregion

        #region ====================== DEBUG区命令 ======================
        [RelayCommand]
        public void ReadA0LowByte()
        {
            byte val = _iicCom.ReadA0Byte(0);
            A0LowByte = $"{val:X2}";
            AddLog($"A0 Low Byte: 0x{A0LowByte}");
        }

        [RelayCommand]
        public void ReadA0HighByte()
        {
            byte val = _iicCom.ReadA0Byte(128);
            A0HighByte = $"{val:X2}";
            AddLog($"A0 High Byte: 0x{A0HighByte}");
        }

        [RelayCommand]
        public void ReadA2LowByte()
        {
            byte val = _iicCom.ReadA2Byte(0);
            A2LowByte = $"{val:X2}";
            AddLog($"A2 Low Byte: 0x{A2LowByte}");
        }

        [RelayCommand]
        public void ReadA2HighByte()
        {
            byte val = _iicCom.ReadA2Byte(128);
            A2HighByte = $"{val:X2}";
            AddLog($"A2 High Byte: 0x{A2HighByte}");
        }

        [RelayCommand]
        public void WriteDebugValue()
        {
            bool ok = _iicCom.WriteByte(DebugPage, DebugAddress, DebugValue);
            AddLog(ok ? $"写入成功：{DebugPage}[{DebugAddress}] = 0x{DebugValue:X2}"
                      : $"写入失败：{DebugPage}[{DebugAddress}]");
        }

        [RelayCommand]
        public void ReadDebugValue()
        {
            byte val = DebugPage == "A0" ? _iicCom.ReadA0Byte(DebugAddress) : _iicCom.ReadA2Byte(DebugAddress);
            DebugValue = val;
            AddLog($"读取成功：{DebugPage}[{DebugAddress}] = 0x{val:X2}");
        }

        [RelayCommand]
        public void TogglePowerMode()
        {
            IsHighPowerMode = !IsHighPowerMode;
            AddLog($"切换至{(IsHighPowerMode ? "高功耗" : "低功耗")}模式");
            // TODO: 发送实际切换命令到模块
        }

        [RelayCommand]
        public void ResetModule()
        {
            AddLog("模块复位命令已发送");
            // TODO: 实现模块复位逻辑
        }

        #endregion

        #region ====================== 温度等级选择 ======================

        [RelayCommand]
        public void SelectIndustrialGrade()
        {
            SelectedTempGrade = "工业级";
            TempHighSetting = 85;
            TempLowSetting = -40;
            AddLog("选择工业级温度范围：-40℃ ~ +85℃");
        }

        [RelayCommand]
        public void SelectCommercialGrade()
        {
            SelectedTempGrade = "商业级";
            TempHighSetting = 70;
            TempLowSetting = 0;
            AddLog("选择商业级温度范围：0℃ ~ +70℃");
        }

        [RelayCommand]
        public void SelectExtendedGrade()
        {
            SelectedTempGrade = "扩展级";
            TempHighSetting = 85;
            TempLowSetting = -20;
            AddLog("选择扩展级温度范围：-20℃ ~ +85℃");
        }

        [RelayCommand]
        public void SelectCustomTemp()
        {
            SelectedTempGrade = "自定义";
            AddLog("选择自定义温度，请手动设置高低温值");
        }

        [RelayCommand]
        public void SetHighTemp()
        {
            AddLog($"设置高温测试点：{TempHighSetting}℃");
        }

        [RelayCommand]
        public void SetLowTemp()
        {
            AddLog($"设置低温测试点：{TempLowSetting}℃");
        }

        [RelayCommand]
        public void SetRoomTemp()
        {
            AddLog($"设置常温测试点：{TempRoomSetting}℃");
        }

        #endregion

        #region ====================== 辅助方法 ======================

        /// <summary>
        /// 获取指定类型的仪器GPIB地址
        /// </summary>
        /// <param name="modelName">仪器型号关键字</param>
        /// <returns>GPIB地址（未找到返回-1）</returns>
        private int GetInstrumentAddress(string modelName)
        {
            return Instruments.FirstOrDefault(inst => inst.Model.Contains(modelName))?.GpibAddress ?? -1;
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        /// <param name="message"></param>
        public void AddLog(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Log += $"[{DateTime.Now:HH:mm:ss}] {message}\\r\\n";
            });
        }

        #endregion
    }
}
