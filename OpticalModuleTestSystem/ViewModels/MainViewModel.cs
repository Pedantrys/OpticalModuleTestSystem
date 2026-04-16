using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpticalModuleTestSystem.Models;
using OpticalModuleTestSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalModuleTestSystem.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly InstrumentScanner _scanner = new();
        private readonly InstrumentInitializer _initializer = new();

        [ObservableProperty]
        private ObservableCollection<InstrumentInfo> _instruments = new();

        [ObservableProperty]
        private string _log = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [RelayCommand]
        public void ScanInstruments()
        {
            IsBusy = true;
            Log += $"[{DateTime.Now:HH:mm:ss}] 开始扫描GPIB仪器...\r\n";

            Task.Run(() =>
            {
                var list = _scanner.ScanAll();
                Instruments.Clear();
                foreach (var item in list)
                {
                    Instruments.Add(item);
                    Log += $"[{DateTime.Now:HH:mm:ss}] 找到：{item.Name} {item.Model} @{item.GpibAddress}\r\n";
                }
                IsBusy = false;
                Log += $"[{DateTime.Now:HH:mm:ss}] 扫描完成\r\n";
            });
        }

        [RelayCommand]
        public void InitializeAll()
        {
            IsBusy = true;
            Log += $"[{DateTime.Now:HH:mm:ss}] 开始初始化所有仪器...\r\n";

            Task.Run(() =>
            {
                foreach (var inst in Instruments)
                {
                    if (!inst.IsTargetDevice) continue;

                    inst.Status = ConnectStatus.Initializing;
                    bool ok = _initializer.Initialize(inst);

                    inst.Status = ok ? ConnectStatus.Ready : ConnectStatus.Error;
                    inst.StatusColor = ok ? "#4CD964" : "#FF3B30";

                    Log += ok
                        ? $"[{DateTime.Now:HH:mm:ss}] {inst.Name} 初始化成功\r\n"
                        : $"[{DateTime.Now:HH:mm:ss}] {inst.Name} 初始化失败\r\n";
                }
                IsBusy = false;
            });
        }


    }
}
