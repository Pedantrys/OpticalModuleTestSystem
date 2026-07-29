using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    public class DdmRealTime : INotifyPropertyChanged
    {
        private double _temperature;
        public double Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(); }
        }

        private double _voltage;
        public double Voltage
        {
            get => _voltage;
            set { _voltage = value; OnPropertyChanged(); }
        }

        private double _biasCurrent;
        public double BiasCurrent
        {
            get => _biasCurrent;
            set { _biasCurrent = value; OnPropertyChanged(); }
        }

        private double _txPower;
        public double TxPower
        {
            get => _txPower;
            set { _txPower = value; OnPropertyChanged(); }
        }

        private double _rxPower;
        public double RxPower
        {
            get => _rxPower;
            set { _rxPower = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
