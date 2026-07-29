using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.Models
{
    public class ModuleInfo : INotifyPropertyChanged
    {
        private string _manufacturer = string.Empty;
        public string Manufacturer
        {
            get => _manufacturer;
            set { _manufacturer = value; OnPropertyChanged(); }
        }

        private string _model = string.Empty;
        public string Model
        {
            get => _model;
            set { _model = value; OnPropertyChanged(); }
        }

        private string _serialNumber = string.Empty;
        public string SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(); }
        }

        private string _dateCode = string.Empty;
        public string DateCode
        {
            get => _dateCode;
            set { _dateCode = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
