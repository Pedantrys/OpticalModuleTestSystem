using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ivi.Visa;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// GPIB通信器（增强错误处理版）
    /// </summary>
    public class GpibCommunicator : IDisposable
    {
        public void Dispose() => Disconnect();
        private IMessageBasedSession? _session;

        public bool Connect(int gpibAddress, int board = 0)
        {
            try
            {
                string resource = $"GPIB{board}::{gpibAddress}::INSTR";
                _session = (IMessageBasedSession)GlobalResourceManager.Open(resource);
                _session.TimeoutMilliseconds = 5000;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GPIB连接失败 [{gpibAddress}]: {ex.Message}");
                return false;
            }
        }

        public string Query(string command)
        {
            try
            {
                _session?.RawIO.Write(command + "\n");
                return _session?.RawIO.ReadString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Query失败 [{command}]: {ex.Message}");
                return string.Empty;
            }
        }

        public void Write(string command)
        {
            try
            {
                _session?.FormattedIO.WriteLine(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Write失败 [{command}]: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                _session?.Dispose();
            }
            catch { }
            finally
            {
                _session = null;
            }
        }

        #region ====================== 温控平台 AST-545 ======================

        /// <summary>
        /// 温控平台（Temptronic AST-545）设置目标温度
        /// </summary>
        /// <param name="targetTemp">目标温度（℃）</param>
        /// <returns>是否设置成功</returns>
        public bool SetTemperature(int targetTemp)
        {
            if (_session == null) return false;
            try
            {
                // AST-545 温控平台标准SCPI指令：设置目标温度
                Write($"TEMP {targetTemp}");
                // 启动温控
                Write("TEMP:RUN");  
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取温控平台当前设定温度
        /// </summary>
        /// <returns>设定温度值</returns>
        public double GetSetTemperature()
        {
            return double.TryParse(Query("TEMP?"), out double temp) ? temp : 0;
        }

        /// <summary>
        /// 停止温控平台加热/制冷
        /// </summary>
        public void StopTemperatureControl()
        {
            Write("TEMP:STOP");
        }

        /// <summary>
        /// 温控平台初始化：设置25℃并启动输出
        /// </summary>
        public bool InitTempControllerTo25C()
        {
            if (_session == null) return false;
            try
            {
                Write("TEMP:MODE TARG");
                Write("TEMP 25");
                Write("TEMP:RUN");
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ====================== Keysight 86100D 示波器 ======================
        /// <summary>
        /// Keysight 86100D 切换测试通道（Tx/Rx）
        /// </summary>
        /// <param name="channel">通道类型："Tx" 或 "Rx"</param>
        /// <returns>是否切换成功</returns>
        public bool Switch86100DChannel(string channel)
        {
            if (_session == null) return false;
            try
            {
                // 86100D 通道切换指令（根据实际设备SCPI手册调整，示例为通用格式）
                // 假设：Tx对应通道1，Rx对应通道2
                string channelCmd = channel.ToUpper() switch
                {
                    "TX" => ":CHAN1:SEL",  // 选择发射端通道
                    "RX" => ":CHAN2:SEL",  // 选择接收端通道
                    _ => throw new ArgumentException("通道类型仅支持 Tx/Rx")
                };
                Write(channelCmd);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ====================== EXFO IQS-3150 光衰减/功率计 ======================
        /// <summary>
        /// EXFO IQS-3150 设置光衰减值
        /// </summary>
        /// <param name="attenuationDb">衰减值 (dB)，支持0.1dB步进</param>
        /// <returns>是否设置成功</returns>
        public bool SetEXFOAttenuation(double attenuationDb)
        {
            if (_session == null) return false;
            try
            {
                // IQS-3150 标准SCPI指令：设置衰减值
                Write($"ATT {attenuationDb} DB");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// EXFO IQS-3150 读取当前光功率值
        /// </summary>
        /// <returns>光功率值 (dBm)，读取失败返回 NaN</returns>
        public double ReadEXFOPower()
        {
            if (_session == null) return double.NaN;
            try
            {
                // IQS-3150 标准SCPI指令：读取光功率
                string powerStr = Query(":POW?");
                return double.TryParse(powerStr, out double power) ? power : double.NaN;
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// 光功率模块初始化：设置速率 + 应用校准系数（数据调正）
        /// </summary>
        /// <param name="lineRate">物理线速率（Gbps）</param>
        /// <param name="calFactor">校准补偿系数</param>
        public bool InitPowerMeter(double lineRate, double calFactor)
        {
            if (_session == null) return false;
            try
            {
                // 1. 设置速率档位
                Write($":SENS:RATE {lineRate:F4}");
                // 2. 应用校准系数（数据调正）
                Write($":SENS:CORR:GAIN {calFactor:F3}");
                // 3. 基础配置
                Write(":SENS:POW:UNIT DBM");
                Write(":SENS:POW:RANG:AUTO ON");
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ====================== Anritsu MP1900A 误码仪 ======================

        /// <summary>
        /// Anritsu MP1900A 误码仪清零
        /// </summary>
        /// <returns>是否清零成功</returns>
        public bool ResetMP1900ABer()
        {
            if (_session == null) return false;
            try
            {
                // MP1900A 标准SCPI指令：误码率清零
                Write(":STAT:RES");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Anritsu MP1900A 读取当前误码率
        /// </summary>
        /// <returns>误码率（如1.2E-5），读取失败返回 NaN</returns>
        public double ReadMP1900ABer()
        {
            if (_session == null) return double.NaN;
            try
            {
                // MP1900A 标准SCPI指令：读取误码率
                string berStr = Query(":STAT:BER?");
                return double.TryParse(berStr, out double ber) ? ber : double.NaN;
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Anritsu MP1900A 启动误码测试
        /// </summary>
        /// <returns>是否启动成功</returns>
        public bool StartMP1900ATest()
        {
            if (_session == null) return false;
            try
            {
                Write(":INIT:IMM");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 误码仪初始化：设置速率 + 自动校准灵敏度判决阈值
        /// </summary>
        public bool InitBert(double lineRate)
        {
            if (_session == null) return false;
            try
            {
                // 1. 设置工作速率
                Write($":SOUR:RATE {lineRate:F4}");
                // 2. 自动校准灵敏度（判决阈值）
                Write(":SENS:THR:AUTO ONCE");
                // 3. 复位计数器
                Write(":STAT:RES");
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ====================== Anritsu MS9740A 光谱分析仪 ======================
        /// <summary>
        /// 光谱仪初始化：速率匹配带宽 + 加载测试模板 + 基础参数配置
        /// </summary>
        /// <param name="lineRate">物理线速率（Gbps）</param>
        /// <param name="templateName">测试模板名称</param>
        public bool InitSpectrumAnalyzer(double lineRate, string templateName)
        {
            if (_session == null) return false;
            try
            {
                // 1. 根据速率自动匹配分辨率带宽
                string rbw = lineRate switch
                {
                    >= 100 => "0.02NM",
                    >= 25 => "0.05NM",
                    >= 10 => "0.1NM",
                    _ => "0.1NM"
                };
                Write($":SENS:BAND:RES {rbw}");

                // 2. 加载指定测试模板并开启模板检测
                Write($":CALC:MARK:TEMP:LOAD \"{templateName}\"");
                Write(":CALC:MARK:TEMP:STAT ON");

                // 3. 基础初始化：中心波长、参考电平、扫宽
                Write(":SENS:WAV:CENT 1550NM");
                Write(":DISP:WIND:TRAC:Y:RLEV -10DBM");
                Write(":SENS:WAV:SPAN 20NM");
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
