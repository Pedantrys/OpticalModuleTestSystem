using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    /// <summary>
    /// 文件名称：ConnectStatus.cs
    /// 功能描述：
    /// </summary>
    /// <author>hui.chen</author>
    /// <createDate>2026.3.31</createDate>
    /// <version>1.0.0</version>
    public enum ConnectStatus
    {
        Disconnected,
        Scanning,
        Connected,
        Initializing,
        Ready,
        Error
    }
}
