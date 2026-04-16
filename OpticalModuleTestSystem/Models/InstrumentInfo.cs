using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    public partial class InstrumentInfo : ObservableObject
    {
        [ObservableProperty]
        private int _gpibAddress;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _model = string.Empty;

        [ObservableProperty]
        private string _idnString = string.Empty;

        [ObservableProperty]
        private ConnectStatus _status;

        [ObservableProperty]
        private bool _isTargetDevice;

        [ObservableProperty]
        private string _statusColor = "#FF6B6B";
    }
}
