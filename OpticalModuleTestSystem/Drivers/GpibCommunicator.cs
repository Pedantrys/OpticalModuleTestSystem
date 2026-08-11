using Ivi.Visa;
using OpticalModuleTestSystem.Resources;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// GPIB通信器（增强错误处理 + 线程安全版）
    /// </summary>
    public class GpibCommunicator : IDisposable
    {
        // 串行化所有 IO 操作，避免并发写导致命令混淆
        private readonly object _ioLock = new();

        private IMessageBasedSession? _session;

        /// <summary>
        /// 当前是否已连接
        /// </summary>
        public bool IsConnected => _session != null;

        /// <summary>
        /// 外部日志注入（可选），用于将底层异常传递到 ViewModel 日志
        /// </summary>
        public Action<string>? Logger { get; set; }

        /// <summary>
        /// 最后发送的命令（调试用）
        /// </summary>
        public string LastCommand { get; private set; } = string.Empty;

        /// <summary>
        /// 最后收到的响应（调试用）
        /// </summary>
        public string LastResponse { get; private set; } = string.Empty;

        #region ====================== 连接与断开 ======================

        public void Dispose() => Disconnect();

        /// <summary>
        /// 连接到指定GPIB地址的设备
        /// </summary>
        public bool Connect(int gpibAddress, int board = 0)
        {
            try
            {
                Disconnect(); // 先清理旧连接
                string resource = $"GPIB{board}::{gpibAddress}::INSTR";
                _session = (IMessageBasedSession)GlobalResourceManager.Open(resource);
                _session.TimeoutMilliseconds = 5000;

                // 统一使用 FormattedIO，自动处理终止符和编码
                // NOTE: IMessageBasedFormattedIO 没有 SRMDelay 属性，保留占位以便未来扩展
                return true;
            }
            catch (Exception ex)
            {
                Log($"GPIB连接失败 [{gpibAddress}]: {ex.Message}");
                _session = null;
                return false;
            }
        }

        /// <summary>
        /// 断开GPIB连接并释放资源（线程安全）
        /// </summary>
        public void Disconnect()
        {
            lock (_ioLock)
            {
                try
                {
                    _session?.Dispose();
                }
                catch (Exception ex)
                {
                    Log($"Disconnect异常: {ex.Message}");
                }
                finally
                {
                    _session = null;
                }
            }
        }

        #endregion

        #region ====================== 基础 IO（统一 FormattedIO）======================

        /// <summary>
        /// 发送查询命令并返回响应
        /// </summary>
        /// <summary>
        /// 标准 Query：一问一答，适合单行返回（绝大多数 SCPI 命令）
        /// 发送前自动清空残留缓冲区
        /// </summary>
        public string Query(string command)
        {
            LastCommand = command;
            try
            {
                lock (_ioLock)
                {
                    if (_session == null) return string.Empty;

                    // ✅ 关键：发命令前清空输入缓冲区，防止读到上次的残留
                    try { _session.Clear(); } catch { }

                    _session.FormattedIO.WriteLine(command);

                    string response = _session.FormattedIO.ReadString().TrimEnd('\n', '\r');
                    LastResponse = response;
                    return response;
                }
            }
            catch (Exception ex)
            {
                Log($"Query失败 [{command}]: {ex.Message}");
                LastResponse = string.Empty;
                return string.Empty;
            }
        }

        /// <summary>
        /// 多行 Query：循环读取直到超时，适合 :MEASure:RESults? 这类多行返回
        /// </summary>
        public string QueryMultiLine(string command, int lineTimeoutMs = 200)
        {
            LastCommand = command;
            try
            {
                lock (_ioLock)
                {
                    if (_session == null) return string.Empty;

                    // 清空残留
                    try { _session.Clear(); } catch { }

                    var oldTimeout = _session.TimeoutMilliseconds;
                    try
                    {
                        _session.FormattedIO.WriteLine(command);

                        // 把单条读取超时设短，用来判断"是否还有下一行"
                        _session.TimeoutMilliseconds = lineTimeoutMs;

                        var sb = new StringBuilder();
                        while (true)
                        {
                            try
                            {
                                string line = _session.FormattedIO.ReadString().TrimEnd('\n', '\r');
                                if (sb.Length > 0) sb.Append('\n');
                                sb.Append(line);
                            }
                            catch (System.TimeoutException)
                            {
                                // 超时说明读完了
                                break;
                            }
                        }
                        LastResponse = sb.ToString();
                        return sb.ToString();
                    }
                    finally
                    {
                        _session.TimeoutMilliseconds = oldTimeout;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"QueryMultiLine失败 [{command}]: {ex.Message}");
                LastResponse = string.Empty;
                return string.Empty;
            }
        }

        /// <summary>
        /// 发送命令（无响应）
        /// </summary>
        public void Write(string command)
        {
            LastCommand = command;
            try
            {
                lock (_ioLock)
                {
                    _session?.FormattedIO.WriteLine(command);
                }
            }
            catch (Exception ex)
            {
                Log($"Write失败 [{command}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送原始字节（用于二进制块传输等特殊情况）
        /// </summary>
        public void WriteRaw(byte[] data)
        {
            try
            {
                lock (_ioLock)
                {
                    _session?.RawIO.Write(data);
                }
            }
            catch (Exception ex)
            {
                Log($"WriteRaw失败: {ex.Message}");
            }
        }

        #endregion

        #region ====================== 通用 SCPI 查询 ======================

        public string Identify()
        {
            return Query(ScpiCommands.IDN);
        }

        public bool ResetDevice()
        {
            Write(ScpiCommands.CLS);
            Write(ScpiCommands.RST);
            return true;
        }

        public bool ClearStatus()
        {
            Write(ScpiCommands.CLS);
            return true;
        }

        /// <summary>
        /// 读取状态字节（*STB?）
        /// </summary>
        public int GetStatusByte()
        {
            string s = Query("*STB?");
            if (int.TryParse(s, out int v)) return v;

            s = Query("STAT:QUES?");
            if (int.TryParse(s, out v)) return v;

            return -1;
        }

        /// <summary>
        /// 查询系统错误队列
        /// </summary>
        public string QuerySystemError()
        {
            var r = Query("SYST:ERR?");
            return string.IsNullOrWhiteSpace(r) ? "0,No error" : r.Trim();
        }

        /// <summary>
        /// 等待设备操作完成（*OPC?），超时恢复保证在 finally 中执行
        /// </summary>
        public bool WaitForOperationComplete(int timeoutMs = 5000)
        {
            if (_session == null) return false;

            int prevTimeout = _session.TimeoutMilliseconds;
            try
            {
                lock (_ioLock)
                {
                    _session.TimeoutMilliseconds = Math.Max(1000, timeoutMs);
                    _session.FormattedIO.WriteLine("*OPC?");
                    string r = _session.FormattedIO.ReadString().Trim();
                    return r == "1";
                }
            }
            catch (Exception ex)
            {
                Log($"WaitForOPC失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 确保超时值一定恢复，避免影响后续命令
                try { if (_session != null) _session.TimeoutMilliseconds = prevTimeout; }
                catch { }
            }
        }

        #endregion

        #region ====================== 温控平台 ATS-545 ======================

        /// <summary>
        /// 设置目标温度并验证（支持设定温度回读）
        /// </summary>
        public bool SetTemperature(double targetTemp)
        {
            if (_session == null) return false;

            try
            {
                // ATS-545 通道选择（经验规则）
                string setn = targetTemp switch
                {
                    > -70 and <= 10 => "SETN 2",
                    > 10 and <= 50 => "SETN 1",
                    > 50 and <= 150 => "SETN 0",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(setn))
                {
                    Log($"目标温度 {targetTemp} 超出 ATS-545 支持范围");
                    return false;
                }

                Write(setn);

                // 尝试多种设置指令
                string[] setTempCmds = new[]
                {
                    $"SETP {targetTemp:F1}",
                    $":SOUR:TEMP {targetTemp:F1}",
                    $"TEMP {targetTemp:F1}",
                    $"SETT {targetTemp:F1}"
                };

                foreach (var cmd in setTempCmds)
                {
                    Write(cmd);
                    Thread.Sleep(100);
                }

                // 启动气流
                Write("FLOW 1");

                // 回读验证：优先读取 SETP?（设定温度），如不支持则回退 TEMP?
                const int maxRetries = 5;
                const double tol = 0.6;
                for (int i = 0; i < maxRetries; i++)
                {
                    double set = GetSetTemperature();
                    if (!double.IsNaN(set) && Math.Abs(set - targetTemp) <= tol)
                        return true;

                    Thread.Sleep(300);
                }

                Log($"温控设定验证失败：目标 {targetTemp:F1}℃ 未在容差内确认");
                return false;
            }
            catch (Exception ex)
            {
                Log($"SetTemperature异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 读取设定温度（优先）或当前温度（回退）。失败返回 NaN。
        /// </summary>
        public double GetSetTemperature()
        {
            // 先尝试读取设定温度（Set Point）
            string[] setpointCmds = new[] { "SETP?", "SETPOINT?", "SET:TEMP?" };
            foreach (var cmd in setpointCmds)
            {
                string resp = Query(cmd);
                if (double.TryParse(resp, out double temp))
                    return temp;
            }

            // 回退：读取当前实际温度（注意：验证设定值时不应使用此值！）
            string[] tempCmds = new[] { "TEMP?", "T?", "MEAS:TEMP?" };
            foreach (var cmd in tempCmds)
            {
                string resp = Query(cmd);
                if (double.TryParse(resp, out double temp))
                    return temp;
            }

            return double.NaN;
        }

        public bool StopTemperatureControl()
        {
            if (_session == null) return false;
            try
            {
                Write("FLOW 0");
                Thread.Sleep(100);
                Write("ABOR"); // 比 STOP 更通用的 SCPI 停止命令
                return true;
            }
            catch { return false; }
        }

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
            catch { return false; }
        }

        #endregion

        #region ====================== EXFO IQS-610P ======================

        /// <summary>
        /// 读取当前衰减值（dB），失败返回 -1
        /// </summary>
        public double GetEXFOAttenuation()
        {
            string[] queryCmds = new[]
            {
                "ATT?",
                "INPUT:ATTENUATION?",
                "ATTENUATION?",
                "INP:ATT?"
            };

            foreach (var cmd in queryCmds)
            {
                string resp = Query(cmd);
                if (!string.IsNullOrWhiteSpace(resp))
                {
                    var numStr = new string(resp.Where(c =>
                        char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'E' || c == 'e').ToArray());

                    if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, null, out double val))
                        return val;
                }
            }

            return -1;
        }

        /// <summary>
        /// 设置衰减值（0~60 dB），支持回读验证
        /// </summary>
        public bool SetEXFOAttenuation(double attenuationDb)
        {
            try
            {
                attenuationDb = Math.Clamp(attenuationDb, 0.0, 60.0);

                string[] setCmds = new[]
                {
                    $"ATT {attenuationDb:F2}",
                    $"INPUT:ATTENUATION {attenuationDb:F2}",
                    $"ATTENUATION {attenuationDb:F2}",
                    $"INP:ATT {attenuationDb:F2}"
                };

                foreach (var cmd in setCmds)
                {
                    Write(cmd);
                    Thread.Sleep(50);
                }

                // 回读验证
                Thread.Sleep(200);
                double actual = GetEXFOAttenuation();
                return actual >= 0 && Math.Abs(actual - attenuationDb) < 0.1;
            }
            catch { return false; }
        }

        /// <summary>
        /// 读取光功率（dBm），失败返回 NaN
        /// </summary>
        public double ReadEXFOPower()
        {
            string[] queryCmds = new[]
            {
                "POWER?",
                "MEASure:POWer?",
                "READ:POWer?",
                "POW?"
            };

            foreach (var cmd in queryCmds)
            {
                string resp = Query(cmd);
                if (!string.IsNullOrWhiteSpace(resp))
                {
                    var numStr = new string(resp.Where(c =>
                        char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'E' || c == 'e').ToArray());

                    if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, null, out double val))
                        return val;
                }
            }

            return double.NaN;
        }

        public bool InitPowerMeter(double lineRate, double calFactor)
        {
            if (_session == null) return false;
            try
            {
                Write($":SENS:RATE {lineRate:F4}");
                Write($":SENS:CORR:GAIN {calFactor:F3}");
                Write(":SENS:POW:UNIT DBM");
                Write(":SENS:POW:RANG:AUTO ON");
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region ====================== Anritsu MP1900A 误码仪 ======================

        public bool ResetMP1900ABer()
        {
            if (_session == null) return false;
            try
            {
                Write(":STAT:RES");
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 读取误码率。MP1900A 无信号时可能返回 9.91E-001，调用方需自行判断。
        /// </summary>
        public double ReadMP1900ABer()
        {
            if (_session == null) return double.NaN;
            try
            {
                string berStr = Query(":STAT:BER?");
                if (double.TryParse(berStr, out double ber))
                    return ber;
                return double.NaN;
            }
            catch { return double.NaN; }
        }

        public bool StartMP1900ATest()
        {
            if (_session == null) return false;
            try
            {
                Write(":INIT:IMM");
                return true;
            }
            catch { return false; }
        }

        public bool InitBert(double lineRate)
        {
            if (_session == null) return false;
            try
            {
                Write($":SOUR:RATE {lineRate:F4}");
                Write(":SENS:THR:AUTO ONCE");
                Write(":STAT:RES");
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region ====================== Anritsu MS9740A 光谱仪 ======================

        public bool InitSpectrumAnalyzer(double lineRate, string templateName)
        {
            if (_session == null) return false;
            try
            {
                string rbw = lineRate switch
                {
                    >= 100 => "0.02NM",
                    >= 25 => "0.05NM",
                    >= 10 => "0.1NM",
                    _ => "0.1NM"
                };

                Write($":SENS:BAND:RES {rbw}");
                Write($":CALC:MARK:TEMP:LOAD \"{templateName}\"");
                Write(":CALC:MARK:TEMP:STAT ON");
                Write(":SENS:WAV:CENT 1550NM");
                Write(":DISP:WIND:TRAC:Y:RLEV -10DBM");
                Write(":SENS:WAV:SPAN 20NM");
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region ====================== 内部辅助 ======================

        private void Log(string message)
        {
            Logger?.Invoke(message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        #endregion
    }
}