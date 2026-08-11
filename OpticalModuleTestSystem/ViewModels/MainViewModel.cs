using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticalModuleTestSystem.Configs;
using OpticalModuleTestSystem.Drivers;
using OpticalModuleTestSystem.Models;
using OpticalModuleTestSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpticalModuleTestSystem.ViewModels
{
    /// <summary>
    /// 主ViewModel - 完全遵循MVVM模式（重构优化版）
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        // 扫描仪器
        private readonly InstrumentScanner _scanner = new();
        private readonly InstrumentInitializer _initializer = new();

        // 通讯面板
        private readonly IICCom _iicCom;
        private CancellationTokenSource _testCts;
        private CancellationTokenSource _thermalFlowCts;

        // 日志防抖缓冲
        private readonly StringBuilder _logBuffer = new();
        private readonly object _logLock = new();
        private DateTime _lastLogFlush = DateTime.MinValue;
        private readonly TimeSpan _logFlushInterval = TimeSpan.FromMilliseconds(100);

        // 在 MainViewModel 里加一个配置实例
        public ScopeModuleConfig ScopeConfig { get; } = new();
        #region === Observable Properties ===

        [ObservableProperty]
        private ObservableCollection<InstrumentInfo> _instruments = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelfTestSelectedInstrumentCommand))]
        private InstrumentInfo _selectedInstrument;

        [ObservableProperty]
        private string _log = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelfTestSelectedInstrumentCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private ObservableCollection<TemperatureTestResult> _testResults = new();

        [ObservableProperty]
        private string _testStatus = "就绪";

        [ObservableProperty]
        private Brush _testStatusColor = new SolidColorBrush(Colors.Gray);

        // 温度设定值
        [ObservableProperty]
        private double _tempHighSetting = 85.0;

        [ObservableProperty]
        private double _tempLowSetting = -40.0;

        [ObservableProperty]
        private double _tempRoomSetting = 25.0;

        [ObservableProperty]
        private double _currentTemperature;

        [ObservableProperty]
        private double _currentRxPower;

        // PID 控制参数
        [ObservableProperty]
        private double _pidKp = 0.5;

        [ObservableProperty]
        private double _pidKi = 0.02;

        [ObservableProperty]
        private double _pidKd = 0.1;

        [ObservableProperty]
        private double _pidMaxStep = 3.0;

        [ObservableProperty]
        private int _pidCheckSec = 5;

        [ObservableProperty]
        private double _pidStableTol = 0.5;

        [ObservableProperty]
        private int _pidStableDuration = 120;

        [ObservableProperty]
        private int _pidMinSetIntervalSeconds = 120;

        [ObservableProperty]
        private double _pidChangeRateThresholdDegPerMin = 0.05;

        // 分段缓升/缓降参数
        [ObservableProperty]
        private double _rampStepSizeDeg = 2.0;

        [ObservableProperty]
        private int _rampStepIntervalSeconds = 60;

        [ObservableProperty]
        private double _rampMaxSlopeDegPerMin = 1.0;

        [ObservableProperty]
        private string _selectedPackage = "SFP+";

        [ObservableProperty]
        private string _selectedOpticalProtocol = "SFF-8472";

        [ObservableProperty]
        private string _selectedElectricProtocol = "1000BASE-T";

        [ObservableProperty]
        private string _selectedOpticalRate = "10G";

        [ObservableProperty]
        private string _selectedElectricRate = "10G";

        [ObservableProperty]
        private string _selectedModulation = "NRZ";

        // 下拉选项集合
        public ObservableCollection<string> PackageOptions { get; } = new() { "SFP+", "QSFP28", "QSFP-DD", "SFP28" };
        public ObservableCollection<string> OpticalProtocolOptions { get; } = new() { "SFF-8472", "SFF-8636", "CMIS" };
        public ObservableCollection<string> ElectricProtocolOptions { get; } = new() { "1000BASE-T", "10GBASE-T", "25GBASE-CR" };
        public ObservableCollection<string> OpticalRateOptions { get; } = new() { "10G", "25G", "100G", "400G" };
        public ObservableCollection<string> ElectricRateOptions { get; } = new() { "1.5G", "10G", "25G", "50G", "100G", "500G" };
        public ObservableCollection<string> ModulationOptions { get; } = new() { "NRZ", "PAM4" };

        [ObservableProperty]
        private bool _isElectricPort = true;

        [ObservableProperty]
        private string _selectedTempGrade = "商业级";

        // DDM数据绑定
        public DdmRealTime RealTime { get; } = new();
        public DdmThresholds Thresholds { get; } = new();
        public ModuleInfo ModuleInfo { get; } = new();
        public AlarmStatus Alarm { get; } = new();

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

        [ObservableProperty]
        private bool _isTempControllerConnected;

        [ObservableProperty]
        private bool _isTempCustom = false;

        [ObservableProperty]
        private bool _isBertConnected;

        // 性能测试项开关
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

        #region === 86100D 通道映射配置（0=自动探测）===

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelfTestSelectedInstrumentCommand))]
        private int _scopeTxChannel = 0;   // 0=自动, 1~4=手动指定

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelfTestSelectedInstrumentCommand))]
        private int _scopeRxChannel = 0;   // 0=自动, 1~4=手动指定

        #endregion

        #region === 全选/子项联动 ===

        partial void OnSelectAllTestsChanged(bool value)
        {
            SetAllTestItems(value);
        }

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

        private void SetAllTestItems(bool value)
        {
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

            NotifyTestItemChanges();
        }

        private void NotifyTestItemChanges()
        {
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

        private void UpdateSelectAllFromChildren()
        {
            bool all = _txSingleChannelPower && _txEyeMargin && _txTdecq && _txExtinctionRatio
                       && _txCenterWavelength && _txSpectralWidth && _txSmsr && _txPowerAccuracy
                       && _rxSingleChannelSensitivity && _rxBerPowerTrend && _rxLosa
                       && _rxLosd && _rxLosHysteresis && _rxPowerAccuracy;

            if (_selectAllTests != all)
            {
                _selectAllTests = all;
                OnPropertyChanged(nameof(SelectAllTests));
            }
        }

        #endregion

        #region === 协议/速率联动 ===

        private ObservableCollection<string> _currentProtocolOptions = new();
        public ObservableCollection<string> CurrentProtocolOptions
        {
            get => _currentProtocolOptions;
            set => SetProperty(ref _currentProtocolOptions, value);
        }

        private ObservableCollection<string> _currentRateOptions = new();
        public ObservableCollection<string> CurrentRateOptions
        {
            get => _currentRateOptions;
            set => SetProperty(ref _currentRateOptions, value);
        }

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

        #endregion

        #region === DebugValue 字符串转换 ===

        public string DebugValueString
        {
            get => DebugValue.ToString("X2");
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                string s = value.Trim();
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
                if (byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    DebugValue = b;
                }
            }
        }

        #endregion

        public MainViewModel(IICCom iicCom)
        {
            _iicCom = iicCom ?? throw new ArgumentNullException(nameof(iicCom));

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

        #region ====================== 仪器自检 ======================

        [RelayCommand(CanExecute = nameof(CanSelfTestSelectedInstrument))]
        public async Task SelfTestSelectedInstrument()
        {
            if (SelectedInstrument == null)
            {
                AddLog("未选择仪器进行自检。");
                return;
            }

            IsBusy = true;
            var inst = SelectedInstrument;
            AddLog($"开始对 {inst.Name} (@{inst.GpibAddress}) 执行自检...");

            try
            {
                await Task.Run(async () =>
                {
                    using var gpib = new GpibCommunicator();
                    if (!gpib.Connect(inst.GpibAddress))
                    {
                        AddLog($"{inst.Name} 连接失败 (GPIB {inst.GpibAddress})");
                        return;
                    }

                    await RunInstrumentSelfTestAsync(gpib, inst);
                    gpib.Disconnect();
                    AddLog($"{inst.Name} 自检完成。");
                });
            }
            catch (Exception ex)
            {
                AddLog($"自检异常：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RunInstrumentSelfTestAsync(GpibCommunicator gpib, InstrumentInfo inst)
        {
            // 1. 基础通信自检
            var idn = gpib.Identify() ?? string.Empty;
            await DispatcherInvokeAsync(() => inst.IdnString = idn);
            AddLog($"{inst.Name} IDN: {idn}");

            gpib.ClearStatus();
            var syserr = gpib.QuerySystemError();
            if (!string.IsNullOrWhiteSpace(syserr) && !syserr.StartsWith("0,"))
                AddLog($"{inst.Name} 系统错误: {syserr}");
            else
                AddLog($"{inst.Name} 系统错误队列清空或无错误。");

            var stb = gpib.GetStatusByte();
            if (stb >= 0) AddLog($"{inst.Name} 状态字节: {stb}");

            // 2. 仪器类型特定功能验证
            string modelUpper = inst.Model?.ToUpper() ?? string.Empty;

            if (modelUpper.Contains("86100D"))
                await SelfTest86100DAsync(gpib, inst);

            if (modelUpper.Contains("MS9740A"))
                IsSpectrumAnalyzerConnected = true;

            if (modelUpper.Contains("IQS-610P") || modelUpper.Contains("IQS600"))
                IsAttenuatorConnected = true;

            if (modelUpper.Contains("ATS-545"))
                IsTempControllerConnected = true;

            if (modelUpper.Contains("MP1900A"))
                await SelfTestMP1900AAsync(gpib, inst);
        }

        private async Task SelfTest86100DAsync(GpibCommunicator gpib, InstrumentInfo inst)
        {
            AddLog($"{inst.Name} 检测到采样示波器，执行眼图功能验证...");

            try
            {
                gpib.Write("*CLS");
                gpib.Write(":SYSTEM:MODE EYE");
                await Task.Delay(500);

                // 速率配置
                double dataRateHz = ParseSelectedRateToHz();
                if (dataRateHz > 0)
                {
                    gpib.Write($":CRECOVERY1:CRATE {dataRateHz:E}");
                    gpib.Write($":TRIGger:BRATe {dataRateHz:E}");
                    AddLog($"{inst.Name} 数据速率已设置为 {dataRateHz / 1e9:F3} Gb/s");
                }

                // 根据速率自动推导通道：10G→1/2，25G→3/4
                int txCh = ResolveScopeChannel("Tx");
                int rxCh = ResolveScopeChannel("Rx");
                AddLog($"{inst.Name} 当前速率 {GetCurrentRate()}，使用通道 Tx=CH{txCh}, Rx=CH{rxCh}");

                // ========== 模板只加载一次（Tx/Rx 共用）==========
                string maskFile = GetMaskFileNameForCurrentRate();
                bool hasMask = !string.IsNullOrEmpty(maskFile);
                if (hasMask)
                {
                    gpib.Write($":MTESt:LOAD \"{maskFile}\"");
                    await Task.Delay(300);
                    AddLog($"{inst.Name} 模板已加载: {maskFile}");
                }

                // 测 Tx
                EyeTxResult txEyeResult = new EyeTxResult();
                await Test86100DChannelAsync(gpib, inst, "Tx", txCh, hasMask, txEyeResult, null);

                // 测 Rx
                EyeRxResult rxEyeResult = new EyeRxResult();
                await Test86100DChannelAsync(gpib, inst, "Rx", rxCh, hasMask, null, rxEyeResult);

                if (hasMask) gpib.Write(":MTESt:STOP");

                AddLog($"{inst.Name} 眼图功能验证通过");
                IsOscilloscopeConnected = true;
            }
            catch (Exception ex)
            {
                AddLog($"{inst.Name} 眼图功能验证异常: {ex.Message}");
            }
        }

        private string GetCurrentRate()
        {
            return _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
        }
        private async Task SelfTestMP1900AAsync(GpibCommunicator gpib, InstrumentInfo inst)
        {
            AddLog($"{inst.Name} 检测到误码仪，执行基础功能验证...");

            try
            {
                string modulation = _selectedModulation?.ToUpper() ?? "NRZ";
                gpib.Write($":SOURce1:MODulation {modulation}");
                AddLog($"{inst.Name} 调制方式已设置为 {modulation}");

                double dataRateHz = ParseSelectedRateToHz();
                if (dataRateHz > 0)
                {
                    gpib.Write($":SOURce1:BITRate {dataRateHz:E}");
                    AddLog($"{inst.Name} 速率已设置为 {dataRateHz / 1e9:F3} Gb/s");
                }
                else
                {
                    AddLog($"{inst.Name} 警告：未能从UI解析有效速率，跳过速率配置");
                }

                gpib.Write(":SOURce:OUTPut:ASET ON");
                AddLog($"{inst.Name} 信号输出已开启");

                gpib.Write(":SENSe:MEASure:ASTRt");
                AddLog($"{inst.Name} 测量已启动");

                IsBertConnected = true;
                AddLog($"{inst.Name} 误码仪基础自检完成");
            }
            catch (Exception ex)
            {
                AddLog($"{inst.Name} 误码仪自检异常: {ex.Message}");
            }
        }

        private bool CanSelfTestSelectedInstrument()
        {
            return SelectedInstrument != null && !IsBusy;
        }

        #endregion

        #region ====================== 仪器扫描 ======================

        [RelayCommand(CanExecute = nameof(CanScanInstruments))]
        public async Task ScanInstruments()
        {
            if (IsBusy) return;
            IsBusy = true;
            AddLog("开始扫描GPIB仪器...");

            try
            {
                var list = await Task.Run(() => _scanner.ScanAll());

                await DispatcherInvokeAsync(() =>
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
                AddLog($"扫描异常：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanScanInstruments() => !IsBusy;

        private void UpdateInstrumentConnectionStatus(List<InstrumentInfo> list)
        {
            // 辅助：忽略大小写的包含判断
            static bool ContainsIgnore(string source, string value)
            {
                return !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // 扩展匹配：同时检查 Model、Name 和原始 IDN 字符串，避免因为 IDN 格式差异导致匹配失败
            IsOscilloscopeConnected = list.Any(i => ContainsIgnore(i.Model, "86100") || ContainsIgnore(i.IdnString, "86100") || ContainsIgnore(i.Name, "86100"));
            IsSpectrumAnalyzerConnected = list.Any(i => ContainsIgnore(i.Model, "MS9740") || ContainsIgnore(i.IdnString, "MS9740") || ContainsIgnore(i.Name, "MS9740"));
            IsAttenuatorConnected = list.Any(i => ContainsIgnore(i.Model, "IQS-610P") || ContainsIgnore(i.Model, "IQS600") || ContainsIgnore(i.IdnString, "IQS-610P") || ContainsIgnore(i.IdnString, "IQS600") || ContainsIgnore(i.IdnString, "EXFO") || ContainsIgnore(i.Name, "EXFO"));
            IsTempControllerConnected = list.Any(i => ContainsIgnore(i.Model, "ATS-545") || ContainsIgnore(i.IdnString, "ATS-545") || ContainsIgnore(i.IdnString, "TEMPTRONIC") || ContainsIgnore(i.Name, "ATS-545"));
            IsBertConnected = list.Any(i => ContainsIgnore(i.Model, "MP1900A") || ContainsIgnore(i.IdnString, "MP1900") || ContainsIgnore(i.Name, "MP1900"));

            // 简单检测热流仪：匹配 Model/Name/IDN 中包含关键词
            IsThermalFlowConnected = list.Any(i => ContainsIgnore(i.Model, "FLOW") || ContainsIgnore(i.IdnString, "FLOW") || ContainsIgnore(i.Name, "FLOW") || ContainsIgnore(i.Model, "THERM") || ContainsIgnore(i.IdnString, "THERM") || ContainsIgnore(i.Name, "THERM") || ContainsIgnore(i.Model, "热流") || ContainsIgnore(i.Name, "热流"));

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

        [RelayCommand(CanExecute = nameof(CanInitializeAll))]
        public async Task InitializeAll()
        {
            if (IsBusy) return;
            IsBusy = true;
            AddLog("开始初始化数据...");

            try
            {
                string selectedPackage = SelectedPackage;
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

                var (allSuccess, logs, perInstrument) = await _initializer.InitializeAllByRateAsync(
                    Instruments, rateLevel, selectedPackage, selectedProtocol, selectedRate, selectedModulation);

                foreach (var log in logs)
                    AddLog(log);

                //foreach (var inst in Instruments)
                //{
                //    if (!inst.IsTargetDevice) continue;
                //    if (perInstrument != null && perInstrument.TryGetValue(inst.GpibAddress, out bool ok))
                //    {
                //        inst.Status = ok ? ConnectStatus.Ready : ConnectStatus.Error;
                //        inst.StatusColor = ok ? "#4CD964" : "#FF3B30";
                //    }
                //    else
                //    {
                //        inst.Status = allSuccess ? ConnectStatus.Ready : ConnectStatus.Error;
                //        inst.StatusColor = allSuccess ? "#4CD964" : "#FF3B30";
                //    }
                //}

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

        private bool CanInitializeAll() => !IsBusy;

        /// <summary>
        /// 从模块型号自动识别速率等级（修复：先匹配长字符串）
        /// </summary>
        private string GetModuleRateLevel()
        {
            string model = ModuleInfo.Model?.ToUpper() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model))
                return _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;

            // ✅ 先匹配 400G/100G，避免 100G 被 Contains("10G") 误判
            if (model.Contains("400G")) return "400G";
            if (model.Contains("100G")) return "100G";
            if (model.Contains("25G")) return "25G";
            if (model.Contains("10G")) return "10G";

            return _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
        }

        #endregion

        #region ====================== 全温度段测试 ======================

        [RelayCommand(CanExecute = nameof(CanRunFullTemperatureTest))]
        public async Task RunFullTemperatureTest()
        {
            if (IsBusy) return;
            IsBusy = true;
            _testCts = new CancellationTokenSource();
            var token = _testCts.Token;

            UpdateTestStatus("开始全温度段测试...", Colors.Orange);
            AddLog("启动全温度段测试流程");

            try
            {
                await RunSingleTempTest("常温", TempRoomSetting, token);
                if (token.IsCancellationRequested) return;

                await RunSingleTempTest("低温", TempLowSetting, token);
                if (token.IsCancellationRequested) return;

                await RunSingleTempTest("高温", TempHighSetting, token);
                if (token.IsCancellationRequested) return;

                await ReturnToRoomTemp(token);

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

        private bool CanRunFullTemperatureTest() => !IsBusy;

        [RelayCommand]
        public void StopTest()
        {
            _testCts?.Cancel();
            _ = Task.Run(async () =>
            {
                try
                {
                    await ReturnToRoomTemp(CancellationToken.None);
                    await StopThermalFlowWhenRoomTemp(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AddLog($"停止测试时异常：{ex.Message}");
                }
            });
            AddLog("用户请求停止测试");
        }

        #region === 单点温度测试命令（修复并发问题）===

        [RelayCommand(CanExecute = nameof(CanRunSinglePointTest))]
        public async Task RunHighTempTestAsync()
        {
            AddLog($"设置高温测试点：{TempHighSetting}℃，开始仅选中测试项目");
            await ExecuteSingleTempTestAsync("高温", TempHighSetting);
        }

        [RelayCommand(CanExecute = nameof(CanRunSinglePointTest))]
        public async Task RunLowTempTestAsync()
        {
            AddLog($"设置低温测试点：{TempLowSetting}℃，开始仅选中测试项目");
            await ExecuteSingleTempTestAsync("低温", TempLowSetting);
        }

        [RelayCommand(CanExecute = nameof(CanRunSinglePointTest))]
        public async Task RunRoomTempTestAsync()
        {
            AddLog($"设置常温测试点：{TempRoomSetting}℃，开始仅选中测试项目");
            await ExecuteSingleTempTestAsync("常温", TempRoomSetting);
        }

        private bool CanRunSinglePointTest() => !IsBusy;

        private async Task ExecuteSingleTempTestAsync(string tempType, double targetTemp)
        {
            if (IsBusy) return;
            IsBusy = true;
            _testCts = new CancellationTokenSource();
            try
            {
                await RunSingleTempTest(tempType, targetTemp, _testCts.Token);
            }
            catch (Exception ex)
            {
                AddLog($"{tempType}点单项测试异常：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                _testCts?.Dispose();
                _testCts = null;
            }
        }

        #endregion

        #endregion

        #region ====================== 温度控制核心 ======================

        /// <summary>
        /// 执行单温度段测试
        /// </summary>
        private async Task RunSingleTempTest(string tempType, double targetTemp, CancellationToken token)
        {
            int ast545Addr = GetInstrumentAddress("ATS-545");
            if (ast545Addr == -1)
            {
                AddLog($"未找到Temptronic ATS-545温控平台，{tempType}测试跳过");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(ast545Addr))
            {
                AddLog($"温控平台连接失败，{tempType}测试跳过");
                return;
            }

            UpdateTestStatus($"{tempType}测试：等待温度稳定...", Colors.Orange);

            bool isStable = await StabilizeTemperatureAsync(tempType, targetTemp, gpib, token);
            if (!isStable)
            {
                AddLog($"{tempType}测试：温度未稳定，测试终止");
                return;
            }

            UpdateTestStatus($"{tempType}测试：采集模块实时数据...", Colors.Orange);
            var result = await CollectModuleDataAsync(tempType, targetTemp, token);

            // 执行各仪器测试
            if (TxEyeMargin || TxTdecq || TxSmsr || TxPowerAccuracy || RxBerPowerTrend)
                await RunEyeTestsAsync(result, token);

            if (TxCenterWavelength || TxSpectralWidth || TxSmsr || RxPowerAccuracy)
                await RunSpectrumTestsAsync(result, token);

            if (TxSingleChannelPower || RxSingleChannelSensitivity || RxBerPowerTrend)
                await RunEXFOAttenuationTestAsync(result, token);

            if (RxBerPowerTrend)
                await RunMP1900ABerTestAsync(result, token);

            gpib.StopTemperatureControl();
            await DispatcherInvokeAsync(() => TestResults.Add(result));
            AddLog($"{tempType}测试完成：稳定温度 {result.StableTemp:F2}℃");
        }

        /// <summary>
        /// 温度稳定主控：分段Ramp + PID稳定
        /// </summary>
        private async Task<bool> StabilizeTemperatureAsync(string tempType, double targetTemp, GpibCommunicator gpib, CancellationToken token)
        {
            if (tempType == "常温")
            {
                UpdateTestStatus($"{tempType}测试：确认模块温度在 25~30℃ 范围并稳定...", Colors.Orange);
                // ✅ 修复：常温不再用单点 25℃ ±5℃ 的 PID，而是明确监控区间 [25, 30]
                return await WaitTempInRangeAsync(min: 25.0, max: 30.0, gpib, token);
            }

            // 低温/高温：先配置热流仪
            if (tempType == "低温" && Math.Abs(targetTemp + 40.0) < 0.5)
            {
                await ConfigureThermalFlowTargetAsync(targetTemp - 10.0, token);
                await Task.Delay(2000, token);
                return await RunPidStabilizerAsync(targetTemp, gpib, token, overrideStableTol: 1.0);
            }

            if (tempType == "高温" && Math.Abs(targetTemp - TempHighSetting) < 0.5)
            {
                await ConfigureThermalFlowTargetAsync(targetTemp + 10.0, token);
                await Task.Delay(2000, token);
                return await RunPidStabilizerAsync(targetTemp, gpib, token, overrideStableTol: 1.0);
            }

            return await RunPidStabilizerAsync(targetTemp, gpib, token);
        }

        /// <summary>
        /// 分段缓升/缓降至目标平台设定
        /// </summary>
        private async Task<double> RampToTargetAsync(double targetPlatformSet, GpibCommunicator gpib, CancellationToken token)
        {
            double thermalOffset = 12.0;
            double currentPlatformSet = gpib.GetSetTemperature();

            if (Math.Abs(currentPlatformSet) < 0.0001)
                currentPlatformSet = targetPlatformSet;

            double remaining = targetPlatformSet - currentPlatformSet;

            if (Math.Abs(remaining) <= 0.02)
                return currentPlatformSet;

            double sign = Math.Sign(remaining);
            double maxSlope = Math.Max(0.01, RampMaxSlopeDegPerMin);

            while (Math.Abs(remaining) > 0.02)
            {
                token.ThrowIfCancellationRequested();

                double absRem = Math.Abs(remaining);
                double desiredStep = absRem > 5.0 ? 2.0 : (absRem > 1.0 ? 1.0 : 0.2);
                desiredStep = Math.Min(desiredStep, RampStepSizeDeg);

                int desiredInterval = absRem > 5.0 ? 10 : (absRem > 1.0 ? 5 : 2);
                double allowedStep = maxSlope * (desiredInterval / 60.0);

                if (desiredStep > allowedStep)
                    desiredStep = Math.Max(0.01, allowedStep);

                double step = sign * Math.Min(desiredStep, Math.Abs(remaining));
                double nextSet = Math.Clamp(currentPlatformSet + step, -50.0, 150.0);

                gpib.SetTemperature(nextSet);
                AddLog($"分段下发平台设定：{currentPlatformSet:F2} -> {nextSet:F2} ℃，等待 {desiredInterval}s...");
                currentPlatformSet = nextSet;
                remaining = targetPlatformSet - currentPlatformSet;

                await Task.Delay(desiredInterval * 1000, token);
            }

            return currentPlatformSet;
        }

        /// <summary>
        /// PID 稳定器（核心温控算法）
        /// </summary>
        private async Task<bool> RunPidStabilizerAsync(double targetTemp, GpibCommunicator gpib, CancellationToken token, double? overrideStableTol = null)
        {
            int checkSec = Math.Max(1, PidCheckSec);
            double stableTol = Math.Max(0.1, overrideStableTol ?? PidStableTol);
            int stableDuration = Math.Max(10, PidStableDuration);
            double maxStep = Math.Max(0.5, PidMaxStep);

            double thermalOffset = 12.0;
            double targetPlatformSet = Math.Clamp(targetTemp - thermalOffset, -50.0, 150.0);

            // 先执行 Ramp
            double platformSet = await RampToTargetAsync(targetPlatformSet, gpib, token);

            AddLog($"温控(PID)启动：平台{platformSet:F1}℃ → 目标模块{targetTemp}℃");

            double integral = 0.0;
            double prevError = 0.0;
            int stableSec = 0;
            DateTime lastTime = DateTime.UtcNow;
            DateTime lastSetTime = DateTime.UtcNow;

            var samples = new List<(DateTime t, double temp)>();

            while (stableSec < stableDuration)
            {
                await Task.Delay(checkSec * 1000, token);
                token.ThrowIfCancellationRequested();

                // 刷新 DDM 温度
                await RefreshDdmTemperatureAsync();

                DateTime now = DateTime.UtcNow;
                double dt = Math.Max(checkSec, (now - lastTime).TotalSeconds);
                lastTime = now;

                double moduleTemp = CurrentTemperature;
                double error = targetTemp - moduleTemp;

                // PID
                integral += error * dt;
                double derivative = (error - prevError) / dt;
                prevError = error;

                double pidOut = PidKp * error + PidKi * integral + PidKd * derivative;
                double delta = Math.Clamp(pidOut, -maxStep, maxStep);
                double newSet = Math.Clamp(platformSet + delta, -50.0, 150.0);

                // 计算变化率
                samples.Add((now, moduleTemp));
                var window = TimeSpan.FromMinutes(3);
                samples.RemoveAll(s => (now - s.t) > window);

                double changeRatePerMin = 0.0;
                if (samples.Count >= 2)
                {
                    var first = samples.First();
                    var last = samples.Last();
                    double minutes = (last.t - first.t).TotalMinutes;
                    if (minutes > 0) changeRatePerMin = (last.temp - first.temp) / minutes;
                }

                bool allowSetInterval = (now - lastSetTime).TotalSeconds >= Math.Max(1, PidMinSetIntervalSeconds);
                bool allowSetRate = Math.Abs(changeRatePerMin) <= Math.Max(0.0001, PidChangeRateThresholdDegPerMin);

                if (Math.Abs(newSet - platformSet) >= 0.05)
                {
                    if (allowSetInterval || allowSetRate)
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

                // 自适应热阻估计
                double actualOffset = moduleTemp - platformSet;
                thermalOffset = thermalOffset * 0.8 + actualOffset * 0.2;

                // 稳定性判断
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

        /// <summary>
        /// 异步刷新 DDM 温度（批量读取）
        /// </summary>
        private async Task RefreshDdmTemperatureAsync()
        {
            try
            {
                byte[] a0 = await Task.Run(() => _iicCom.ReadPage("A0", 256));
                byte[] a2 = await Task.Run(() => _iicCom.ReadPage("A2", 256));

                var ext = DdmParser.ParseDdmExt(a2);
                await DispatcherInvokeAsync(() =>
                {
                    RealTime.Temperature = ext.Temperature;
                    RealTime.Voltage = ext.Voltage;
                    RealTime.BiasCurrent = ext.BiasCurrent;
                    RealTime.TxPower = ext.TxPower;
                    RealTime.RxPower = ext.RxPower;
                    CurrentTemperature = ext.Temperature;
                    CurrentRxPower = ext.RxPower;
                });
            }
            catch { /* 忽略单次读取错误 */ }
        }

        /// <summary>
        /// 采集模块数据并生成结果对象
        /// </summary>
        private async Task<TemperatureTestResult> CollectModuleDataAsync(string tempType, double targetTemp, CancellationToken token)
        {
            await RefreshDdmTemperatureAsync();

            TemperatureTestResult result = null;
            await DispatcherInvokeAsync(() =>
            {
                result = new TemperatureTestResult
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
            });

            return result;
        }

        /// <summary>
        /// 恢复常温
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

            bool isStable = await RunPidStabilizerAsync(TempRoomSetting, gpib, token);
            AddLog(isStable ? "常温恢复完成，温度已稳定" : "常温恢复超时，但仍停止温控");

            gpib.StopTemperatureControl();
        }

        #endregion

        #region ====================== 热流仪控制 ======================

        private void StartThermalFlowMonitor()
        {
            if (_thermalFlowCts != null) return;

            _thermalFlowCts = new CancellationTokenSource();
            var token = _thermalFlowCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    int addr = GetInstrumentAddress("ATS-545");
                    if (addr == -1) addr = GetInstrumentAddress("THERM");
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
                            string resp = await QueryThermalFlowTempAsync(gpib);
                            if (!string.IsNullOrWhiteSpace(resp) && double.TryParse(resp, out double t))
                            {
                                await DispatcherInvokeAsync(() => ThermalFlowOutletTemp = t);
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

        private async Task<string> QueryThermalFlowTempAsync(GpibCommunicator gpib)
        {
            string[] cmds = new[] { ":MEAS:TEMP?", "MEAS:TEMP?", "TEMP?", ":SENS:TEMP?", "T?" };
            foreach (var c in cmds)
            {
                var resp = gpib.Query(c);
                if (!string.IsNullOrWhiteSpace(resp))
                {
                    resp = resp.Trim();
                    if (double.TryParse(resp, out _)) return resp;

                    // 处理带单位的情况，如 "23.5 C"
                    var num = new string(resp.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                    if (double.TryParse(num, out _)) return num;
                }
                await Task.Delay(50);
            }
            return null;
        }

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

        /// <summary>
        /// 等待温度持续稳定在指定区间内（用于常温测试）
        /// </summary>
        /// <param name="min">区间下限（℃）</param>
        /// <param name="max">区间上限（℃）</param>
        /// <param name="gpib">温控平台连接（用于必要时微调）</param>
        /// <param name="token">取消令牌</param>
        /// <returns>是否稳定达标</returns>
        private async Task<bool> WaitTempInRangeAsync(double min, double max, GpibCommunicator gpib, CancellationToken token)
        {
            int checkSec = Math.Max(1, PidCheckSec);
            int stableDuration = 60; // 常温只需稳定 60 秒
            int stableSec = 0;
            bool hasAdjusted = false;
            double? platformSet = null;

            AddLog($"常温区间稳定启动：目标范围 {min:F1}℃ ~ {max:F1}℃");

            while (stableSec < stableDuration)
            {
                await Task.Delay(checkSec * 1000, token);
                token.ThrowIfCancellationRequested();

                await RefreshDdmTemperatureAsync();
                double temp = CurrentTemperature;

                if (temp >= min && temp <= max)
                {
                    stableSec += checkSec;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] 常温稳定计时 {stableSec}/{stableDuration} | 当前 {temp:F2}℃（范围 {min:F1}~{max:F1}℃）");
                }
                else
                {
                    stableSec = 0;

                    // 如果温度偏离区间，启动温控平台微调一次（避免频繁下发）
                    if (!hasAdjusted || Math.Abs(temp - (platformSet ?? 0)) > 3.0)
                    {
                        double target = temp < min ? 28.0 : 27.0; // 低时加热到28，高时制冷到27
                        gpib.SetTemperature(target);
                        platformSet = target;
                        hasAdjusted = true;
                        AddLog($"[{DateTime.Now:HH:mm:ss}] 常温调整：当前 {temp:F2}℃，启动平台设定 {target:F1}℃");
                    }

                    AddLog($"[{DateTime.Now:HH:mm:ss}] 常温未达标：当前 {temp:F2}℃（目标范围 {min:F1}~{max:F1}℃）");
                }
            }

            AddLog($"常温区间稳定完成：温度持续在 {min:F1}~{max:F1}℃ 范围内 {stableDuration} 秒");
            return true;
        }

        private async Task<bool> ConfigureThermalFlowTargetAsync(double setTemp, CancellationToken token)
        {
            int addr = GetInstrumentAddress("FLOW");
            if (addr == -1) addr = GetInstrumentAddress("THERM");
            if (addr == -1) addr = GetInstrumentAddress("ATS-545");
            if (addr == -1)
            {
                AddLog($"未找到热流仪，无法设置热流目标 {setTemp:F1}℃");
                return false;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(addr))
            {
                AddLog($"连接热流仪失败，无法设置热流目标 {setTemp:F1}℃");
                return false;
            }

            AddLog($"尝试配置热流仪目标温度 {setTemp:F1}℃ @ GPIB {addr} ...");

            string[] cmds = new[]
            {
                $"SETP {setTemp:F1}",
                $"TEMP {setTemp:F1}",
                $"SETT {setTemp:F1}",
                $"T {setTemp:F1}",
                $":SOUR:TEMP {setTemp:F1}"
            };

            foreach (var c in cmds)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    gpib.Write(c);
                    await Task.Delay(300, token);
                }
                catch { }
            }

            try { gpib.Write("FLOW 1"); } catch { }

            try
            {
                await Task.Delay(800, token);
                string resp = gpib.Query(":MEAS:TEMP?") ?? gpib.Query("TEMP?");
                if (!string.IsNullOrWhiteSpace(resp))
                {
                    var num = new string(resp.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                    if (double.TryParse(num, out double readT))
                    {
                        AddLog($"热流仪已反馈温度 {readT:F2}℃ (期望 {setTemp:F1}℃)");
                    }
                }
            }
            catch { }

            gpib.Disconnect();
            return true;
        }

        private async Task StopThermalFlowWhenRoomTemp(CancellationToken token)
        {
            double tol = 0.5;
            int stableSec = 30;
            int pollIntervalMs = 2000;
            int timeoutSec = 600;

            DateTime start = DateTime.UtcNow;
            int stableCount = 0;

            AddLog("停止流程：等待热流仪出口温度回到常温以停止吹气...");

            if (_thermalFlowCts != null)
            {
                while ((DateTime.UtcNow - start).TotalSeconds < timeoutSec)
                {
                    token.ThrowIfCancellationRequested();
                    double t = ThermalFlowOutletTemp;
                    if (Math.Abs(t - TempRoomSetting) <= tol)
                    {
                        stableCount += pollIntervalMs / 1000;
                        if (stableCount >= stableSec)
                        {
                            AddLog($"热流仪出口温度已稳定在常温 {t:F2}℃，准备停止吹气");
                            break;
                        }
                    }
                    else
                    {
                        stableCount = 0;
                    }
                    await Task.Delay(pollIntervalMs, token);
                }
            }
            else
            {
                await MonitorAndStopThermalFlowDirectlyAsync(token, tol, stableSec, pollIntervalMs, timeoutSec);
            }

            await SendThermalFlowStopCommandAsync(token);
            StopThermalFlowMonitor();
        }

        private async Task MonitorAndStopThermalFlowDirectlyAsync(CancellationToken token, double tol, int stableSec, int pollIntervalMs, int timeoutSec)
        {
            int addr = GetInstrumentAddress("ATS-545");
            if (addr == -1) addr = GetInstrumentAddress("THERM");
            if (addr == -1)
            {
                AddLog("未找到热流仪，无法自动停止吹气");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(addr))
            {
                AddLog("连接热流仪失败，无法自动停止吹气");
                return;
            }

            DateTime start = DateTime.UtcNow;
            int stableCount = 0;

            while ((DateTime.UtcNow - start).TotalSeconds < timeoutSec)
            {
                token.ThrowIfCancellationRequested();
                string resp = await QueryThermalFlowTempAsync(gpib);
                if (!string.IsNullOrWhiteSpace(resp) && double.TryParse(resp, out double t))
                {
                    if (Math.Abs(t - TempRoomSetting) <= tol)
                    {
                        stableCount += pollIntervalMs / 1000;
                        if (stableCount >= stableSec)
                        {
                            AddLog($"热流仪温度已稳定在常温 {t:F2}℃，准备停止吹气");
                            break;
                        }
                    }
                    else
                    {
                        stableCount = 0;
                    }
                }
                await Task.Delay(pollIntervalMs, token);
            }

            gpib.Disconnect();
        }

        private async Task SendThermalFlowStopCommandAsync(CancellationToken token)
        {
            int stopAddr = GetInstrumentAddress("FLOW");
            if (stopAddr == -1) stopAddr = GetInstrumentAddress("THERM");
            if (stopAddr == -1) stopAddr = GetInstrumentAddress("ATS-545");
            if (stopAddr == -1)
            {
                AddLog("停止吹气：未找到热流仪地址，操作中止");
                return;
            }

            using var gpib = new GpibCommunicator();
            if (!gpib.Connect(stopAddr))
            {
                AddLog("停止吹气：连接热流仪失败");
                return;
            }

            string[] stopCmds = new[] { "FLOW 0", "FLOW:STATE OFF", "STOP", "OFF" };
            foreach (var cmd in stopCmds)
            {
                gpib.Write(cmd);
                await Task.Delay(200, token);
                AddLog($"向热流仪发送停止指令: {cmd}");
                break; // 发送第一个成功即退出
            }

            gpib.Disconnect();
            AddLog("已向热流仪发送停止吹气指令");
        }

        #endregion

        #region ====================== 仪器数据采集 ======================

        private async Task RunEyeTestsAsync(TemperatureTestResult result, CancellationToken token)
        {
            UpdateTestStatus("采集86100D Tx/Rx眼图数据...", Colors.Orange);
            var txEye = new EyeTxResult();
            var rxEye = new EyeRxResult();

            var (txStr, rxStr) = await Get86100DEyeDiagramDataAsync(txEye, rxEye, token);

            result.TxEye = txEye;   // 结构化数据入库
            result.RxEye = rxEye;
            result.TxEyeDiagramData = txStr;  // 字符串留日志
            result.RxEyeDiagramData = rxStr;

            AddLog($"眼图完成: {txStr}");
            AddLog($"眼图完成: {rxStr}");
        }

        private async Task RunSpectrumTestsAsync(TemperatureTestResult result, CancellationToken token)
        {
            UpdateTestStatus("采集MS9740A光谱数据...", Colors.Orange);
            result.SpectrumData = await GetMS9740ASpectrumDataAsync(token);
            AddLog($"光谱数据采集完成: {result.SpectrumData}");
        }

        private async Task<(string txData, string rxData)> Get86100DEyeDiagramDataAsync(
    EyeTxResult txEye, EyeRxResult rxEye, CancellationToken token)
        {
            return await Task.Run(async () =>
            {
                int addr = GetInstrumentAddress("86100D");
                if (addr == -1) return ("未找到86100D", "未找到86100D");

                using var gpib = new GpibCommunicator();
                if (!gpib.Connect(addr)) return ("连接失败", "连接失败");

                try
                {
                    gpib.Write("*CLS");
                    gpib.Write(":SYSTEM:MODE EYE");
                    await Task.Delay(300, token);

                    double dataRateHz = ParseSelectedRateToHz();
                    if (dataRateHz > 0)
                    {
                        gpib.Write($":CRECOVERY1:CRATE {dataRateHz:E}");
                        gpib.Write($":TRIGger:BRATe {dataRateHz:E}");
                    }

                    int txCh = ResolveScopeChannel("Tx");
                    int rxCh = ResolveScopeChannel("Rx");

                    // 模板只加载一次
                    string maskFile = GetMaskFileNameForCurrentRate();
                    bool hasMask = !string.IsNullOrEmpty(maskFile);
                    if (hasMask)
                    {
                        gpib.Write($":MTESt:LOAD \"{maskFile}\"");
                        await Task.Delay(300, token);
                    }

                    string txResult = await Measure86100DChannelAsync(gpib, "Tx", txCh, hasMask, txEye, rxEye, token);
                    string rxResult = await Measure86100DChannelAsync(gpib, "Rx", rxCh, hasMask, txEye, rxEye, token);

                    if (hasMask) gpib.Write(":MTESt:STOP");

                    return (txResult, rxResult);
                }
                catch (Exception ex)
                {
                    return ($"Tx异常:{ex.Message}", $"Rx异常:{ex.Message}");
                }
                finally
                {
                    gpib.Disconnect();
                }
            }, token);
        }

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

            var testPoints = new[]
            {
                (atten: 0.0, label: "0dB", prop: "RxPower_0dB"),
                (atten: 3.5, label: "3.5dB", prop: "RxPower_3_5dB"),
                (atten: 7.0, label: "7dB", prop: "RxPower_7dB")
            };

            foreach (var point in testPoints)
            {
                UpdateTestStatus($"EXFO测试：设置 {point.label} 衰减...", Colors.Orange);
                if (gpib.SetEXFOAttenuation(point.atten))
                {
                    await Task.Delay(1000, token);
                    double modulePower = CurrentRxPower;
                    double exfoPower = gpib.ReadEXFOPower();

                    // 使用反射或预定义方法设置结果（简化示例）
                    if (point.atten == 0) { result.RxPower_0dB_Module = modulePower; result.RxPower_0dB_EXFO = exfoPower; }
                    else if (point.atten == 3.5) { result.RxPower_3_5dB_Module = modulePower; result.RxPower_3_5dB_EXFO = exfoPower; }
                    else { result.RxPower_7dB_Module = modulePower; result.RxPower_7dB_EXFO = exfoPower; }

                    AddLog($"EXFO {point.label}：模块={modulePower:F2} dBm，设备={exfoPower:F2} dBm");
                }
            }

            gpib.SetEXFOAttenuation(0);
            UpdateTestStatus("EXFO衰减测试完成", Colors.Orange);
        }

        private async Task RunMP1900ABerTestAsync(TemperatureTestResult result, CancellationToken token)
        {
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

            mp1900Gpib.ResetMP1900ABer();
            mp1900Gpib.StartMP1900ATest();
            await Task.Delay(1000, token);

            // 步骤1：衰减到误码率=5E-5
            UpdateTestStatus("误码仪测试：衰减到误码率5E-5...", Colors.Orange);
            double currentAtten = await FindBerThresholdAsync(mp1900Gpib, exfoGpib, 5e-5, token, 30, 2.0, 0.5);
            if (currentAtten < 30)
            {
                result.Ber_5E5_EXFO_Power = exfoGpib.ReadEXFOPower();
                AddLog($"误码率5E-5：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_5E5_EXFO_Power:F2}dBm");
            }
            else
            {
                AddLog("未达到误码率5E-5，后续步骤跳过");
                exfoGpib.SetEXFOAttenuation(0);
                return;
            }

            // 步骤2：继续衰减直到误码率消失
            UpdateTestStatus("误码仪测试：衰减到误码率消失...", Colors.Orange);
            currentAtten = await FindBerThresholdAsync(mp1900Gpib, exfoGpib, 1e-12, token, 35, 0.5, 0.5, descending: true, consecutive: 3);
            if (currentAtten < 35)
            {
                result.Ber_Disappear_EXFO_Power = exfoGpib.ReadEXFOPower();
                AddLog($"误码率消失：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_Disappear_EXFO_Power:F2}dBm");
            }

            // 步骤3：往回衰减直到误码率重现
            UpdateTestStatus("误码仪测试：往回衰减到误码率重现...", Colors.Orange);
            currentAtten = await FindBerThresholdAsync(mp1900Gpib, exfoGpib, 1e-10, token, 0, -0.2, -0.2, descending: false, consecutive: 2);
            if (currentAtten > 0)
            {
                result.Ber_Reappear_EXFO_Power = exfoGpib.ReadEXFOPower();
                AddLog($"误码率重现：衰减={currentAtten:F1}dB，EXFO功率={result.Ber_Reappear_EXFO_Power:F2}dBm");
            }

            exfoGpib.SetEXFOAttenuation(0);
            UpdateTestStatus("误码仪测试完成", Colors.Orange);
        }

        /// <summary>
        /// 通用误码率阈值查找（支持递增/递减）
        /// </summary>
        private async Task<double> FindBerThresholdAsync(GpibCommunicator bert, GpibCommunicator exfo,
            double threshold, CancellationToken token, double limit, double coarseStep, double fineStep,
            bool descending = true, int consecutive = 1)
        {
            double currentAtten = exfo.GetEXFOAttenuation(); // 假设有读取方法，否则从0开始
            if (currentAtten < 0) currentAtten = 0;

            int meetCount = 0;
            double step = coarseStep;
            int sign = descending ? 1 : -1;

            while (descending ? currentAtten < limit : currentAtten > limit)
            {
                token.ThrowIfCancellationRequested();
                double ber = bert.ReadMP1900ABer();
                bool condition = descending
                    ? (ber >= threshold || double.IsNaN(ber))
                    : (!double.IsNaN(ber) && ber >= threshold);

                if (condition)
                {
                    if (++meetCount >= consecutive) break;
                }
                else
                {
                    meetCount = 0;
                }

                // 动态步长
                double diff = descending ? (threshold - ber) : (ber - threshold);
                step = Math.Abs(diff) > threshold * 10 ? coarseStep : fineStep;
                currentAtten += sign * step;
                exfo.SetEXFOAttenuation(currentAtten);
                await Task.Delay(800, token);
            }

            return currentAtten;
        }

        #endregion

        #region ====================== DDM数据解析（批量读取优化）======================

        [RelayCommand]
        public void RefreshAllDdm()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // ✅ 批量读取：仅 2 次 I²C 事务
                    byte[] a0 = await Task.Run(() => _iicCom.ReadPage("A0", 256));
                    byte[] a2 = await Task.Run(() => _iicCom.ReadPage("A2", 256));

                    var realTime = DdmParser.ParseDdmExt(a2);
                    var thresholds = DdmParser.ParseAlarmThresholds(a2, true);
                    var moduleInfo = DdmParser.ParseModuleInfo(a0);
                    var alarm = ParseAlarmStatusBytes(a2[92], a2[110]);

                    await DispatcherInvokeAsync(() =>
                    {
                        // 实时值
                        RealTime.Temperature = realTime.Temperature;
                        RealTime.Voltage = realTime.Voltage;
                        RealTime.BiasCurrent = realTime.BiasCurrent;
                        RealTime.TxPower = realTime.TxPower;
                        RealTime.RxPower = realTime.RxPower;

                        CurrentTemperature = realTime.Temperature;
                        CurrentRxPower = realTime.RxPower;

                        // 阈值
                        Thresholds.TempHighAlarm = thresholds.TempThresholds[0];
                        Thresholds.TempHighWarning = thresholds.TempThresholds[1];
                        Thresholds.TempLowWarning = thresholds.TempThresholds[2];
                        Thresholds.TempLowAlarm = thresholds.TempThresholds[3];

                        Thresholds.VccHighAlarm = thresholds.VoltageThresholds[0];
                        Thresholds.VccHighWarning = thresholds.VoltageThresholds[1];
                        Thresholds.VccLowWarning = thresholds.VoltageThresholds[2];
                        Thresholds.VccLowAlarm = thresholds.VoltageThresholds[3];

                        Thresholds.BiasHighAlarm = thresholds.BiasThresholds[0];
                        Thresholds.BiasHighWarning = thresholds.BiasThresholds[1];
                        Thresholds.BiasLowWarning = thresholds.BiasThresholds[2];
                        Thresholds.BiasLowAlarm = thresholds.BiasThresholds[3];

                        Thresholds.TxPowerHighAlarm = thresholds.TxPowerThresholds[0];
                        Thresholds.TxPowerHighWarning = thresholds.TxPowerThresholds[1];
                        Thresholds.TxPowerLowWarning = thresholds.TxPowerThresholds[2];
                        Thresholds.TxPowerLowAlarm = thresholds.TxPowerThresholds[3];

                        Thresholds.RxPowerHighAlarm = thresholds.RxPowerThresholds[0];
                        Thresholds.RxPowerHighWarning = thresholds.RxPowerThresholds[1];
                        Thresholds.RxPowerLowWarning = thresholds.RxPowerThresholds[2];
                        Thresholds.RxPowerLowAlarm = thresholds.RxPowerThresholds[3];

                        // 模块信息
                        ModuleInfo.Manufacturer = moduleInfo.Manufacturer;
                        ModuleInfo.Model = moduleInfo.Model;
                        ModuleInfo.SerialNumber = moduleInfo.SerialNumber;
                        ModuleInfo.DateCode = moduleInfo.DateCode;

                        // 告警状态
                        Alarm.IsInternalMode = alarm.IsInternalMode;
                        Alarm.TxFault = alarm.TxFault;
                        Alarm.RxLos = alarm.RxLos;

                        UpdateAlarmStates();
                        Alarm.RunStatus = "✅ 数据刷新正常";
                    });
                }
                catch (Exception ex)
                {
                    await DispatcherInvokeAsync(() =>
                        Alarm.RunStatus = $"❌ 刷新异常: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 纯静态方法解析告警状态，无需 I²C 实例
        /// </summary>
        private static AlarmStatus ParseAlarmStatusBytes(byte flagByte, byte alarmByte)
        {
            return new AlarmStatus
            {
                IsInternalMode = (flagByte & 32) == 0,
                TxFault = (alarmByte & 2) != 0,
                RxLos = (alarmByte & 4) != 0
            };
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

            try
            {
                byte value = IsHighPowerMode ? (byte)1 : (byte)0;
                bool ok = _iicCom.WriteByte("A0", 0x00, value);
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
            try
            {
                bool ok = _iicCom.WriteByte("A0", 0x01, 0x01);
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

        #endregion

        #region ====================== 辅助方法 ======================

        /// <summary>
        /// 线程安全更新测试状态（文字+颜色）
        /// </summary>
        private void UpdateTestStatus(string statusText, Color statusColor)
        {
            DispatcherInvokeAsync(() =>
            {
                TestStatus = statusText;
                TestStatusColor = new SolidColorBrush(statusColor);
            });
        }

        /// <summary>
        /// 防抖日志输出（批量缓冲，减少 Dispatcher 调用）
        /// </summary>
        public void AddLog(string message)
        {
            lock (_logLock)
            {
                _logBuffer.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }

            // 立即触发刷新（带防抖）
            _ = FlushLogsAsync();
        }

        private async Task FlushLogsAsync()
        {
            await Task.Delay(_logFlushInterval);

            string text;
            lock (_logLock)
            {
                if (_logBuffer.Length == 0) return;
                // 防抖：如果距离上次刷新太近，跳过
                if (DateTime.Now - _lastLogFlush < _logFlushInterval) return;

                text = _logBuffer.ToString();
                _logBuffer.Clear();
                _lastLogFlush = DateTime.Now;
            }

            await DispatcherInvokeAsync(() => Log += text);
        }

        /// <summary>
        /// 封装 Dispatcher.InvokeAsync，统一异常处理
        /// </summary>
        private async Task DispatcherInvokeAsync(Action action)
        {
            if (App.Current?.Dispatcher != null)
            {
                await App.Current.Dispatcher.InvokeAsync(action);
            }
            else
            {
                // 设计时或无 Dispatcher 环境，直接执行
                action();
            }
        }

        /// <summary>
        /// 获取指定类型的仪器GPIB地址
        /// </summary>
        private int GetInstrumentAddress(string modelName)
        {
            return Instruments.FirstOrDefault(inst => inst.Model?.Contains(modelName) == true)?.GpibAddress ?? -1;
        }

        /// <summary>
        /// 将UI选择的速率字符串解析为 Hz
        /// </summary>
        private double ParseSelectedRateToHz()
        {
            string rateStr = _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
            if (string.IsNullOrWhiteSpace(rateStr)) return 0;

            if (rateStr.ToUpper().EndsWith("G") && double.TryParse(rateStr.TrimEnd('G', 'g'), out double rateG))
            {
                return rateG switch
                {
                    1.25 => 1.25e9,
                    10 => 10.3125e9,
                    25 => 25.78125e9,
                    100 => 25.78125e9,
                    400 => 26.5625e9,
                    _ => rateG * 1e9
                };
            }
            return 0;
        }

        /// <summary>
        /// 根据当前速率和调制方式返回对应的眼图模板文件名
        /// </summary>
        private string GetMaskFileNameForCurrentRate()
        {
            string rateStr = _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;
            string modStr = _selectedModulation?.ToUpper() ?? "NRZ";

            if (string.IsNullOrWhiteSpace(rateStr)) return null;

            return rateStr switch
            {
                "10G" when modStr == "NRZ" => "10GBASE-SR.msk",
                "25G" when modStr == "NRZ" => "100GBASE_SR4_TX_Optical.msk",
                "100G" when modStr == "NRZ" => "100G-SR10_10.3125.msk",
                "100G" when modStr == "PAM4" => "100G-CR4_PAM4.msk",
                "400G" when modStr == "PAM4" => "400G-SR16_PAM4.msk",
                _ => null
            };
        }

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

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _testCts?.Cancel();
                _testCts?.Dispose();
                _thermalFlowCts?.Cancel();
                _thermalFlowCts?.Dispose();
            }
            catch { }
        }

        // ========== 辅助方法 ==========

        /// <summary>
        /// 根据当前测试速率，自动解析出实际 SCPI 通道号（1~4）
        /// </summary>
        private int ResolveScopeChannel(string txOrRx)
        {
            // 1. 确定当前速率场景
            string rate = _isElectricPort ? _selectedElectricRate : _selectedOpticalRate;

            // 2. 确定模块起始通道
            int baseCh = rate switch
            {
                "10G" => ScopeConfig.Module10GBaseChannel,   // 1
                "25G" => ScopeConfig.Module25GBaseChannel,   // 3
                "100G" => ScopeConfig.Module25GBaseChannel,  // 假设100G也用86105D
                "400G" => ScopeConfig.Module25GBaseChannel,  // 假设400G也用86105D
                _ => 1  // 默认 fallback
            };

            // 3. 加上 Tx/Rx 偏移
            int offset = txOrRx.ToUpperInvariant() switch
            {
                "TX" => ScopeConfig.TxOffset,   // +0 → 1A/3A
                "RX" => ScopeConfig.RxOffset,   // +1 → 2A/4A
                _ => 0
            };

            return baseCh + offset;  // 10G Tx=1, 10G Rx=2, 25G Tx=3, 25G Rx=4
        }

        private async Task<bool> WaitOpcAsync(GpibCommunicator gpib, int timeoutMs = 10000)
        {
            // Ensure an OPC event is queued so the instrument will set the flag when
            // the previously sent operations complete. Some instruments require an
            // explicit *OPC before polling *OPC? to get a reliable completion signal.
            try { gpib.Write("*OPC"); } catch { }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    string resp = gpib.Query("*OPC?")?.Trim();
                    if (resp == "1") return true;
                }
                catch { }
                await Task.Delay(500);
            }
            return false;
        }

        private async Task Test86100DChannelAsync(GpibCommunicator gpib, InstrumentInfo inst,
    string channelName, int channelNum, bool hasMask, EyeTxResult txEye = null, EyeRxResult rxEye = null)
        {
            AddLog($"{inst.Name} 测试 {channelName} (CH{channelNum})...");

            // ========== 1. 关闭所有通道，再只开当前（避免之前通道干扰）==========
            for (int i = 1; i <= 4; i++)
                gpib.Write($":CHANnel{i}:DISPLAY OFF");

            gpib.Write($":CHANnel{channelNum}:DISPLAY ON");
            gpib.Write($":CHANnel{channelNum}:INPut DC");
            await Task.Delay(1000);

            // ========== 2. AUTOSCALE ==========
            gpib.Write(":CDisplay");           // ← 关键：清空 Color Grade 数据库
            gpib.Write(":AUToscale");
            if (!await WaitOpcAsync(gpib, 30000))
                AddLog($"{inst.Name} {channelName} AUTOSCALE 超时，继续...");
            await Task.Delay(5000);

            // ========== 3. 设置测量源 ==========
            // ========== 1. 统一设置测量源（关键修复）==========
            gpib.Write($":MEASure:SOURce CHANnel{channelNum}");
            // ========== 2. 清空并重新添加测量（关键修复）==========
            gpib.Write(":MEASure:EYE:SELection:CLEar");

            // 显式开启需要的测量项（86100D 要求）
            gpib.Write(":MEASure:EYE:JITTer:PP:STATe ON");
            gpib.Write(":MEASure:EYE:JITTer:RMS:STATe ON");
            gpib.Write(":MEASure:EYE:RTIMe:STATe ON");
            gpib.Write(":MEASure:EYE:FTIMe:STATe ON");
            gpib.Write(":MEASure:EYE:ERATio:STATe ON");
            gpib.Write(":MEASure:EYE:CROSSing:STATe ON");
            gpib.Write(":MEASure:EYE:APOWer:STATe ON");

            // ========== 3. 如果是 Tx，额外开启 Color Grade 相关测量 ==========
            if (channelName.ToUpperInvariant() == "TX")
            {
                gpib.Write(":MEASure:CGRade:MASK:MARGin:STATe ON");
                gpib.Write(":MEASure:CGRade:JITTer:PP:STATe ON");
                gpib.Write(":MEASure:CGRade:JITTer:RMS:STATe ON");
                gpib.Write(":MEASure:CGRade:RTIMe:STATe ON");
                gpib.Write(":MEASure:CGRade:FTIMe:STATe ON");
                gpib.Write(":MEASure:CGRade:APOWer:STATe ON");
                gpib.Write(":MEASure:CGRade:ERATio:STATe ON");
                gpib.Write(":MEASure:CGRade:CROSSing:STATe ON");
            }
            else // Rx
            {
                gpib.Write(":MEASure:CGRade:WIDTh:STATe ON");
                gpib.Write(":MEASure:CGRade:HEIGHt:STATe ON");
                gpib.Write(":MEASure:CGRade:RTIMe:STATe ON");
                gpib.Write(":MEASure:CGRade:FTIMe:STATe ON");
            }

            await Task.Delay(1000);

            // ========== 4. 模板源切换（不重新LOAD文件）==========
            if (hasMask)
            {
                gpib.Write($":MTESt:SOURce CHANnel{channelNum}");
                gpib.Write(":MTESt:STARt");
                await Task.Delay(1000);
            }

            // ========== 5. RUN（开始连续采集，积累数据）==========
            //gpib.Write(":ACQuire:RUNTil WAVeforms,500");  // ← 关键：让 RUN 自动停
            gpib.Write(":RUN");
            await Task.Delay(35000);  // 给1.5秒让眼图和模板数据稳定

            // ========== 6. 按 Tx/Rx 分别查不同的测量项 ==========

            if (channelName.ToUpperInvariant() == "TX" && txEye != null)
            {
                // 使用安全赋值，避免 txEye 为 null 导致 NRE
                txEye.Margin = QueryEyeMeasure(gpib, ":MEASure:CGRade:MASK:MARGin?");
                txEye.Crossing = QueryEyeMeasure(gpib, ":MEASure:CGRade:CROSSing?");
                txEye.RiseTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:RTIMe?");
                txEye.FallTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:FTIMe?");
                txEye.AveragePower = QueryEyeMeasure(gpib, ":MEASure:CGRade:APOWer?");
                txEye.JitterPP = QueryEyeMeasure(gpib, ":MEASure:CGRade:JITTer:PP?");
                txEye.JitterRMS = QueryEyeMeasure(gpib, ":MEASure:CGRade:JITTer:RMS?");
                txEye.ExtRatio = QueryEyeMeasure(gpib, ":MEASure:CGRade:ERATio?");

                // 安全格式化输出
                Func<double?, string> fmt = d => d.HasValue ? d.Value.ToString("F2") : "N/A";
                var marginStr = txEye?.Margin is not null ? fmt(txEye.Margin) : "N/A";
                var crossingStr = txEye?.Crossing is not null ? fmt(txEye.Crossing) : "N/A";
                var riseStr = txEye?.RiseTime is not null ? fmt(txEye.RiseTime) : "N/A";
                var fallStr = txEye?.FallTime is not null ? fmt(txEye.FallTime) : "N/A";
                var powerStr = txEye?.AveragePower is not null ? fmt(txEye.AveragePower) : "N/A";
                var jppStr = txEye?.JitterPP is not null ? fmt(txEye.JitterPP) : "N/A";
                var jrmsStr = txEye?.JitterRMS is not null ? fmt(txEye.JitterRMS) : "N/A";
                var erStr = txEye?.ExtRatio is not null ? fmt(txEye.ExtRatio) : "N/A";

                AddLog($"{inst.Name} Tx(CH{channelNum}) -> " +
                       $"Margin:{marginStr}%, Crossing:{crossingStr}%, " +
                       $"Tr:{riseStr}ps, Tf:{fallStr}ps, " +
                       $"Power:{powerStr}dBm, JitterPP:{jppStr}ps, " +
                       $"JitterRMS:{jrmsStr}ps, ER:{erStr}dB");

                if (hasMask)
                {
                    string passFail = gpib.Query(":MTESt:TEST:RESult?")?.Trim() ?? "N/A";
                    string hits = gpib.Query(":MTESt:COUNT:HITS?")?.Trim() ?? "N/A";
                    AddLog($"{inst.Name} Tx 模板 -> 结果:{passFail}, 违规:{hits}");
                }
            }
            else if (channelName.ToUpperInvariant() == "RX")
            {
                if (rxEye != null)
                {
                    rxEye.EyeWidth = QueryEyeMeasure(gpib, ":MEASure:CGRade:WIDTh?");
                    rxEye.EyeHeight = QueryEyeMeasure(gpib, ":MEASure:CGRade:HEIGHt?");
                    rxEye.RiseTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:RTIMe?");
                    rxEye.FallTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:FTIMe?");
                }

                Func<double?, string> fmt2 = d => d.HasValue ? d.Value.ToString("F2") : "N/A";
                var ew = rxEye?.EyeWidth is not null ? fmt2(rxEye.EyeWidth) : "N/A";
                var eh = rxEye?.EyeHeight is not null ? rxEye.EyeHeight.Value.ToString("F3") : "N/A";
                var tr = rxEye?.RiseTime is not null ? fmt2(rxEye.RiseTime) : "N/A";
                var tf = rxEye?.FallTime is not null ? fmt2(rxEye.FallTime) : "N/A";

                AddLog($"{inst.Name} Rx(CH{channelNum}) -> " +
                       $"EyeWidth:{ew}ps, EyeHeight:{eh}, " +
                       $"Tr:{tr}ps, Tf:{tf}ps");
            }
        }

        private async Task<string> Measure86100DChannelAsync(
    GpibCommunicator gpib, string channelName, int channelNum, bool hasMask,
    EyeTxResult txEye, EyeRxResult rxEye, CancellationToken token)
        {
            // 1. 关闭所有，只开当前
            for (int i = 1; i <= 4; i++)
                gpib.Write($":CHANnel{i}:DISPLAY OFF");

            gpib.Write($":CHANnel{channelNum}:DISPLAY ON");
            gpib.Write($":CHANnel{channelNum}:INPut DC");
            await Task.Delay(1000, token);
            token.ThrowIfCancellationRequested();

            // 2. AUTOSCALE
            gpib.Write(":AUToscale");
            if (!await WaitOpcAsync(gpib, 30000))
                return $"{channelName}(CH{channelNum}) AUTOSCALE超时";
            await Task.Delay(1000, token);

            // 3. 测量源
            gpib.Write($":MEASure:SOURce CHANnel{channelNum}");

            // 4. 模板源切换（不重新LOAD）
            if (hasMask)
            {
                gpib.Write($":MTESt:SOURce CHANnel{channelNum}");
                gpib.Write(":MTESt:STARt");
                await Task.Delay(1000, token);
            }

            // 5. RUN
            gpib.Write(":RUN");
            // 设置目标样本数（比如 1000 个波形/UI）
            //gpib.Write(":ACQuire:RUNTil 1000");
            await Task.Delay(35000);

            // 6. 按 Tx/Rx 查不同项（空值安全）
            if (channelName.ToUpperInvariant() == "TX" && txEye != null)
            {
                txEye.Margin = QueryEyeMeasure(gpib, ":MEASure:CGRade:MASK:MARGin?");
                txEye.Crossing = QueryEyeMeasure(gpib, ":MEASure:CGRade:CROSSing?");
                txEye.FallTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:FTIMe?");
                txEye.RiseTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:RTIMe?");
                txEye.AveragePower = QueryEyeMeasure(gpib, ":MEASure:CGRade:APOWer?");
                txEye.JitterPP = QueryEyeMeasure(gpib, ":MEASure:CGRade:JITTer:PP?");
                txEye.JitterRMS = QueryEyeMeasure(gpib, ":MEASure:CGRade:JITTer:RMS?");
                txEye.ExtRatio = QueryEyeMeasure(gpib, ":MEASure:CGRade:ERATio?");

                Func<double?, string> fmt = d => d.HasValue ? d.Value.ToString("F2") : "N/A";
                var marginStr = txEye?.Margin is not null ? fmt(txEye.Margin) : "N/A";
                var crossingStr = txEye?.Crossing is not null ? fmt(txEye.Crossing) : "N/A";
                var riseStr = txEye?.RiseTime is not null ? fmt(txEye.RiseTime) : "N/A";
                var fallStr = txEye?.FallTime is not null ? fmt(txEye.FallTime) : "N/A";
                var powerStr = txEye?.AveragePower is not null ? fmt(txEye.AveragePower) : "N/A";
                var jppStr = txEye?.JitterPP is not null ? fmt(txEye.JitterPP) : "N/A";
                var jrmsStr = txEye?.JitterRMS is not null ? fmt(txEye.JitterRMS) : "N/A";
                var erStr = txEye?.ExtRatio is not null ? fmt(txEye.ExtRatio) : "N/A";

                AddLog($"{channelName} Tx(CH{channelNum}) -> " +
                       $"Margin:{marginStr}%, Crossing:{crossingStr}%, " +
                       $"Tr:{riseStr}ps, Tf:{fallStr}ps, " +
                       $"Power:{powerStr}dBm, JitterPP:{jppStr}ps, " +
                       $"JitterRMS:{jrmsStr}ps, ER:{erStr}dB");

                if (hasMask)
                {
                    string passFail = gpib.Query(":MTESt:TEST:RESult?")?.Trim() ?? "N/A";
                    string hits = gpib.Query(":MTESt:COUNT:HITS?")?.Trim() ?? "N/A";
                    AddLog($"{channelName} Tx 模板 -> 结果:{passFail}, 违规:{hits}");
                }
            }
            else if (channelName.ToUpperInvariant() == "RX")
            {
                if (rxEye != null)
                {
                    rxEye.EyeWidth = QueryEyeMeasure(gpib, ":MEASure:CGRade:WIDTh?");
                    rxEye.EyeHeight = QueryEyeMeasure(gpib, ":MEASure:CGRade:HEIGHt?");
                    rxEye.FallTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:FTIMe?");
                    rxEye.RiseTime = QueryEyeMeasure(gpib, ":MEASure:CGRade:RTIMe?");
                }

                Func<double?, string> fmt2 = d => d.HasValue ? d.Value.ToString("F2") : "N/A";
                var ew = rxEye?.EyeWidth is not null ? fmt2(rxEye.EyeWidth) : "N/A";
                var eh = rxEye?.EyeHeight is not null ? rxEye.EyeHeight.Value.ToString("F3") : "N/A";
                var tr = rxEye?.RiseTime is not null ? fmt2(rxEye.RiseTime) : "N/A";
                var tf = rxEye?.FallTime is not null ? fmt2(rxEye.FallTime) : "N/A";

                AddLog($"{channelName} Rx(CH{channelNum}) -> " +
                       $"EyeWidth:{ew}ps, EyeHeight:{eh}, " +
                       $"Tr:{tr}ps, Tf:{tf}ps");
            }

            return $"{channelName}(CH{channelNum}) 未知通道类型";
        }

        /// <summary>
        /// 86100D 测量查询：先发送设置命令激活，再查询数值
        /// </summary>
        /// <summary>
        /// 直接 Query 86100D Eye/Mask 测量值（不要先 Write 激活）
        /// </summary>
        private double? QueryEyeMeasure(GpibCommunicator gpib, string cmdWithQuestionMark, int delayMs = 300)
        {
            try
            {
                // 优化：先尝试直接 Query，如果返回无效，再发送激活（Write 不带问号）后重查一次。
                Thread.Sleep(delayMs);
                string? resp = gpib.Query(cmdWithQuestionMark)?.Trim();

                // 9.91E+37 / 9.99999E+37 表示无效数据或溢出
                bool invalid = string.IsNullOrWhiteSpace(resp) ||
                               resp.Contains("9.91E+37") ||
                               resp.Contains("9.99999E+37");

                if (!invalid && double.TryParse(resp, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }

                // 如果第一次 Query 失败或返回无效值，尝试先发一个激活写命令（去掉末尾的问号），再 Query 一次
                try
                {
                    var writeCmd = cmdWithQuestionMark.TrimEnd();
                    if (writeCmd.EndsWith("?")) writeCmd = writeCmd.Substring(0, writeCmd.Length - 1);
                    gpib.Write(writeCmd);
                    Thread.Sleep(Math.Max(delayMs, 500));
                    resp = gpib.Query(cmdWithQuestionMark)?.Trim();

                    if (string.IsNullOrWhiteSpace(resp) ||
                        resp.Contains("9.91E+37") ||
                        resp.Contains("9.99999E+37"))
                        return null;

                    if (double.TryParse(resp, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out val))
                        return val;
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"[QueryEye] retry {cmdWithQuestionMark} 异常: {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[QueryEye] {cmdWithQuestionMark} 异常: {ex.Message}");
            }
            return null;
        }
        #endregion
    }
}