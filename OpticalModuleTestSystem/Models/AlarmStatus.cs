using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    public class AlarmStatus : INotifyPropertyChanged
    {
        private bool _isInternalMode;
        public bool IsInternalMode
        {
            get => _isInternalMode;
            set
            {
                _isInternalMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeText));
                OnPropertyChanged(nameof(CombinedStatus));
            }
        }

        public string ModeText => IsInternalMode ? "内部模式 INT" : "外部模式 EXT";

        private bool _txFault;
        public bool TxFault
        {
            get => _txFault;
            set
            {
                _txFault = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlarmMessage));
                OnPropertyChanged(nameof(CombinedStatus));
            }
        }

        private bool _rxLos;
        public bool RxLos
        {
            get => _rxLos;
            set
            {
                _rxLos = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlarmMessage));
                OnPropertyChanged(nameof(CombinedStatus));
            }
        }

        public string AlarmMessage
        {
            get
            {
                List<string> alarms = new List<string>();
                if (TxFault) alarms.Add("TX_FAULT 异常");
                if (RxLos) alarms.Add("RX_LOS 无光");
                return alarms.Count == 0 ? "✅ 无告警" : string.Join("\r\n", alarms);
            }
        }

        private string _runStatus = "就绪";
        public string RunStatus
        {
            get => _runStatus;
            set { _runStatus = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string CombinedStatus
        {
            get => $"{ModeText}\r\n{AlarmMessage}";
        }
    }
}
