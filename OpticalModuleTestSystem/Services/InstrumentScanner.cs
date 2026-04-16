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
    public class InstrumentScanner
    {
        public List<InstrumentInfo> ScanAll(int startAddr = 0, int endAddr = 30)
        {
            var list = new List<InstrumentInfo>();

            for (int addr = startAddr; addr <= endAddr; addr++)
            {
                var driver = new GpibCommunicator();
                if (!driver.Connect(addr))
                {
                    driver.Disconnect();
                    continue;
                }

                string idn = driver.Query(ScpiCommands.IDN);
                var inst = MatchInstrument(idn, addr);
                inst.Status = ConnectStatus.Connected;
                list.Add(inst);

                driver.Disconnect();
            }
            return list;
        }

        private InstrumentInfo MatchInstrument(string idn, int addr)
        {
            var inst = new InstrumentInfo
            {
                GpibAddress = addr,
                IdnString = idn,
                StatusColor = "#4CD964"
            };

            if (idn.Contains("Temptronic AST-545"))
            {
                inst.Name = "温控平台";
                inst.Model = "AST-545";
                inst.IsTargetDevice = true;
            }
            else if (idn.Contains("EXFO IQS-3150"))
            {
                inst.Name = "光功率模块";
                inst.Model = "IQS-3150";
                inst.IsTargetDevice = true;
            }
            else if (idn.Contains("86100D"))
            {
                inst.Name = "光示波器";
                inst.Model = "86100D";
                inst.IsTargetDevice = true;
            }
            else if (idn.Contains("MP1900A"))
            {
                inst.Name = "误码仪";
                inst.Model = "MP1900A";
                inst.IsTargetDevice = true;
            }
            else if (idn.Contains("MS9740A"))
            {
                inst.Name = "光谱分析仪";
                inst.Model = "MS9740A";
                inst.IsTargetDevice = true;
            }
            else
            {
                inst.Name = "未知设备";
                inst.Model = "Unknown";
                inst.IsTargetDevice = false;
                inst.StatusColor = "#999";
            }
            return inst;
        }
    }
}
