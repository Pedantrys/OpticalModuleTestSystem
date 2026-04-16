using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Drivers
{
    public class IICCom
    {
        private SerialPort _com;
        private byte[] _dataBuf = new byte[256];

        public bool SweepCom()
        {
            try
            {
                _com = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);
                _com.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SelectIICRate(int rate)
        {
            // 0=100KHz 1=1Hz
            SendCommand(rate == 0 ? "RATE0" : "RATE1");
        }

        public void ReadPage(string addr, int len)
        {
            SendCommand($"READ {addr} 0 {len}");
            // 硬件返回数据解析
        }

        public byte ReadData(int index)
        {
            return index >= 0 && index < 256 ? _dataBuf[index] : (byte)0;
        }

        public bool WriteByte(string addr, int index, byte value)
        {
            SendCommand($"WRITE {addr} {index} {value:X2}");
            return true;
        }

        private void SendCommand(string cmd)
        {
            if (_com == null || !_com.IsOpen) return;
            _com.WriteLine(cmd);
        }
    }
}
