using OpticalModuleTestSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpticalModuleTestSystem.Drivers
{
    /// <summary>
    /// 文件名称：DdmParser.cs
    /// 功能描述：DDM/A2页数据解析工具（已修复校准逻辑）
    /// </summary>
    /// <author>hui.chen</author>
    /// <createDate>2026.7.8</createDate>
    /// <version>1.0.0</version>
    public static class DdmParser
    {
        #region 基础工具方法
        /// <summary>
        /// 十进制转十六进制字符串
        /// </summary>
        public static string DecToHex(long value)
        {
            if (value == 0) return "0";
            StringBuilder sb = new StringBuilder();
            bool isNegative = value < 0;
            if (isNegative) value = -value;
            while (value > 0)
            {
                long mod = value % 16;
                sb.Insert(0, mod < 10 ? mod.ToString() : ((char)('A' + mod - 10)).ToString());
                value /= 16;
            }
            return (isNegative ? "-" : "") + sb.ToString();
        }

        /// <summary>
        /// 以10为底的对数，安全处理
        /// </summary>
        public static double SafeLog10(double x)
        {
            if (x <= 0) return -99.99;
            return Math.Log10(x);
        }

        // 大端模式：2字节转UInt16（MSB在前）
        private static ushort ToUInt16BigEndian(byte hi, byte lo) => (ushort)(hi << 8 | lo);

        // 大端模式：2字节转Int16有符号数
        private static short ToInt16BigEndian(byte hi, byte lo) => (short)(hi << 8 | lo);
        #endregion

        #region 1. 模块基础信息解析（A0页）
        /// <summary>
        /// 解析A0页厂商、型号、SN、日期
        /// </summary>
        public static DdmModuleInfo ParseModuleInfo(byte[] readData)
        {
            if (readData == null || readData.Length < 128)
                throw new ArgumentException("A0页数据长度不足");

            DdmModuleInfo info = new DdmModuleInfo();
            // 生产厂家：字节20-35
            info.Manufacturer = Encoding.ASCII.GetString(readData, 20, 16).Trim();
            // 模块型号：字节40-55
            info.Model = Encoding.ASCII.GetString(readData, 40, 16).Trim();
            // SN号：字节68-83
            info.SerialNumber = Encoding.ASCII.GetString(readData, 68, 16).Trim();
            // 生产日期：字节84-91
            info.DateCode = Encoding.ASCII.GetString(readData, 84, 8).Trim();
            return info;
        }
        #endregion

        #region 2. 无校准DDM实时值解析
        /// <summary>
        /// 原始AD值直接计算，不使用校准系数
        /// </summary>
        public static DdmRealTimeData ParseDdmInt(byte[] readData)
        {
            if (readData == null || readData.Length < 128)
                throw new ArgumentException("A2页数据长度不足");

            DdmRealTimeData data = new DdmRealTimeData();

            // 温度：字节96-97，8.8格式有符号数
            short tempRaw = ToInt16BigEndian(readData[96], readData[97]);
            data.Temperature = tempRaw / 256.0;

            // 电压：字节98-99，单位V（0.0001V/LSB）
            ushort voltRaw = ToUInt16BigEndian(readData[98], readData[99]);
            data.Voltage = voltRaw * 0.0001;

            // 偏置电流：字节100-101，单位mA（2uA/LSB → mA）
            ushort biasRaw = ToUInt16BigEndian(readData[100], readData[101]);
            data.BiasCurrent = biasRaw * 2.0 * 0.001;

            // 发射功率：字节102-103，转dBm（0.0001mW/LSB）
            ushort txRaw = ToUInt16BigEndian(readData[102], readData[103]);
            double txMw = txRaw * 0.0001;
            data.TxPower = txRaw == 0 ? -99.99 : SafeLog10(txMw) * 10;

            // 接收功率：字节104-105，转dBm
            ushort rxRaw = ToUInt16BigEndian(readData[104], readData[105]);
            double rxMw = rxRaw * 0.0001;
            data.RxPower = rxRaw == 0 ? -99.99 : SafeLog10(rxMw) * 10;

            return data;
        }
        #endregion

        #region  3. 带校准DDM实时值解析（修复版）
        /// <summary>
        /// 使用模块内部校准系数计算，精度更高
        /// </summary>
        public static DdmRealTimeData ParseDdmExt(byte[] readData)
        {
            if (readData == null || readData.Length < 128)
                throw new ArgumentException("A2页数据长度不足");

            DdmRealTimeData data = new DdmRealTimeData();

            // ---------- 温度校准：字节84-85斜率，86-87偏移 ----------
            // 斜率：8位整数 + 8位小数
            double tempSlope = readData[84] + readData[85] / 256.0;
            short tempOffset = ToInt16BigEndian(readData[86], readData[87]);

            // AD值：有符号16位，8.8格式
            short tempAd = ToInt16BigEndian(readData[96], readData[97]);
            double tempAdVal = tempAd / 256.0;  // 8.8格式转实际值

            // 校准计算
            data.Temperature = (tempSlope * tempAdVal + tempOffset / 256.0);

            //data.Temperature = tempAdVal < 128 ? tempAdVal : tempAdVal - 256;
            //// 校准计算（对齐原VB逻辑）
            //data.Temperature = tempSlope * tempAd + tempOffset; // 注：原VB此处逻辑与ddm_int一致，保留校准公式
            //data.Temperature /= 256.0;

            // ---------- 电压校准：字节88-89斜率，90-91偏移 ----------
            double voltSlope = readData[88] + readData[89] / 256.0;
            short voltOffset = ToInt16BigEndian(readData[90], readData[91]);
            ushort voltAd = ToUInt16BigEndian(readData[98], readData[99]);
            data.Voltage = (voltSlope * voltAd + voltOffset) / 10000.0;

            // ---------- 偏置电流校准：字节76-77斜率，78-79偏移 ----------
            double biasSlope = readData[76] + readData[77] / 256.0;
            short biasOffset = ToInt16BigEndian(readData[78], readData[79]);
            ushort biasAd = ToUInt16BigEndian(readData[100], readData[101]);
            data.BiasCurrent = (biasSlope * biasAd + biasOffset) * 2.0 * 0.001;

            // ---------- 发射功率校准：字节80-81斜率，82-83偏移 ----------
            double txSlope = readData[80] + readData[81] / 256.0;
            short txOffset = ToInt16BigEndian(readData[82], readData[83]);
            ushort txAd = ToUInt16BigEndian(readData[102], readData[103]);
            double txCalibrated = txSlope * txAd + txOffset;
            data.TxPower = txAd == 0 ? -99.99 : SafeLog10(txCalibrated / 10000.0) * 10;

            // ---------- 接收功率4次多项式校准：字节56-71等5组系数 ----------
            // 系数存储顺序：从字节56开始，每4字节一个float（IEEE 754）
            double[] rxCoeff = new double[5]; // 索引4=4次项，0=常数项
            for (int i = 0; i < 5; i++)
            {
                int startAddr = 56 + i * 4;  // 修正：从56开始，不是72
                byte[] floatBytes = new byte[4];
                // 大端字节序转小端
                floatBytes[3] = readData[startAddr];
                floatBytes[2] = readData[startAddr + 1];
                floatBytes[1] = readData[startAddr + 2];
                floatBytes[0] = readData[startAddr + 3];
                rxCoeff[i] = BitConverter.ToSingle(floatBytes, 0);
            }
            ushort rxAd = ToUInt16BigEndian(readData[104], readData[105]);
            double rxCalibrated = rxCoeff[4] * Math.Pow(rxAd, 4)
                                + rxCoeff[3] * Math.Pow(rxAd, 3)
                                + rxCoeff[2] * Math.Pow(rxAd, 2)
                                + rxCoeff[1] * rxAd
                                + rxCoeff[0];

            data.RxPower = rxAd == 0 ? -99.99 : SafeLog10(rxCalibrated / 10000.0) * 10;

            return data;
        }
        #endregion

        #region 4. 标准告警阈值与FLAG解析
        /// <summary>
        /// 解析告警阈值与20位状态标志
        /// </summary>
        public static DdmAlarmData ParseAlarmThresholds(byte[] readData, bool enableCalibration = true)
        {
            if (readData == null || readData.Length < 128)
                throw new ArgumentException("A2页数据长度不足");

            DdmAlarmData alarm = new DdmAlarmData();
            double[] temp = new double[4];
            double[] volt = new double[4];
            double[] bias = new double[4];
            double[] txPwr = new double[4];
            double[] rxPwr = new double[4];

            // ========== 温度阈值：字节0-7（高告警/高警告/低警告/低告警）==========
            for (int i = 0; i < 4; i++)
            {
                short raw = ToInt16BigEndian(readData[i * 2], readData[i * 2 + 1]);
                temp[i] = raw / 256.0;
            }
            alarm.TempThresholds = temp;

            // ========== 电压阈值：字节8-15 ==========
            for (int i = 0; i < 4; i++)
            {
                ushort raw = ToUInt16BigEndian(readData[8 + i * 2], readData[8 + i * 2 + 1]);
                volt[i] = raw * 0.0001;
            }
            alarm.VoltageThresholds = volt;

            // ========== 偏置电流校准系数 ==========
            double biasGain = 1.0, biasOffset = 0;
            if (enableCalibration)
            {
                biasGain = readData[76] + readData[77] / 256.0;
                biasOffset = ToInt16BigEndian(readData[78], readData[79]);
            }

            // ========== 偏置电流阈值：字节16-23 ==========
            for (int i = 0; i < 4; i++)
            {
                ushort raw = ToUInt16BigEndian(readData[16 + i * 2], readData[16 + i * 2 + 1]);
                double val = enableCalibration ? (biasGain * raw + biasOffset) : raw;
                bias[i] = val * 2.0 * 0.001;
            }
            alarm.BiasThresholds = bias;

            // ========== 发射功率校准系数 ==========
            double txGain = 1.0, txOffset = 0;
            if (enableCalibration)
            {
                txGain = readData[80] + readData[81] / 256.0;
                txOffset = ToInt16BigEndian(readData[82], readData[83]);
            }

            // ========== 发射功率阈值：字节24-31 ==========
            for (int i = 0; i < 4; i++)
            {
                ushort raw = ToUInt16BigEndian(readData[24 + i * 2], readData[24 + i * 2 + 1]);
                double val = enableCalibration ? (txGain * raw + txOffset) : raw;
                txPwr[i] = SafeLog10(val * 0.0001) * 10;
            }
            alarm.TxPowerThresholds = txPwr;

            // ========== 接收功率校准系数 ==========
            double[] rxCoeff = new double[5];
            if (enableCalibration)
            {
                for (int i = 0; i < 5; i++)
                {
                    int startAddr = 56 + i * 4;
                    byte[] floatBytes = new byte[4];
                    floatBytes[3] = readData[startAddr];
                    floatBytes[2] = readData[startAddr + 1];
                    floatBytes[1] = readData[startAddr + 2];
                    floatBytes[0] = readData[startAddr + 3];
                    rxCoeff[i] = BitConverter.ToSingle(floatBytes, 0);
                }
            }

            // ========== 接收功率阈值：字节32-39 ==========
            for (int i = 0; i < 4; i++)
            {
                ushort raw = ToUInt16BigEndian(readData[32 + i * 2], readData[32 + i * 2 + 1]);
                double val = raw;
                if (enableCalibration)
                {
                    val = rxCoeff[4] * Math.Pow(raw, 4)
                        + rxCoeff[3] * Math.Pow(raw, 3)
                        + rxCoeff[2] * Math.Pow(raw, 2)
                        + rxCoeff[1] * raw
                        + rxCoeff[0];
                }
                rxPwr[i] = SafeLog10(val * 0.0001) * 10;
            }
            alarm.RxPowerThresholds = rxPwr;

            // ========== 20位告警FLAG：字节112-113、116-117 ==========
            ushort flagWord1 = ToUInt16BigEndian(readData[112], readData[113]);
            ushort flagWord2 = ToUInt16BigEndian(readData[116], readData[117]);

            alarm.AlarmFlags = new bool[20];

            // flagWord1 对应位15-6（10位）
            for (int i = 0; i < 10; i++)
            {
                alarm.AlarmFlags[i + 10] = (flagWord1 & (0x8000 >> i)) != 0;
            }

            // flagWord2 对应位15-6（10位）
            for (int i = 0; i < 10; i++)
            {
                alarm.AlarmFlags[i] = (flagWord2 & (0x8000 >> i)) != 0;
            }

            return alarm;
        }
        #endregion

        #region 5. 中兴扩展告警解析
        /// <summary>
        /// 解析A2页自定义扩展告警区域
        /// </summary>
        public static DdmExtAlarmData ParseExtAlarm(byte[] readData)
        {
            if (readData == null || readData.Length < 256)
                return new DdmExtAlarmData { IsSupported = false };

            DdmExtAlarmData ext = new DdmExtAlarmData();

            // 功能支持判断：字节244不等于128则无此功能
            if (readData[244] != 128)
            {
                ext.IsSupported = false;
                return ext;
            }
            ext.IsSupported = true;

            string[] flagTexts = { "健康", "风险", "未知", "故障" };
            Brush[] flagBrushes = { Brushes.Green, Brushes.Yellow, Brushes.Black, Brushes.Red };

            // 温度：字节248-249（0.01℃/LSB）
            ushort tempRaw = ToUInt16BigEndian(readData[248], readData[249]);
            ext.Temp = tempRaw * 0.01;
            int tempFlagVal = (readData[246] & 0xC0) >> 6;
            ext.TempFlag = flagTexts[Math.Min(tempFlagVal, 3)];
            ext.TempFlagBrush = flagBrushes[Math.Min(tempFlagVal, 3)];

            // 偏置：字节250-251（0.01mA/LSB）
            ushort biasRaw = ToUInt16BigEndian(readData[250], readData[251]);
            ext.Bias = biasRaw * 0.01;
            int biasFlagVal = (readData[246] & 0x30) >> 4;
            ext.BiasFlag = flagTexts[Math.Min(biasFlagVal, 3)];
            ext.BiasFlagBrush = flagBrushes[Math.Min(biasFlagVal, 3)];

            // 发射功率：字节252-253（0.1dBm/LSB）
            ushort txRaw = ToUInt16BigEndian(readData[252], readData[253]);
            ext.TxPower = txRaw * 0.1;
            int txFlagVal = (readData[246] & 0x0C) >> 2;
            ext.TxPowerFlag = flagTexts[Math.Min(txFlagVal, 3)];
            ext.TxPowerFlagBrush = flagBrushes[Math.Min(txFlagVal, 3)];

            // 接收功率：字节254-255（0.1dBm/LSB）
            ushort rxRaw = ToUInt16BigEndian(readData[254], readData[255]);
            ext.RxPower = rxRaw * 0.1;
            int rxFlagVal = readData[246] & 0x03;
            ext.RxPowerFlag = flagTexts[Math.Min(rxFlagVal, 3)];
            ext.RxPowerFlagBrush = flagBrushes[Math.Min(rxFlagVal, 3)];

            return ext;
        }
        #endregion

        #region 6. IIC读取与界面刷新（对应原VB read_iic）
        /// <summary>
        /// 读取IIC页面并格式化数据（十进制/十六进制切换）
        /// </summary>
        /// <param name="iicCom">IIC通信实例</param>
        /// <param name="pageAddr">页地址</param>
        /// <param name="startOffset">起始偏移</param>
        /// <param name="length">读取长度</param>
        /// <param name="isHex">是否十六进制显示</param>
        /// <param name="readData">输出原始字节数组</param>
        /// <returns>读取是否成功</returns>
        public static bool ReadIicPage(IICCom iicCom, string pageAddr, int startOffset, int length, bool isHex, out byte[] readData)
        {
            readData = new byte[256];
            if (startOffset + length > 128)
            {
                // 超出范围处理，同原VB注释逻辑
                return false;
            }

            if (!iicCom.Read_Page(pageAddr, length))
            {
                return false;
            }

            // 原始数据从IIC类读取（需对应IICCom类的Read_Data属性）
            readData = IICCom.Read_Data;
            return true;
        }
        #endregion
    }
}
