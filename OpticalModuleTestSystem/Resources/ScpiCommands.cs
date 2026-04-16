using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Resources
{
    public static class ScpiCommands
    {
        public const string IDN = "*IDN?";
        public const string RST = "*RST";
        public const string CLS = "*CLS";
        public const string ESE = "*ESE 0";
        public const string SRE = "*SRE 0";
    }
}
