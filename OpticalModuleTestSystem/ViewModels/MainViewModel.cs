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

        // PID 控制参数（可在 ViewModel 中调整以便调参）
        [ObservableProperty]
        private double _pidKp = 0.5;

        [ObservableProperty]
        private double _pidKi = 0.02;

        [ObservableProperty]
        private double _pidKd = 0.1;

        [ObservableProperty]
        private double _pidMaxStep = 3.0; // 单次最大改变量（℃）

        [ObservableProperty]
        private int _pidCheckSec = 5; // PID 采样周期（秒）

        [ObservableProperty]
        private double _pidStableTol = 0.5; // 稳定公差（℃）

        [ObservableProperty]
        private int _pidStableDuration = 120; // 稳定持续时间（秒）

        // 最小平台设定间隔（秒），防止频繁下发温控指令
        [ObservableProperty]
        private int _pidMinSetIntervalSeconds = 120;

        // 温度变化率阈值（℃/min），当模块温度变化率低于该值时允许再次调整平台设定
        [ObservableProperty]
        private double _pidChangeRateThresholdDegPerMin = 0.05;

        // 分段缓升/缓降参数
        [ObservableProperty]
        private double _rampStepSizeDeg = 2.0; // 每步最多 2℃

        [ObservableProperty]
        private int _rampStepIntervalSeconds = 60; // 每步间隔 60s（会根据最大斜率调整）

        [ObservableProperty]
        private double _rampMaxSlopeDegPerMin = 1.0; // 最大斜率 1℃/min

        [ObservableProperty]
        private string _selectedPackage = "SFP+"; // 光模块封装选择

        [ObservableProperty]
        private string _selectedOpticalProtocol = "SFF-8472"; // 光口协议默认

        [ObservableProperty]
        private string _selectedElectricProtocol = "1000BASE-T"; // 电口协议默认

        [ObservableProperty]
        private string _selectedOpticalRate = "10G"; // 光口速率默认

        [ObservableProperty]
        private string _selectedElectricRate = "10G"; // 电口速率默认

        [ObservableProperty]
        private string _selectedModulation = "NRZ"; // 调制方式默认

        // 下拉选项集合（用于 ItemsSource 绑定，便于维护与本地化）
        public ObservableCollection<string> PackageOptions { get; } = new ObservableCollection<string> { "SFP+", "QSFP28", "QSFP-DD", "SFP28" };
        public ObservableCollection<string> OpticalProtocolOptions { get; } = new ObservableCollection<string> { "SFF-8472", "SFF-8636", "CMIS" };
        public ObservableCollection<string> ElectricProtocolOptions { get; } = new ObservableCollection<string> { "1000BASE-T", "10GBASE-T", "25GBASE-CR" };
        public ObservableCollection<string> OpticalRateOptions { get; } = new ObservableCollection<string> { "10G", "25G", "100G", "400G" };
        public ObservableCollection<string> ElectricRateOptions { get; } = new ObservableCollection<string> { "1.5G", "10G", "25G", "50G", "100G", "500G" };
        public ObservableCollection<string> ModulationOptions { get; } = new ObservableCollection<string> { "NRZ", "PAM4" };



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
        private double _thermalFlowOutletTemp;

        private CancellationTokenSource _thermalFlowCts;

        [ObservableProperty]
        private bool _isTempControllerConnected;

        [ObservableProperty]
        private bool _isTempCustom = false; // 是否允许编辑温度设定（自定义）

        [ObservableProperty]
        private bool _isBertConnected;

        // ===== 性能测试项开关（默认全部启用） =====
        [ObservableProperty]
        private bool _selectAllTests = true;

        [ObservableProperty]
        private bool _txSingleChannelPower = true;

        [ObservableProperty]
        private bool _txEyeMargin = true;

        [ObservableProperty]
        private bool _txTdecq = true;

        [ObservableProperty]
        private bool _txExtinctionRatio = true;

        [ObservableProperty]
        private bool _txCenterWavelength = true;

        [ObservableProperty]
        private bool _txSpectralWidth = true;

        [ObservableProperty]
        private bool _txSmsr = true;

        [ObservableProperty]
        private bool _txPowerAccuracy = true;

        [ObservableProperty]
        private bool _rxSingleChannelSensitivity = true;

        [ObservableProperty]
        private bool _rxBerPowerTrend = true;

        [ObservableProperty]
        private bool _rxLosa = true;

        [ObservableProperty]
        private bool _rxLosd = true;

        [ObservableProperty]
        private bool _rxLosHysteresis = true;

        [ObservableProperty]
        private bool _rxPowerAccuracy = true;

        partial void OnSelectAllTestsChanged(bool value)
        {
            // 当用户切换全选，设置所有子项
            _txSingleChannelPower = value;
            _txEyeMargin = value;
            _txTdecq = value;
            _txExtinctionRatio = value;
            _txCenterWavelength = value;
            _txSpectralWidth = value;
            _txSmsr = value;
            _txPowerAccuracy = value;

            _rxSingleChannelSensitivity = value;
            _rxBerPowerTrend = value;
            _rxLosa = value;
            _rxLosd = value;
            _rxLosHysteresis = value;
            _rxPowerAccuracy = value;

            // 通知属性变化
            OnPropertyChanged(nameof(TxSingleChannelPower));
            OnPropertyChanged(nameof(TxEyeMargin));
            OnPropertyChanged(nameof(TxTdecq));
            OnPropertyChanged(nameof(TxExtinctionRatio));
            OnPropertyChanged(nameof(TxCenterWavelength));
            OnPropertyChanged(nameof(TxSpectralWidth));
            OnPropertyChanged(nameof(TxSmsr));
            OnPropertyChanged(nameof(TxPowerAccuracy));

            OnPropertyChanged(nameof(RxSingleChannelSensitivity));
            OnPropertyChanged(nameof(RxBerPowerTrend));
            OnPropertyChanged(nameof(RxLosa));
            OnPropertyChanged(nameof(RxLosd));
            OnPropertyChanged(nameof(RxLosHysteresis));
            OnPropertyChanged(nameof(RxPowerAccuracy));
        }

        // 当任一子项变化时，更新 SelectAll 状态
        partial void OnTxSingleChannelPowerChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxEyeMarginChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxTdecqChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxExtinctionRatioChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxCenterWavelengthChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxSpectralWidthChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxSmsrChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnTxPowerAccuracyChanged(bool value) => UpdateSelectAllFromChildren();

        partial void OnRxSingleChannelSensitivityChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnRxBerPowerTrendChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnRxLosaChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnRxLosdChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnRxLosHysteresisChanged(bool value) => UpdateSelectAllFromChildren();
        partial void OnRxPowerAccuracyChanged(bool value) => UpdateSelectAllFromChildren();

        private void UpdateSelectAllFromChildren()
        {
            bool all = _txSingleChannelPower && _txEyeMargin && _txTdecq && _txExtinctionRatio && _txCenterWavelength && _txSpectralWidth && _txSmsr && _txPowerAccuracy
                       && _rxSingleChannelSensitivity && _rxBerPowerTrend && _rxLosa && _rxLosd && _rxLosHysteresis && _rxPowerAccuracy;
            if (_selectAllTests != all)
            {
                _selectAllTests = all;
                OnPropertyChanged(nameof(SelectAllTests));
            }
        }

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

        // 字符串表示的 DebugValue（用于在 TextBox 中双向绑定并支持十六进制显示）
        public string DebugValueString
        {
            get => DebugValue.ToString("X2");
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                // 允许带 "0x" 或纯 hex 字符串
                string s = value.Trim();
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
                if (byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    DebugValue = b;
                }
            }
        }

        [ObservableProperty]
        private bool _hostFecEnabled;

        [ObservableProperty]
        private bool _mediaFecEnabled;

        [ObservableProperty]
        private bool _mediaPrbsEnabled;

        [ObservableProperty]
        private bool _isHighPowerMode = true;

        [ObservableProperty]
        private bool _isTempAlarm;

        [ObservableProperty]
        private bool _isVoltAlarm;

        [ObservableProperty]
        private bool _isBiasAlarm;

        [ObservableProperty]
        private bool _isTxPowerAlarm;

        [ObservableProperty]
        private bool _isRxPowerAlarm;

        #endregion

        // 启动热流仪探头出口温度轮询监控
        private void StartThermalFlowMonitor()
        {
            if (_thermalFlowCts != null)
                return; // 已在运行

            _thermalFlowCts = new CancellationTokenSource();
            var token = _thermalFlowCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    int addr = GetInstrumentAddress("FLOW");
                    if (addr == -1)
                    {
                        // 尝试其它关键字
                        addr = GetInstrumentAddress("THERM");
                    }
                    if (addr == -1)
                    {
                        AddLog("未找到热流仪，停止热流监控");
                        StopThermalFlowMonitor();
                        return;
                    }

                    using var gpib = new GpibCommunicator();
                    if (!gpib.Connect(addr))
                    {
                        AddLog("热流仪连接失败，停止监控");
                        StopThermalFlowMonitor();
                        return;
                    }

                    AddLog($"热流仪监控启动 @ GPIB {addr}");

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // 尝试常见的查询命令，设备不同需调整
                            string[] cmds = new[] { ":MEAS:TEMP?", "MEAS:TEMP?", "TEMP?", ":SENS:TEMP?", "T?" };
                            string resp = null;
                            foreach (var c in cmds)
                            {
                                resp = gpib.Query(c);
                                if (!string.IsNullOrWhiteSpace(resp)) break;
                            }

                            if (!string.IsNullOrWhiteSpace(resp))
                            {
                                resp = resp.Trim();
                                if (double.TryParse(resp, out double t))
                                {
                                    App.Current.Dispatcher.Invoke(() => ThermalFlowOutletTemp = t);
                                }
                                else
                                {
                                    // 有些设备返回带单位的字符串，例如 "23.5 C"
                                    var num = new string(resp.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-' ).ToArray());
                                    if (double.TryParse(num, out double t2))
                                    {
                                        App.Current.Dispatcher.Invoke(() => ThermalFlowOutletTemp = t2);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"热流仪读取异常：{ex.Message}");
                        }

                        await Task.Delay(2000, token);
                    }

                    gpib.Disconnect();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AddLog($"热流监控任务异常：{ex.Message}");
                }
            }, token);
        }

        /// <summary>
        /// 
        /// </summary>
        private void StopThermalFlowMonitor()
        {
            try
            {
                _thermalFlowCts?.Cancel();
                _thermalFlowCts?.Dispose();
            }
            catch { }
            finally { _thermalFlowCts = null; }
        }

        // 当前下拉项与统一选择（手动实现以避免对源生成器过度依赖）
        private ObservableCollection<string> _currentProtocolOptions = new ObservableCollection<string>();
        public ObservableCollection<string> CurrentProtocolOptions { get => _currentProtocolOptions; set => SetProperty(ref _currentProtocolOptions, value); }

        private ObservableCollection<string> _currentRateOptions = new ObservableCollection<string>();
        public ObservableCollection<string> CurrentRateOptions { get => _currentRateOptions; set => SetProperty(ref _currentRateOptions, value); }

        private string _selectedProtocol;
        public string SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (SetProperty(ref _selectedProtocol, value))
                {
                    if (_isElectricPort)
                    {
                        _selectedElectricProtocol = value;
                        OnPropertyChanged(nameof(SelectedElectricProtocol));
                    }
                    else
                    {
                        _selectedOpticalProtocol = value;
                        OnPropertyChanged(nameof(SelectedOpticalProtocol));
                    }
                }
            }
        }

        private string _selectedRate;
        public string SelectedRate
        {
            get => _selectedRate;
            set
            {
                if (SetProperty(ref _selectedRate, value))
                {
                    if (_isElectricPort)
                    {
                        _selectedElectricRate = value;
                        OnPropertyChanged(nameof(SelectedElectricRate));
                    }
                    else
                    {
                        _selectedOpticalRate = value;
                        OnPropertyChanged(nameof(SelectedOpticalRate));
                    }
                }
            }
        }

        public MainViewModel(IICCom iicCom)
        {
            _iicCom = iicCom ?? throw new ArgumentNullException(nameof(iicCom));
            // 初始化下拉集合与当前选项（根据默认 IsElectricPort）
            if (_isElectricPort)
            {
                CurrentProtocolOptions = new ObservableCollection<string>(ElectricProtocolOptions);
                CurrentRateOptions = new ObservableCollection<string>(ElectricRateOptions);
                _selectedProtocol = _selectedElectricProtocol;
                _selectedRate = _selectedElectricRate;
            }
            else
            {
                CurrentProtocolOptions = new ObservableCollection<string>(OpticalProtocolOptions);
                CurrentRateOptions = new ObservableCollection<string>(OpticalRateOptions);
                _selectedProtocol = _selectedOpticalProtocol;
                _selectedRate = _selectedOpticalRate;
            }
        }

        // 当 IsElectricPort 切换时，更新当前下拉项并同步 SelectedProtocol/SelectedRate
        partial void OnIsElectricPortChanged(bool value)
        {
            if (value)
            {
                CurrentProtocolOptions = new ObservableCollection<string>(ElectricProtocolOptions);
                CurrentRateOptions = new ObservableCollection<string>(ElectricRateOptions);
                SelectedProtocol = _selectedElectricProtocol;
                SelectedRate = _selectedElectricRate;
            }
            else
            {
                CurrentProtocolOptions = new ObservableCollection<string>(OpticalProtocolOptions);
                CurrentRateOptions = new ObservableCollection<string>(OpticalRateOptions);
                SelectedProtocol = _selectedOpticalProtocol;
                SelectedRate = _selectedOpticalRate;
            }
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
            IsAttenuatorConnected = list.Any(i => i.Model.Contains("IQS-610P") || i.Model.Contains("IQS600"));
            IsTempControllerConnected = list.Any(i => i.Model.Contains("ATS-545"));
            IsBertConnected = list.Any(i => i.Model.Contains("MP1900A"));
            // 简单检测热流仪：匹配 Model 或 Name 中包含关键词
            IsThermalFlowConnected = list.Any(i => i.Model.ToUpper().Contains("FLOW") || i.Model.ToUpper().Contains("THERM") || i.Name.ToUpper().Contains("FLOW") || i.Name.ToUpper().Contains("THERM") || i.Model.Contains("热流"));

            // 启动或停止热流仪监控任务
            if (IsThermalFlowConnected)
            {
                StartThermalFlowMonitor();
            }
            else
            {
                StopThermalFlowMonitor();
            }
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
                // 1. 使用 UI 选择优先（封装/协议/速率/调制），若未设置则回退至自动识别
                string selectedPackage = SelectedPackage;
                // 使用字段直接读取，避免在生成属性不可用的情况下出错
                string selectedProtocol = _isElectricPort ? _selectedElectricProtocol : _selectedOpticalProtocol;
                string selectedRate = _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
                string selectedModulation = _selectedModulation;

                string rateLevel = selectedRate;
                if (string.IsNullOrEmpty(rateLevel))
                {
                    rateLevel = GetModuleRateLevel();
                    if (string.IsNullOrEmpty(rateLevel))
                    {
                        AddLog("❌ 无法识别模块速率，请先读取DDM模块信息或在界面选择速率");
                        return;
                    }
                }

                AddLog($"初始化参数：封装={selectedPackage}, 协议={selectedProtocol}, 速率={selectedRate}, 调制={selectedModulation}");

                // 2. 批量初始化所有仪器（返回总体结果 + 日志 + 每台结果）
                var (allSuccess, logs, perInstrument) = await _initializer.InitializeAllByRateAsync(
                    Instruments, rateLevel, selectedPackage, selectedProtocol, selectedRate, selectedModulation);

                // 3. 回写日志
                foreach (var log in logs)
                {
                    AddLog(log);
                }

                // 4. 更新仪器状态（按单台结果设置）
                foreach (var inst in Instruments)
                {
                    if (!inst.IsTargetDevice) continue;
                    if (perInstrument != null && perInstrument.TryGetValue(inst.GpibAddress, out bool ok))
                    {
                        inst.Status = ok ? ConnectStatus.Ready : ConnectStatus.Error;
                        inst.StatusColor = ok ? "#4CD964" : "#FF3B30";
                    }
                    else
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
                return _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
            }

            if (model.Contains("25G")) return "25G";
            if (model.Contains("10G")) return "10G";
            if (model.Contains("100G")) return "100G";
            return _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
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
            // 创建新的CancellationTokenSource用于取消操作
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
            //关闭温控平台，恢复常温
            Task.Run(async () =>
            {
                try
                {
                    await ReturnToRoomTemp(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AddLog($"停止测试时恢复常温异常：{ex.Message}");
                }
            });
            AddLog("用户请求停止测试");
        }

        /// <summary>
        /// 将模块恢复到25℃常温并等待稳定
        /// </summary>
        private async Task ReturnToRoomTemp(CancellationToken token)
        {
            int ast545Addr = GetInstrumentAddress("ATS-545");
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

            // 等待温度稳定（±1℃内持续60秒）
            bool isStable = await WaitTempStable(TempRoomSetting, gpib, token);
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
            int ast545Addr = GetInstrumentAddress("ATS-545");
            if (ast545Addr == -1)
            {
                AddLog($"未找到Temptronic ATS-545温控平台，{tempType}测试跳过");
                return;
            }

            // 1. 连接温控平台并设置目标温度
            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(ast545Addr))
            {
                AddLog($"温控平台连接失败，{tempType}测试跳过");
                return;
            }

            // 2. 等待温度稳定（±1℃内持续1分钟）
            UpdateTestStatus($"{tempType}测试：等待温度稳定...", Colors.Orange);
            bool isStable = await WaitTempStable(targetTemp, gpib, token);
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

            // 4. 采集Keysight 86100D眼图数据（封装为独立方法）
            if (TxEyeMargin || TxTdecq || TxSmsr || TxPowerAccuracy || RxBerPowerTrend)
            {
                await RunEyeTestsAsync(result, token);
            }

            // 5. 采集Anritsu MS9740A光谱数据
            if (TxCenterWavelength || TxSpectralWidth || TxSmsr || RxPowerAccuracy)
            {
                await RunSpectrumTestsAsync(result, token);
            }

            // 6. 新增：EXFO IQS-610P 衰减测试（0dB/3.5dB/7dB）
            if (TxSingleChannelPower || RxSingleChannelSensitivity || RxBerPowerTrend)
            {
                UpdateTestStatus($"{tempType}测试：开始EXFO衰减测试...", Colors.Orange);
                await RunEXFOAttenuationTestAsync(result, token);
            }

            // 7. 新增：Anritsu MP1900A 误码仪测试
            if (RxBerPowerTrend)
            {
                UpdateTestStatus($"{tempType}测试：开始误码仪测试...", Colors.Orange);
                await RunMP1900ABerTestAsync(result, token);
            }

            // 8. 停止温控，保存结果
            gpib.StopTemperatureControl();
            App.Current.Dispatcher.Invoke(() => TestResults.Add(result));
            AddLog($"{tempType}测试完成：稳定温度 {result.StableTemp:F2}℃");
        }

        /// <summary>
        /// 等待温度稳定（目标温度±1℃，持续60秒）
        /// </summary>
        /// <param name="targetTemp">目标温度</param>
        /// <param name="token">取消令牌</param>
        /// <returns>是否稳定</returns>
        private async Task<bool> WaitTempStable(double targetTemp, GpibCommunicator gpib, CancellationToken token)
        {
            // 使用 PID 控制算法对平台设定进行细调以加速温度收敛并减少过冲
            int checkSec = Math.Max(1, PidCheckSec);
            double stableTol = Math.Max(0.1, PidStableTol);
            int stableDuration = Math.Max(10, PidStableDuration);

            double thermalOffset = 12.0; // 初始热阻估计

            // 计算目标平台设定（基于热阻估计）
            double targetPlatformSet = Math.Clamp(targetTemp - thermalOffset, -50.0, 150.0);

            // 读取当前平台设定（设备返回0表示读取失败，此时直接使用目标设定）
            double currentPlatformSet = gpib.GetSetTemperature();
            if (Math.Abs(currentPlatformSet) < 0.0001) currentPlatformSet = targetPlatformSet;

            AddLog($"温控(PID)准备：当前平台设定 {currentPlatformSet:F2}℃，目标平台设定 {targetPlatformSet:F2}℃（初始热阻估计{thermalOffset:F1}℃）");

            // 分段缓升/缓降（step-wise ramp）——每步不超过 RampStepSizeDeg，且受 RampMaxSlopeDegPerMin 限制
            double remaining = targetPlatformSet - currentPlatformSet;
            if (Math.Abs(remaining) > 0.02)
            {
                double sign = Math.Sign(remaining);
                double maxSlope = Math.Max(0.01, RampMaxSlopeDegPerMin);
                int configuredInterval = Math.Max(1, RampStepIntervalSeconds);

                // 优先保留配置的步间隔（避免过长等待）。若原始步长会导致超出最大斜率，
                // 则缩小步长以满足最大斜率：allowedStep = maxSlope * (configuredInterval/60)
                double allowedStepForInterval = maxSlope * (configuredInterval / 60.0);
                double stepSize = Math.Min(Math.Max(0.01, RampStepSizeDeg), Math.Max(0.01, allowedStepForInterval));
                int effectiveIntervalSec = configuredInterval;

                if (stepSize < RampStepSizeDeg)
                {
                    AddLog($"调整步长以满足最大斜率：配置 {RampStepSizeDeg}°C -> 实际 {stepSize:F2}°C (间隔 {effectiveIntervalSec}s, 最大斜率 {maxSlope}°C/min)");
                }

                while (Math.Abs(remaining) > 0.02)
                {
                    token.ThrowIfCancellationRequested();
                    double step = sign * Math.Min(stepSize, Math.Abs(remaining));
                    double nextSet = Math.Clamp(currentPlatformSet + step, -50.0, 150.0);
                    gpib.SetTemperature(nextSet);
                    AddLog($"分段下发平台设定：{currentPlatformSet:F2} -> {nextSet:F2} ℃，等待 {effectiveIntervalSec}s...");
                    currentPlatformSet = nextSet;
                    remaining = targetPlatformSet - currentPlatformSet;
                    await Task.Delay(effectiveIntervalSec * 1000, token);
                }
            }

            // 最终平台设定用于后续 PID 调整
            double platformSet = currentPlatformSet;

            AddLog($"温控(PID)启动：平台{platformSet:F1}℃ → 目标模块{targetTemp}℃（热阻估计{thermalOffset:F1}℃）");

            double integral = 0.0;
            double prevError = 0.0;
            int stableSec = 0;
            DateTime lastTime = DateTime.UtcNow;

            // 最近温度样本，用于计算变化率（秒级时间戳）
            var samples = new List<(DateTime t, double temp)>();
            DateTime lastSetTime = DateTime.UtcNow;

            while (stableSec < stableDuration)
            {
                await Task.Delay(checkSec * 1000, token);
                token.ThrowIfCancellationRequested();

                DateTime now = DateTime.UtcNow;
                double dt = (now - lastTime).TotalSeconds;
                if (dt <= 0) dt = checkSec;
                lastTime = now;

                double moduleTemp = CurrentTemperature;
                double error = targetTemp - moduleTemp; // 正值需要升温

                // PID 计算
                integral += error * dt;
                double derivative = (error - prevError) / dt;
                prevError = error;

                double pidOut = PidKp * error + PidKi * integral + PidKd * derivative;

                // 限幅并避免过大单次调整
                double maxStep = Math.Max(0.5, PidMaxStep);
                double delta = Math.Clamp(pidOut, -maxStep, maxStep);

                double newSet = Math.Clamp(platformSet + delta, -50.0, 150.0);

                // 记录样本并计算最近变化率（℃/min）
                samples.Add((now, moduleTemp));
                // 保持样本窗口为最近 3 分钟
                var window = TimeSpan.FromMinutes(3);
                samples.RemoveAll(s => (now - s.t) > window);
                double changeRatePerMin = 0.0;
                if (samples.Count >= 2)
                {
                    var first = samples.First();
                    var last = samples.Last();
                    var minutes = (last.t - first.t).TotalMinutes;
                    if (minutes > 0) changeRatePerMin = (last.temp - first.temp) / minutes;
                }

                bool allowSetBecauseInterval = (now - lastSetTime).TotalSeconds >= Math.Max(1, PidMinSetIntervalSeconds);
                bool allowSetBecauseRate = Math.Abs(changeRatePerMin) <= Math.Max(0.0001, PidChangeRateThresholdDegPerMin);

                if (Math.Abs(newSet - platformSet) >= 0.05)
                {
                    if (allowSetBecauseInterval || allowSetBecauseRate)
                    {
                        gpib.SetTemperature(newSet);
                        AddLog($"[{DateTime.Now:HH:mm:ss}] PID调整 平台 {platformSet:F2} -> {newSet:F2} (pid:{pidOut:F3}, err:{error:F3}, rate:{changeRatePerMin:F3}°C/min)");
                        platformSet = newSet;
                        lastSetTime = now;
                    }
                    else
                    {
                        AddLog($"[{DateTime.Now:HH:mm:ss}] 延迟下发设置：上次下发 {Math.Round((now - lastSetTime).TotalSeconds)}s 前，变化率 {changeRatePerMin:F3}°C/min 超过阈值");
                    }
                }
                else
                {
                    AddLog($"[{DateTime.Now:HH:mm:ss}] PID 无需调整 (err:{error:F3})");
                }

                // 自适应更新热阻估计（平滑滤波）
                double actualOffset = moduleTemp - platformSet;
                thermalOffset = thermalOffset * 0.8 + actualOffset * 0.2;

                // 判断稳定性
                if (Math.Abs(error) <= stableTol)
                {
                    stableSec += checkSec;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] 稳定计时 {stableSec}/{stableDuration} | 模块 {moduleTemp:F2}℃ | 平台 {platformSet:F2}℃");
                }
                else
                {
                    stableSec = 0;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] 未稳定：模块 {moduleTemp:F2}℃ (目标 {targetTemp:F2}℃)");
                }
            }

            AddLog($"温控(PID)稳定：目标 {targetTemp:F2}℃ 达成（持续 {stableDuration}s）");
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
        /// 执行EXFO IQS-610P 衰减测试（0dB/3.5dB/7dB）
        /// </summary>
        /// <param name="result">测试结果对象（用于写入数据）</param>
        private async Task RunEXFOAttenuationTestAsync(TemperatureTestResult result, CancellationToken token)
        {
            int exfoAddr = GetInstrumentAddress("IQS-610P");
            if (exfoAddr == -1)
            {
                AddLog("未找到EXFO IQS-610P，衰减测试跳过");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(exfoAddr))
            {
                AddLog("EXFO IQS-610P连接失败，衰减测试跳过");
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
            int exfoAddr = GetInstrumentAddress("IQS-610P");
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

        /// <summary>
        /// 独立运行示波器相关的眼图/抖动测量等
        /// </summary>
        private async Task RunEyeTestsAsync(TemperatureTestResult result, CancellationToken token)
        {
            UpdateTestStatus("采集86100D Tx/Rx眼图数据...", Colors.Orange);
            var (txData, rxData) = await Get86100DEyeDiagramDataAsync(token);
            result.TxEyeDiagramData = txData;
            result.RxEyeDiagramData = rxData;
            AddLog($"眼图数据采集完成: Tx={txData}, Rx={rxData}");
        }

        /// <summary>
        /// 独立运行光谱相关测量
        /// </summary>
        private async Task RunSpectrumTestsAsync(TemperatureTestResult result, CancellationToken token)
        {
            UpdateTestStatus("采集MS9740A光谱数据...", Colors.Orange);
            result.SpectrumData = await GetMS9740ASpectrumDataAsync(token);
            AddLog($"光谱数据采集完成: {result.SpectrumData}");
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

                        // 刷新完实时数据和阈值后，立即计算告警状态
                        UpdateAlarmStates();

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
            // 触发模块功耗模式切换：尝试通过 IIC 写入模块（默认写 A0 页的 0x00 寄存器），
            // 如果实际模块寄存器地址不同，请在此处修改为正确的页/偏移
            try
            {
                byte pageIndex = 0x00; // 可根据模块手册调整
                byte value = IsHighPowerMode ? (byte)1 : (byte)0;
                bool ok = _iicCom.WriteByte("A0", pageIndex, value);
                AddLog(ok ? "模块功耗模式写入成功" : "模块功耗模式写入失败（请检查目标寄存器地址）");
            }
            catch (Exception ex)
            {
                AddLog($"切换功耗模式异常：{ex.Message}");
            }
        }

        [RelayCommand]
        public void ResetModule()
        {
            // 发送模块软复位指令（保守实现：写入 A0 页某寄存器触发软复位）
            try
            {
                // 注意：不同模块软复位寄存器不同，请根据模块手册调整 offset
                byte resetOffset = 0x01;
                bool ok = _iicCom.WriteByte("A0", resetOffset, 0x01);
                AddLog(ok ? "模块软复位命令已发送" : "模块软复位写入失败（请确认复位寄存器地址）");
            }
            catch (Exception ex)
            {
                AddLog($"模块复位异常：{ex.Message}");
            }
        }

        #endregion

        #region ====================== 温度等级选择 ======================

        [RelayCommand]
        public void SelectIndustrialGrade()
        {
            SelectedTempGrade = "工业级";
            TempHighSetting = 85;
            TempLowSetting = -40;
            // 选择预设时禁止编辑数值
            IsTempCustom = false;
            AddLog("选择工业级温度范围：-40℃ ~ +85℃（值不可编辑）");
        }

        [RelayCommand]
        public void SelectCommercialGrade()
        {
            SelectedTempGrade = "商业级";
            TempHighSetting = 70;
            TempLowSetting = 0;
            IsTempCustom = false;
            AddLog("选择商业级温度范围：0℃ ~ +70℃（值不可编辑）");
        }

        [RelayCommand]
        public void SelectExtendedGrade()
        {
            SelectedTempGrade = "扩展级";
            TempHighSetting = 85;
            TempLowSetting = -20;
            IsTempCustom = false;
            AddLog("选择扩展级温度范围：-20℃ ~ +85℃（值不可编辑）");
        }

        [RelayCommand]
        public void SelectCustomTemp()
        {
            SelectedTempGrade = "自定义";
            IsTempCustom = true;
            AddLog("选择自定义温度：允许修改高低温值");
        }

        [RelayCommand]
        public void SetHighTemp()
        {
            AddLog($"设置高温测试点：{TempHighSetting}℃，开始仅选中测试项目");
            // 启动单温度、仅执行所选测试
            Task.Run(async () =>
            {
                try
                {
                    if (IsBusy) return;
                    IsBusy = true;
                    _testCts = new CancellationTokenSource();
                    await RunSingleTempTest("高温", TempHighSetting, _testCts.Token);
                }
                catch (Exception ex)
                {
                    AddLog($"高温点单项测试异常：{ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        [RelayCommand]
        public void SetLowTemp()
        {
            AddLog($"设置低温测试点：{TempLowSetting}℃，开始仅选中测试项目");
            Task.Run(async () =>
            {
                try
                {
                    if (IsBusy) return;
                    IsBusy = true;
                    _testCts = new CancellationTokenSource();
                    await RunSingleTempTest("低温", TempLowSetting, _testCts.Token);
                }
                catch (Exception ex)
                {
                    AddLog($"低温点单项测试异常：{ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        [RelayCommand]
        public void SetRoomTemp()
        {
            AddLog($"设置常温测试点：{TempRoomSetting}℃，开始仅选中测试项目");
            Task.Run(async () =>
            {
                try
                {
                    if (IsBusy) return;
                    IsBusy = true;
                    _testCts = new CancellationTokenSource();
                    await RunSingleTempTest("常温", TempRoomSetting, _testCts.Token);
                }
                catch (Exception ex)
                {
                    AddLog($"常温点单项测试异常：{ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            });
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

        /// <summary>
        /// 根据实时值与阈值比较，更新各监控项告警状态
        /// </summary>
        private void UpdateAlarmStates()
        {
            IsTempAlarm = RealTime.Temperature > Thresholds.TempHighAlarm
                          || RealTime.Temperature < Thresholds.TempLowAlarm;

            IsVoltAlarm = RealTime.Voltage > Thresholds.VccHighAlarm
                          || RealTime.Voltage < Thresholds.VccLowAlarm;

            IsBiasAlarm = RealTime.BiasCurrent > Thresholds.BiasHighAlarm
                          || RealTime.BiasCurrent < Thresholds.BiasLowAlarm;

            IsTxPowerAlarm = RealTime.TxPower > Thresholds.TxPowerHighAlarm
                          || RealTime.TxPower < Thresholds.TxPowerLowAlarm;

            IsRxPowerAlarm = RealTime.RxPower > Thresholds.RxPowerHighAlarm
                          || RealTime.RxPower < Thresholds.RxPowerLowAlarm;
        }
    }
}
