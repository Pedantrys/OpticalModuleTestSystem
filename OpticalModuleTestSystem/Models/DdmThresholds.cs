using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    /// <summary>
    /// DDM告警阈值（完整版：包含Alarm和Warning两级）
    /// </summary>
    public class DdmThresholds : INotifyPropertyChanged
    {
        // ========== 温度阈值 ==========
        private double _tempHighAlarm;
        public double TempHighAlarm
        {
            get => _tempHighAlarm;
            set { _tempHighAlarm = value; OnPropertyChanged(); }
        }

        private double _tempHighWarning;
        public double TempHighWarning
        {
            get => _tempHighWarning;
            set { _tempHighWarning = value; OnPropertyChanged(); }
        }

        private double _tempLowWarning;
        public double TempLowWarning
        {
            get => _tempLowWarning;
            set { _tempLowWarning = value; OnPropertyChanged(); }
        }

        private double _tempLowAlarm;
        public double TempLowAlarm
        {
            get => _tempLowAlarm;
            set { _tempLowAlarm = value; OnPropertyChanged(); }
        }

        // ========== 电压阈值 ==========
        private double _vccHighAlarm;
        public double VccHighAlarm
        {
            get => _vccHighAlarm;
            set { _vccHighAlarm = value; OnPropertyChanged(); }
        }

        private double _vccHighWarning;
        public double VccHighWarning
        {
            get => _vccHighWarning;
            set { _vccHighWarning = value; OnPropertyChanged(); }
        }

        private double _vccLowWarning;
        public double VccLowWarning
        {
            get => _vccLowWarning;
            set { _vccLowWarning = value; OnPropertyChanged(); }
        }

        private double _vccLowAlarm;
        public double VccLowAlarm
        {
            get => _vccLowAlarm;
            set { _vccLowAlarm = value; OnPropertyChanged(); }
        }

        // ========== 偏置电流阈值 ==========
        private double _biasHighAlarm;
        public double BiasHighAlarm
        {
            get => _biasHighAlarm;
            set { _biasHighAlarm = value; OnPropertyChanged(); }
        }

        private double _biasHighWarning;
        public double BiasHighWarning
        {
            get => _biasHighWarning;
            set { _biasHighWarning = value; OnPropertyChanged(); }
        }

        private double _biasLowWarning;
        public double BiasLowWarning
        {
            get => _biasLowWarning;
            set { _biasLowWarning = value; OnPropertyChanged(); }
        }

        private double _biasLowAlarm;
        public double BiasLowAlarm
        {
            get => _biasLowAlarm;
            set { _biasLowAlarm = value; OnPropertyChanged(); }
        }

        // ========== 发射功率阈值 ==========
        private double _txPowerHighAlarm;
        public double TxPowerHighAlarm
        {
            get => _txPowerHighAlarm;
            set { _txPowerHighAlarm = value; OnPropertyChanged(); }
        }

        private double _txPowerHighWarning;
        public double TxPowerHighWarning
        {
            get => _txPowerHighWarning;
            set { _txPowerHighWarning = value; OnPropertyChanged(); }
        }

        private double _txPowerLowWarning;
        public double TxPowerLowWarning
        {
            get => _txPowerLowWarning;
            set { _txPowerLowWarning = value; OnPropertyChanged(); }
        }

        private double _txPowerLowAlarm;
        public double TxPowerLowAlarm
        {
            get => _txPowerLowAlarm;
            set { _txPowerLowAlarm = value; OnPropertyChanged(); }
        }

        // ========== 接收功率阈值 ==========
        private double _rxPowerHighAlarm;
        public double RxPowerHighAlarm
        {
            get => _rxPowerHighAlarm;
            set { _rxPowerHighAlarm = value; OnPropertyChanged(); }
        }

        private double _rxPowerHighWarning;
        public double RxPowerHighWarning
        {
            get => _rxPowerHighWarning;
            set { _rxPowerHighWarning = value; OnPropertyChanged(); }
        }

        private double _rxPowerLowWarning;
        public double RxPowerLowWarning
        {
            get => _rxPowerLowWarning;
            set { _rxPowerLowWarning = value; OnPropertyChanged(); }
        }

        private double _rxPowerLowAlarm;
        public double RxPowerLowAlarm
        {
            get => _rxPowerLowAlarm;
            set { _rxPowerLowAlarm = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
