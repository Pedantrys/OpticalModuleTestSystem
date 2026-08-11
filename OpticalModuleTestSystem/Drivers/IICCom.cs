using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 
    /// </summary>
    public class IICCom
    {
        private SerialPort _com;
        private readonly byte[] _a0Buf = new byte[256]; // A0页专属缓冲区
        private readonly byte[] _a2Buf = new byte[256]; // A2页专属缓冲区
        public static byte[] Read_Data { get; private set; } = new byte[256];

        // 与 VB 完全一致的波特率
        private const int BaudRate = 57600;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// 自动扫描所有可用串口
        /// </summary>
        public bool SweepCom()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    if (TryOpenPort(port))
                    {
                        if (CheckRS232() == 1)
                            return true;
                        _com?.Close();
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试打开指定串口
        /// </summary>
        /// <param name="portName"></param>
        /// <returns></returns>
        private bool TryOpenPort(string portName)
        {
            try
            {
                _com = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    Encoding = Encoding.ASCII
                };
                _com.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 RS-232 连接
        /// </summary>
        private int CheckRS232()
        {
            byte[] cmd = { (byte)'#', 4, 1, (byte)'$' }; // 命令码 1：Check RS-232
            try
            {
                lock (_lock)
                {
                    _com.Write(cmd, 0, cmd.Length);

                    // 等待 4 字节响应（# OK $）
                    for (int i = 0; i < 100; i++)
                    {
                        Thread.Sleep(3);
                        if (_com.BytesToRead >= 4)
                            break;
                    }

                    if (_com.BytesToRead != 4)
                    {
                        _com.DiscardInBuffer();
                        return 0;
                    }

                    byte[] resp = new byte[4];
                    _com.Read(resp, 0, 4);

                    if (resp[0] == '#' && resp[3] == '$' && resp[1] == 79 && resp[2] == 75)
                        return 1;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 选择 IIC 速率
        /// </summary>
        public bool SelectIICRate(int rate)
        {
            byte[] cmd = { (byte)'#', 5, 17, (byte)rate, (byte)'$' };
            try
            {
                lock (_lock)
                {
                    _com.Write(cmd, 0, cmd.Length);
                    for (int i = 0; i < 200; i++)
                    {
                        Thread.Sleep(1);
                        if (_com.BytesToRead == 4)
                            break;
                    }

                    if (_com.BytesToRead < 4)
                    {
                        _com.DiscardInBuffer();
                        return false;
                    }

                    byte[] resp = new byte[4];
                    _com.Read(resp, 0, 4);
                    return resp[0] == '#' && resp[3] == '$' && resp[1] == 1;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 读取 IIC 页面数据（返回字节数组版本）
        /// </summary>
        /// <param name="page">页名 "A0" 或 "A2"</param>
        /// <param name="length">读取长度 1-256</param>
        /// <returns>读取到的字节数组（副本）</returns>
        /// <exception cref="ArgumentOutOfRangeException">长度非法</exception>
        /// <exception cref="ArgumentException">页名非法</exception>
        /// <exception cref="InvalidOperationException">串口未打开</exception>
        /// <exception cref="IOException">读取失败</exception>
        public byte[] ReadPage(string page, int length)
        {
            if (length <= 0 || length > 256)
                throw new ArgumentOutOfRangeException(nameof(length), "读取长度必须在 1-256 之间");

            string pageUpper = page?.ToUpperInvariant();
            if (pageUpper != "A0" && pageUpper != "A2")
                throw new ArgumentException("页名必须是 A0 或 A2", nameof(page));

            lock (_lock)
            {
                if (_com == null || !_com.IsOpen)
                    throw new InvalidOperationException("IIC 串口未打开或已断开");

                byte deviceAddr = pageUpper == "A0" ? (byte)0xA0 : (byte)0xA2;
                byte[] targetBuf = pageUpper == "A0" ? _a0Buf : _a2Buf;

                // 每 16 字节一页循环读取
                for (int pageStart = 0; pageStart < length; pageStart += 16)
                {
                    if (!ReadOnePage(deviceAddr, (byte)pageStart, out byte[] pageData))
                        throw new IOException($"IIC 读取失败：{page} 页偏移 0x{pageStart:X2} 无响应或校验错误");

                    int copyLen = Math.Min(16, length - pageStart);
                    Array.Copy(pageData, 0, targetBuf, pageStart, copyLen);
                }

                // 返回独立副本，防止外部修改内部缓冲区
                byte[] result = new byte[length];
                Array.Copy(targetBuf, 0, result, 0, length);

                // 兼容旧接口：同步写入静态 Read_Data
                if (Read_Data != null && Read_Data.Length >= length)
                    Array.Copy(targetBuf, 0, Read_Data, 0, length);

                return result;
            }
        }

        /// <summary>
        /// 读取一页（16 字节）
        /// </summary>
        private bool ReadOnePage(byte deviceAddr, byte pageStart, out byte[] pageData)
        {
            pageData = new byte[16];
            // 命令： # 6 8 设备地址 页地址 $
            byte[] cmd = { (byte)'#', 6, 8, deviceAddr, pageStart, (byte)'$' };

            try
            {
                _com.Write(cmd, 0, cmd.Length);

                // 等待 18 字节响应（# + 16字节数据 + $）
                for (int i = 0; i < 200; i++)
                {
                    Thread.Sleep(1);
                    if (_com.BytesToRead >= 18)
                        break;
                }
                if (_com.BytesToRead < 18)
                {
                    _com.DiscardInBuffer();
                    return false;
                }

                byte[] resp = new byte[18];
                _com.Read(resp, 0, 18);
                if (resp[0] != '#' || resp[17] != '$')
                    return false;

                Array.Copy(resp, 1, pageData, 0, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读单字节（兼容旧接口，但未在 UI 中使用，可保留）
        /// </summary>
        public byte ReadA0Byte(int index) => index >= 0 && index < 256 ? _a0Buf[index] : (byte)0;
        public byte ReadA2Byte(int index) => index >= 0 && index < 256 ? _a2Buf[index] : (byte)0;


        /// <summary>
        /// 写单字节（简化实现，按需扩展）
        /// </summary>
        public bool WriteByte(string page, int index, byte value)
        {
            //byte deviceAddr = page.ToUpper() == "A0" ? (byte)160 : (byte)162;
            byte deviceAddr = page.ToUpper() == "A0" ? (byte)0xA0 : (byte)0xA2;
            byte[] cmd = { (byte)'#', 7, 7, deviceAddr, (byte)index, value, (byte)'$' };
            try
            {
                lock (_lock)
                {
                    _com.Write(cmd, 0, cmd.Length);
                    for (int i = 0; i < 200; i++)
                    {
                        //等待延迟
                        Thread.Sleep(1);
                        if (_com.BytesToRead == 3)
                            break;
                    }
                    if (_com.BytesToRead < 3)
                    {
                        _com.DiscardInBuffer();
                        return false;
                    }

                    byte[] resp = new byte[3];
                    _com.Read(resp, 0, 3);
                    return resp[0] == '#' && resp[2] == '$' && resp[1] != 78;
                }
            }
            catch { return false; }
        }

        public void Close() => Dispose();

        public void Dispose()
        {
            if (!_disposed)
            {
                _com?.Close();
                _com?.Dispose();
                _disposed = true;
            }
        }
    }
}
