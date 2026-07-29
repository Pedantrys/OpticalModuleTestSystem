using OpticalModuleTestSystem.Drivers;
using OpticalModuleTestSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpticalModuleTestSystem.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 2. 这里声明所有缺失的成员变量
        private MainViewModel _vm;
        private DispatcherTimer _ddmTimer;
        private IICCom _iicCom;

        public MainWindow()
        {
            // 第一步：先调用 InitializeComponent（必须放在最前面）
            InitializeComponent();

            // 初始化驱动
            _iicCom = new IICCom();
            bool iicOk = _iicCom.SweepCom();
            if (!iicOk)
            {
                MessageBox.Show("IIC 通信板未找到，请检查串口连接！\\nDDM功能将被禁用。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                // 可考虑继续运行但禁用 DDM 功能
            }
            else
            {
                _iicCom.SelectIICRate(0);  // 0=100KHz
            }

            // 初始化ViewModel并绑定
            _vm = new MainViewModel(_iicCom);
            DataContext = _vm;

            // DDM自动刷新定时器
            _ddmTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)  // 从 500 改为 1000
            };
            _ddmTimer.Tick += (s, e) => _vm.RefreshAllDdm();

            // 按钮事件
            Btn_AutoDDM.Click += (s, e) =>
            {
                _ddmTimer.Start();
                _vm.AddLog("DDM自动刷新已启动（1秒/次）");
            };
            Btn_StopDDM.Click += (s, e) =>
            {
                _ddmTimer.Stop();
                _vm.AddLog("DDM自动刷新已停止");
            };

            //日志自动滚动
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.Log))
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        LogScrollViewer?.ScrollToEnd();
                    }, DispatcherPriority.Background);
                }
            };

            // 窗口加载完成后自动扫描
            Loaded += async (s, e) =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _vm.ScanInstrumentsCommand.Execute(null);
                }, DispatcherPriority.Background);
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _ddmTimer?.Stop();
            _iicCom?.Close();
            base.OnClosing(e);
        }
    }
}
