using OpticalModuleTestSystem.Drivers;
using OpticalModuleTestSystem.Models;
using OpticalModuleTestSystem.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Services
{
    public class InstrumentInitializer
    {
        public bool Initialize(InstrumentInfo instrument)
        {
            try
            {
                var driver = new GpibCommunicator();
                if (!driver.Connect(instrument.GpibAddress))
                    return false;

                driver.Write(ScpiCommands.RST);
                driver.Write(ScpiCommands.CLS);
                string idn = driver.Query(ScpiCommands.IDN);

                driver.Disconnect();
                return !string.IsNullOrEmpty(idn);
            }
            catch
            {
                return false;
            }
        }
    }
}
