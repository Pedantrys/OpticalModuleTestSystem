using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ivi.Visa;

namespace OpticalModuleTestSystem.Drivers
{
    public class GpibCommunicator
    {
        private IMessageBasedSession? _session;

        public bool Connect(int gpibAddress, int board = 0)
        {
            try
            {
                string resource = $"GPIB{board}::{gpibAddress}::INSTR";
                _session = (IMessageBasedSession)GlobalResourceManager.Open(resource);
                _session.TimeoutMilliseconds = 2000;
                return true;
            }
            catch
            {
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
            catch
            {
                return string.Empty;
            }
        }

        public void Write(string command)
        {
            _session?.FormattedIO.WriteLine(command);
        }

        public void Disconnect()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
