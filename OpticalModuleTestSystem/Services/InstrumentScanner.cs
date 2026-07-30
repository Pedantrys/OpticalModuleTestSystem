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
    /// <summary>
    /// 仪器扫描服务（修复版）
    /// </summary>
    public class InstrumentScanner
    {
        /// <summary>
        /// 扫描GPIB总线上的所有仪器
        /// </summary>
        /// <param name="startAddr">起始地址</param>
        /// <param name="endAddr">结束地址</param>
        /// <returns></returns>
        public List<InstrumentInfo> ScanAll(int startAddr = 0, int endAddr = 30)
        {
            var list = new List<InstrumentInfo>();

            for (int addr = startAddr; addr <= endAddr; addr++)
            {
                using var driver = new GpibCommunicator();
                if (!driver.Connect(addr))
                {
                    driver.Disconnect();
                    continue;
                }

                string idn = driver.Query(ScpiCommands.IDN);
                if (string.IsNullOrWhiteSpace(idn))
                {
                    continue;
                }

                var inst = MatchInstrument(idn, addr);
                inst.Status = ConnectStatus.Connected;
                list.Add(inst);

                driver.Disconnect();
            }
            return list;
        }

        /// <summary>
        /// 根据IDN字符串匹配仪器型号
        /// </summary>
        /// <param name="idn"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        private InstrumentInfo MatchInstrument(string idn, int addr)
        {
            var inst = new InstrumentInfo
            {
                GpibAddress = addr,
                IdnString = idn,
                StatusColor = "#4CD964"
            };

            string idnUpper = idn.ToUpper();

            if (idnUpper.Contains("TEMPTRONIC") && idnUpper.Contains("ATS-545"))
            {
                inst.Name = "温控平台";
                inst.Model = "ATS-545";
                inst.IsTargetDevice = true;
            }
            else if (idnUpper.Contains("EXFO") && (idnUpper.Contains("IQS-610P") || idnUpper.Contains("IQS600")))
            {
                inst.Name = "光功率/衰减模块";
                inst.Model = "IQS-610P";
                inst.IsTargetDevice = true;
            }
            else if (idnUpper.Contains("KEYSIGHT") && idnUpper.Contains("86100"))
            {
                inst.Name = "光示波器";
                inst.Model = "86100D";
                inst.IsTargetDevice = true;
            }
            else if (idnUpper.Contains("MP1900A") && idnUpper.Contains("MP1900"))
            {
                inst.Name = "误码仪";
                inst.Model = "MP1900A";
                inst.IsTargetDevice = true;
            }
            else if (idnUpper.Contains("MS9740A") && idnUpper.Contains("MS9740"))
            {
                inst.Name = "光谱分析仪";
                inst.Model = "MS9740A";
                inst.IsTargetDevice = true;
            }
            else
            {
                // 未知设备：保留原始IDN信息，标记为非目标设备
                inst.Name = "未知设备";
                inst.Model = idn.Split(',').FirstOrDefault() ?? "Unknown";
                inst.IsTargetDevice = false;
                inst.StatusColor = "#999999";
            }
            return inst;
        }
    }
}
