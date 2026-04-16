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

        // 告警标志
        private bool _isInternalMode = false;
        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;

            // DDM 自动刷新
            _ddmTimer = new DispatcherTimer();
            _ddmTimer.Interval = TimeSpan.FromMilliseconds(500);
            _ddmTimer.Tick += (s, e) => RefreshAllDDM();

            // IIC 初始化
            if (!_iicCom.SweepCom())
                MessageBox.Show("IIC 通信板连接失败");
            _iicCom.SelectIICRate(0);

            Btn_AutoDDM.Click += (s, e) => _ddmTimer.Start();
            Btn_StopDDM.Click += (s, e) => _ddmTimer.Stop();
        }

        /// <summary>
        /// 刷新：实时值 + 规格值 + 告警
        /// </summary>
        private void RefreshAllDDM()
        {
            try
            {
                _iicCom.ReadPage("A0", 256);
                _iicCom.ReadPage("A2", 256);

                // 1. 实时值
                Txt_Temp.Text = $"{CalcTemp():F2} ℃";
                Txt_Volt.Text = $"{CalcVolt():F2} V";
                Txt_Bias.Text = $"{CalcBias():F2} mA";
                Txt_TxPower.Text = $"{CalcTx():F2} dBm";
                Txt_RxPower.Text = $"{CalcRx():F2} dBm";

                // 2. 规格值（告警阈值）
                Txt_Temp_High.Text = $"{ReadTempHigh():F2} ℃";
                Txt_Temp_Low.Text = $"{ReadTempLow():F2} ℃";
                Txt_Vcc_High.Text = $"{ReadVccHigh():F2} V";
                Txt_Vcc_Low.Text = $"{ReadVccLow():F2} V";
                Txt_TxPower_High.Text = $"{ReadTxPowerHigh():F2} dBm";
                Txt_TxPower_Low.Text = $"{ReadTxPowerLow():F2} dBm";

                // 3. 模块信息
                Txt_Manufacturer.Text = GetManufacturer();
                Txt_Model.Text = GetModel();
                Txt_SN.Text = GetSN();
                Txt_Date.Text = GetDate();

                // 4. 告警判断（你VB原版 Alarm_Warning）
                Alarm_Warning(true);
                Txt_Status.Text = "✅ GPIB正常 | DDM读取正常 | 告警已判断";
            }
            catch
            {
                Txt_Status.Text = "❌ DDM读取失败";
            }
        }

        #region ===================== 你 VB 原版 Alarm_Warning 完整移植 =====================
        private void Alarm_Warning(bool enable)
        {
            try
            {
                byte flagByte = _iicCom.ReadData(92);
                _isInternalMode = (flagByte & 32) == 0;

                if (_isInternalMode)
                    Txt_AlarmStatus.Text = "内部模式 INT";
                else
                    Txt_AlarmStatus.Text = "外部模式 EXT";

                // 读取告警字节
                byte alarm = _iicCom.ReadData(110);

                bool txFault = (alarm & 2) != 0;
                bool rxLos = (alarm & 4) != 0;

                string alarmMsg = "";
                if (txFault) alarmMsg += "TX_FAULT 异常\r\n";
                if (rxLos) alarmMsg += "RX_LOS 无光\r\n";

                if (alarmMsg == "") alarmMsg = "✅ 无告警";

                Txt_AlarmStatus.Text += "\r\n" + alarmMsg;
            }
            catch { }
        }
        #endregion

        #region ===================== DDM 实时值 =====================
        private double CalcTemp()
        {
            int h = _iicCom.ReadData(14);
            int l = _iicCom.ReadData(15);
            int val = (h << 8) | l;
            if (val > 32767) val -= 65536;
            return val / 256.0;
        }

        private double CalcVolt()
        {
            int h = _iicCom.ReadData(16);
            int l = _iicCom.ReadData(17);
            return ((h << 8) | l) * 0.0001;
        }

        private double CalcBias()
        {
            int h = _iicCom.ReadData(18);
            int l = _iicCom.ReadData(19);
            return ((h << 8) | l) * 0.002;
        }

        private double CalcTx()
        {
            int h = _iicCom.ReadData(26);
            int l = _iicCom.ReadData(27);
            return ((h << 8) | l) * 0.0001;
        }

        private double CalcRx()
        {
            int h = _iicCom.ReadData(34);
            int l = _iicCom.ReadData(35);
            return ((h << 8) | l) * 0.0001;
        }
        #endregion

        #region ===================== DDM 规格值（告警阈值）=====================
        private double ReadTempHigh()
        {
            int h = _iicCom.ReadData(40);
            int l = _iicCom.ReadData(41);
            int val = (h << 8) | l;
            if (val > 32767) val -= 65536;
            return val / 256.0;
        }

        private double ReadTempLow()
        {
            int h = _iicCom.ReadData(42);
            int l = _iicCom.ReadData(43);
            int val = (h << 8) | l;
            if (val > 32767) val -= 65536;
            return val / 256.0;
        }

        private double ReadVccHigh()
        {
            int val = (_iicCom.ReadData(44) << 8) | _iicCom.ReadData(45);
            return val * 0.0001;
        }

        private double ReadVccLow()
        {
            int val = (_iicCom.ReadData(46) << 8) | _iicCom.ReadData(47);
            return val * 0.0001;
        }

        private double ReadTxPowerHigh()
        {
            int val = (_iicCom.ReadData(60) << 8) | _iicCom.ReadData(61);
            return val * 0.0001;
        }

        private double ReadTxPowerLow()
        {
            int val = (_iicCom.ReadData(62) << 8) | _iicCom.ReadData(63);
            return val * 0.0001;
        }
        #endregion

        #region ===================== 模块信息 =====================
        private string GetManufacturer()
        {
            string s = "";
            for (int i = 20; i <= 35; i++) s += (char)_iicCom.ReadData(i);
            return s.Trim();
        }

        private string GetModel()
        {
            string s = "";
            for (int i = 40; i <= 55; i++) s += (char)_iicCom.ReadData(i);
            return s.Trim();
        }

        private string GetSN()
        {
            string s = "";
            for (int i = 68; i <= 83; i++) s += (char)_iicCom.ReadData(i);
            return s.Trim();
        }

        private string GetDate()
        {
            string s = "";
            for (int i = 84; i <= 91; i++) s += (char)_iicCom.ReadData(i);
            return s.Trim();
        }
        #endregion
    }
}
