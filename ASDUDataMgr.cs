using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Power2000.General.CommTools;
using System.Data;
using System.IO;

using Power2000.General.StandardDef;

/*
 * 104规约
 * */
namespace Power2000.CommuCfg
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIDataInfo            //处理遥信、遥测
    {
        public APCIFrameHead head;
        public APCI_ASDU ASDU;            // 数据单元
        public APCI_ASDU_DATA rtASDU;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIFVInfo
    {
        public APCIFrameHead head;           //报文头
        public APCI_ASDU ASDU;               // 数据单元
        public APCI_ASDU_FV rtASDU;          //信息体
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIFileInfo
    {
        public APCIFrameHead head;            //报文头
        public APCI_ASDU ASDU;                // 数据单元
        public APCI_ASDU_FILE rtASDU;         //信息体
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCI_ASDU_ST               //串口101规约定义的APDU
    {
        public APCI_ASDU_S ASDU;             // 数据单元
        public Byte wInfoAddr;			     // 信息体地址
        public Byte byInfoAddrH;			 // 信息体地址2
        public Byte byDataIndex;			 // 数据地址
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIDataInfoST             //串口101规约定义的APDU
    {
        public APCIFrameHead head;           // 报文头
        public APCI_ASDU_ST rtASDU;          // 信息体
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCISNTPInfo
    {
        public APCIFrameHead head;           //报文头
        public APCI_ASDU ASDU;               // 数据单元
        public APCI_ASDU_SNTP rtASDU;          //信息体
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCI_ASDU_SNTP
    {
        public short wInfoAddr;			     // 信息体地址
        public Byte byInfoAddrH;			 // 信息体地址2
        public APCI_SOE_TIME time;           // 年月日时分毫秒
    }

    public class ASDUDataMgr : PtlmoduleMgr
    {
        private static ASDUDataMgr instance;
        public static ASDUDataMgr Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ASDUDataMgr();
                }
                return instance;
            }
        } 

        //public event RtDataReportEventHandler RtDataReportEvent;
        //public event LinkEventHandler LinkEvent;

        private bool isRtDataReflashed = false;//刷新表格数据
        public bool IsRtDataReflashed
        {
            get {  return isRtDataReflashed; }
            set { isRtDataReflashed = value; }
        }

        private bool isFvDataReflashed = false;//刷新功能定值表格数据
        public bool IsFvDataReflashed
        {
            get { return isFvDataReflashed; }
            set { isFvDataReflashed = value; }
        }

        private bool isSNTPReflashed = false;//刷新功能定值表格数据
        public bool IsSNTPReflashed
        {
            get { return isSNTPReflashed; }
            set { isSNTPReflashed = value; }
        }

        private bool isSystemReflashed = false;//刷新表格数据
        public bool IsSystemReflashed
        {
            get { return isSystemReflashed; }
            set { isSystemReflashed = value; }
        }

        private bool isVersionReflashed = false;//刷新表格数据
        public bool IsVersionReflashed
        {
            get { return isVersionReflashed; }
            set { isVersionReflashed = value; }
        }

            

        /*
        * 接收数据报文处理
        * */
        public override bool OnRecvData(byte[] RecvData)
        {
            SConfigForm.Instance.t0 = 0;
            bool IsFinished = false;            
            IntPtr ptr = Marshal.AllocHGlobal(RecvData.Length);
            unsafe
            {
                Marshal.Copy(RecvData, 0, ptr, sizeof(APCIDataInfo));
            }
            APCIDataInfo rtInfo = (APCIDataInfo)Marshal.PtrToStructure(ptr, typeof(APCIDataInfo));
            Marshal.FreeHGlobal(ptr);
            string s = RecvDataFormat(RecvData);
            int dataLen = rtInfo.head.length + Marshal.SizeOf(typeof(short));

            byte[] recvArray = new byte[dataLen];
            Array.Copy(RecvData, 0, recvArray, 0, dataLen);
            
            if ((rtInfo.head.ctlreg.wctlSN & 0x01) == 0)
            {
                CLinklayerMgr.Instance.IsSendFlag = false;
                
                //nPackageNum++;//计算I帧报文数
                CLinklayerMgr.Instance.SetCountAPCI_I();
                CLinklayerMgr.Instance.SetNotifyRecvSN();                
            }

            CLinklayerMgr.Instance.ShowRecvInfo(recvArray); 

            if (CLinklayerMgr.Instance.GetSendFlag_I(rtInfo))
            {
                CLinklayerMgr.Instance.SendConfirm_S();//发送S帧确认帧
            }
            else if (rtInfo.ASDU.byType == 0x46)
            {
                CLinklayerMgr.Instance.CallupAllData();//发送总召命令
            }
            else if (rtInfo.ASDU.byType == 0x78)
            {
                IsCanSendFinished = true;
                //CallUpHistoryData(short.Parse(strfileName), 2);
            }

            if (rtInfo.head.ctlreg.wctlSN == APCI_TESTFR_Act)//发送U帧确认帧 0x43
            {
                CLinklayerMgr.Instance.SendConfirm_U();
            }
            else if (rtInfo.head.ctlreg.wctlSN == APCI_STOPDT_Act)//重新建立链路 0x13
            {                
                CLinklayerMgr.Instance.InitialLinklayer();
                CLinklayerMgr.Instance.ResetSN();
            }

            if ((rtInfo.head.ctlreg.wctlSN & 0x01) == 0)
            {
                switch (rtInfo.ASDU.byType)
                {
                    case (byte)ParseType.YX:
                        ExtractData_Change_YX(recvArray, ParaType.YX, rtInfo);
                        break;
                    case (byte)ParseType.DS:
                        ExtractData_Change_YX(recvArray, ParaType.DS, rtInfo);
                        break;
                    case (byte)ParseType.YC:
                        ExtractData_Change_YC(recvArray, ParaType.YC, rtInfo);
                        break;
                    case (byte)ParseType.SOE:
                        ExtractData_Change_SOE(recvArray, ParaType.SOE, rtInfo);
                        break;
                    case (byte)ParseType.DSOE:
                        ExtractData_Change_SOE(recvArray, ParaType.DSOE, rtInfo);
                        break;
                    case (byte)ParseType.YK:
                        ExtractData_Change_YK(recvArray, ParaType.YK, rtInfo);
                        break;
                    case (byte)ParseType.DYK:
                        ExtractData_Change_YK(recvArray, ParaType.YK, rtInfo);
                        break;
                    case (byte)ParseType.SNTP:
                        ExtractData_SNTP(recvArray, ParaType.Last, rtInfo);
                        break;
                    case (byte)ParseType.FV:
                        ExtractData_Change_FV(recvArray, ParaType.FV, rtInfo);
                        break;
                    case (byte)ParseType.FILE:    //文件操作
                         ExtractData_FILE(recvArray, 0, rtInfo);
                        break;  
                    case (byte)ParseType.SECTION:
                         ExtractData_FILE(recvArray, 0, rtInfo);
                        break;  
                    case (byte)ParseType.FINALSEG:
                         ExtractData_FILE(recvArray, 0, rtInfo);
                        break;  
                    case (byte)ParseType.SEG:                   
                        ExtractData_SEG(recvArray, 0, rtInfo);
                        break;
                    default:                       
                        break;
                }
                
            }            
            
            IsRecvFinished = true;

            return IsFinished;
        }

        /*
        * 解析遥测数据报文
        * */
        public void ExtractData_Change_YC(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            switch (rtInfo.ASDU.byQS & 0x80)
            {
                case 0:
                    ExtractData_Change_YC_LS(recvArray, type, rtInfo);
                    break;
                case 0x80:
                    ExtractData_Change_YC_LX(recvArray, type, rtInfo);
                    break;
                default:
                    break;
            }
            
        }

        /*
        * 解析连续遥测数据报文
        * */
        public void ExtractData_Change_YC_LX(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            unsafe
            {
                Marshal.Copy(recvArray, dataLen, ptr, sizeof(short));
            }

            short id = (short)Marshal.PtrToStructure(ptr, typeof(short));
            id = (short)(id - 0x4001);
            //id = (short)(id & 0xFFF);
            string s = RecvDataFormat(recvArray);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            dataLen += Marshal.SizeOf(typeof(short)) + Marshal.SizeOf(typeof(Byte));
            len -= dataLen;
            if (len < sizeof(short))
                return;

            while (len > 2)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                //转换ID:4001->0,4002->1....
                unsafe
                {
                    dataLen = Marshal.SizeOf(rtInfo.head) / 2;
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YC_LX rtCell = (APCI_Change_Cell_YC_LX)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YC_LX));

                if (0x80 == (rtCell.valH & 0x80))
                    break ;
                rtReport.id = id;
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);

                id++;
                len -= Marshal.SizeOf(rtInfo.head) / 2;
            }
            isRtDataReflashed = true;

            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
         * 解析离散遥测数据报文
         * */
        public void ExtractData_Change_YC_LS(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(16);
            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, sizeof(short));
            }
            string s = RecvDataFormat(recvArray);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            len -= dataLen;
            if (len < sizeof(short) || len % 6 != 0)
                return;

            while (len > 5)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                //转换ID
                //recvArray[0] = (byte)(recvArray[0] - 1);
                //recvArray[1] = 0;                
                //s = RecvDataFormat(recvArray,rtInfo);
                unsafe
                {
                    dataLen = Marshal.SizeOf(rtInfo.head);
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YC rtCell = (APCI_Change_Cell_YC)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YC));
                
                if (0x80 == (rtCell.idH & 0x80))
                    break ;
                rtReport.id = (short)(rtCell.idL - 0x4001);
                //rtReport.id = (short)((rtCell.idL&0xFFF)-1);
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);
                
                len -= Marshal.SizeOf(rtInfo.head);
            }
            
            isRtDataReflashed = true;

            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
         * 解析不连续遥信数据报文
         * */
        public void ExtractData_Change_YX(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            switch (rtInfo.ASDU.byQS & 0x80)
            {
                case 0:
                    ExtractData_Change_YX_LS(recvArray, type, rtInfo);
                    break;
                case 0x80:
                    ExtractData_Change_YX_LX(recvArray, type, rtInfo);
                    break;
                default:
                    break;
            }
        }

        /*
         * 解析连续遥信数据报文
         * */
        public void ExtractData_Change_YX_LX(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(16);
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            unsafe
            {
                Marshal.Copy(recvArray, dataLen, ptr, sizeof(short));
            }

            short id = (short)Marshal.PtrToStructure(ptr, typeof(short));
            if (rtInfo.ASDU.byType == M_DP_NA_1_SYMBOL)
            {
                id = (short)(id - 0x3001);
            }
            else
            {
                id = (short)((id & 0xFFF) - 1);
            }
            //id = (short)(id & 0xFFF);
            //string s = RecvDataFormat(recvArray,rtInfo);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            dataLen += Marshal.SizeOf(typeof(short)) + Marshal.SizeOf(typeof(Byte));
            len -= dataLen;
            if (len < sizeof(short))
                return;
            while (len > 0)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(Byte));
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YX_LX rtCell = (APCI_Change_Cell_YX_LX)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YX_LX));
                if (0x80 == (rtCell.valL & 0x80))
                    break;
                   
                rtReport.id = id;
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);

                id++;
                len -= Marshal.SizeOf(typeof(Byte));
            }

            isRtDataReflashed = true;
            isFvDataReflashed = false;
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
          * 解析离散遥信数据报文
          * */
        public void ExtractData_Change_YX_LS(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, sizeof(short));
            }

            //string s = RecvDataFormat(recvArray,rtInfo);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            len -= dataLen;
            if (len < sizeof(short) || len % 4 != 0)
                return;
            while (len > 3)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);                
                //转换ID
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(int));
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YX rtCell = (APCI_Change_Cell_YX)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YX));

                if (rtInfo.ASDU.byType == M_DP_NA_1_SYMBOL)
                {
                    rtReport.id = (short)(rtCell.wInfoaddrL - 0x3001);
                }
                else
                {
                    rtReport.id = (short)((rtCell.wInfoaddrL & 0xFFF) - 1);
                }
                //rtReport.id = (short)((rtCell.wInfoaddrL&0xFFF) - 1);
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);

                len -= Marshal.SizeOf(typeof(int));
            }

            isRtDataReflashed = true;
            isFvDataReflashed = false;
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
         * 解析连续遥信数据报文
         * */
        public void ExtractData_Change_SOE_LX(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            unsafe
            {
                Marshal.Copy(recvArray, dataLen, ptr, sizeof(short));
            }

            short id = (short)Marshal.PtrToStructure(ptr, typeof(short));
            if (rtInfo.ASDU.byType == M_DP_NA_1_SYMBOL)
            {
                id = (short)(id - 0x3001);
            }
            else
            {
                id = (short)((id & 0xFFF) - 1);
            }
            //string s = RecvDataFormat(recvArray,rtInfo);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            dataLen += Marshal.SizeOf(typeof(short)) + Marshal.SizeOf(typeof(Byte));
            len -= dataLen;
            if (len < sizeof(short))
                return;
            while (len > 0)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(Byte));
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_SOE rtCell = (APCI_Change_Cell_SOE)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_SOE));
                if (0x80 == (rtCell.valL & 0x80))
                    break;

                rtReport.id = id;
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                //rtReport.dateTime = rtCell.datetime;
                DateTime dateTime = new DateTime();
                dateTime = dateTime.AddYears(2000 + rtCell.datetime.byYear);
                dateTime = dateTime.AddMonths(rtCell.datetime.byMonth);
                dateTime = dateTime.AddDays(rtCell.datetime.byDay);
                dateTime = dateTime.AddHours(rtCell.datetime.byHour);
                dateTime = dateTime.AddMinutes(rtCell.datetime.byMinute);
                rtReport.dateTime = dateTime.AddMilliseconds(rtCell.datetime.wMsecond);
                rtEventArg.RtDataList.Add(rtReport);//变位时间未加

                id++;
                len -= Marshal.SizeOf(typeof(Byte)) + 7;
            }

            isRtDataReflashed = true;
            isFvDataReflashed = false;
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
          * 解析离散遥信数据报文
          * */
        public void ExtractData_Change_SOE_LS(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            int count = recvArray[7];
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, sizeof(short));
            }
            
            string s = CLinklayerMgr.Instance.RecvDataFormat(recvArray);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            int dataLen = Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(rtInfo.ASDU);
            len -= dataLen;
            if (len < sizeof(short))
                return;
            while (count > 0)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                //转换ID
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(int)) + 7;
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_SOE rtCell = (APCI_Change_Cell_SOE)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_SOE));

                if (rtInfo.ASDU.byType == M_DP_TB_1_SYMBOL)
                {
                    rtReport.id = (short)(rtCell.wInfoaddrL - 0x3001);
                }
                else
                {
                    rtReport.id = (short)((rtCell.wInfoaddrL & 0xFFF) - 1);
                }
                //rtReport.id = (short)((rtCell.wInfoaddrL & 0xFFF) - 1);
                rtReport.type = type;
                rtReport.value = rtCell.valL;
                //rtReport.dateTime = rtCell.datetime;
                DateTime dateTime = new DateTime();
                dateTime = dateTime.AddYears(1999 + rtCell.datetime.byYear);
                dateTime = dateTime.AddMonths(rtCell.datetime.byMonth - 1);
                dateTime = dateTime.AddDays(rtCell.datetime.byDay - 1);
                dateTime = dateTime.AddHours(rtCell.datetime.byHour);
                dateTime = dateTime.AddMinutes(rtCell.datetime.byMinute);
                rtReport.dateTime = dateTime.AddMilliseconds(rtCell.datetime.wMsecond);
                rtEventArg.RtDataList.Add(rtReport);

                len -= Marshal.SizeOf(typeof(int)) + 7;
                count--;
            }

            isRtDataReflashed = true;
            isFvDataReflashed = false;
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }
        /*
       * 解析SOE数据报文
       * */
        public void ExtractData_Change_SOE(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            switch (rtInfo.ASDU.byQS & 0x80)
            {
                case 0:
                    ExtractData_Change_SOE_LS(recvArray, type, rtInfo);
                    break;
                case 0x80:
                    ExtractData_Change_SOE_LX(recvArray, type, rtInfo);
                    break;
                default:
                    break;
            }
        }

       
        /*
         * 格式化发送数据报文
         * */
        private string SendDataFormat(byte[] revArray)
        {
            string formatString = null;

            formatString = "Tx:" + BitConverter.ToString(revArray, 0, revArray.Length).Replace("-", " ");

            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;

            formatString = formatString + " (" + dateTime.Hour + ":" + dateTime.Minute + ":" + dateTime.Second + "."
                   + dateTime.Millisecond + ")";

            return formatString;
        }

        /*
         * 格式化文件传输报文
         * */
        public string RecvDataFormat(byte[] revArray)
        {
            string formatString = null;
            if (revArray == null)
                return formatString;
            formatString = "Rx:" + BitConverter.ToString(revArray, 0, revArray.Length).Replace("-", " ");

            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;

            formatString = formatString + " (" + dateTime.Hour + ":" + dateTime.Minute + ":" + dateTime.Second + "."
                   + dateTime.Millisecond + ")";           

            return formatString;
        }       

       public void ExtractData_SNTP(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);

            string s = RecvDataFormat(recvArray);
            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(typeof(short));
            len -= dataLen;
            if (type == ParaType.Last)
                return;
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            Array.Copy(recvArray, dataLen, recvArray, 0, len);
            unsafe
            {
                dataLen = Marshal.SizeOf(typeof(APCI_SOE_TIME));
                Marshal.Copy(recvArray, 0, ptr, dataLen);
            }
            APCI_SOE_TIME time = (APCI_SOE_TIME)Marshal.PtrToStructure(ptr, typeof(APCI_SOE_TIME));
            DateTime dateTime = new DateTime();
            dateTime = dateTime.AddYears(time.byYear + 1999);
            dateTime = dateTime.AddMonths(time.byMonth - 1);
            dateTime = dateTime.AddDays(time.byDay - 1);
            dateTime = dateTime.AddHours(time.byHour);
            dateTime = dateTime.AddMinutes(time.byMinute);
            dateTime = dateTime.AddMilliseconds(time.wMsecond);
            rtReport.dateTime = dateTime;
            rtEventArg.RtDataList.Add(rtReport);

            isSNTPReflashed = true;
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }
        }

        /*
         * 解析功能定值的帧数据
         * */
        public void ExtractData_Change_FV(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            int tempLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + Marshal.SizeOf(typeof(Byte));
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            unsafe
            {
                Marshal.Copy(recvArray, tempLen, ptr, sizeof(short));
            }
            short flag = (short)Marshal.PtrToStructure(ptr, typeof(short));
            if (flag == 0xFF)
            {
                ExtractData_System(recvArray, rtInfo);
                return;
            }
            else if ((rtInfo.rtASDU.wInfo & 0xFF) == 0x55)
            {
                ExtractData_BoardInfo(recvArray, rtInfo);
                return;
            }
            else if ((rtInfo.rtASDU.wInfo & 0xFF) == 0x56)
            {
                ExtractData_SNTP(recvArray,0, rtInfo);
                return;
            }
           
            string s = RecvDataFormat(recvArray);
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();
            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(typeof(short));
            int fvtype = recvArray[dataLen - 1];
            len -= dataLen;
            if (len <= sizeof(short))
            {
                if(CLinklayerMgr.Instance.GetFVFlag(recvArray))
                {
                    if (MessageBox.Show(SConfigForm.Instance, "确定执行定值设定操作？", "注意", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        CLinklayerMgr.Instance.CallUpFixValueData(device.byAddr,0x54, 0x06, (Byte)FuncFixValue.Instance.fvlist.Count,(Byte)FuncFixValue.Instance.nFvSN);
                    }
                    else
                    {
                        CLinklayerMgr.Instance.CallUpFixValueData(device.byAddr, 0x54, 0x08, (Byte)FuncFixValue.Instance.fvlist.Count, (Byte)FuncFixValue.Instance.nFvSN);
                    }
                }
                else
                {
                    if (CLinklayerMgr.Instance.GetFVFinishFlag(recvArray))
                    {
                        FuncFixValue.Instance.SettingFinish();
                    }
                    MessageBox.Show(SConfigForm.Instance, CLinklayerMgr.Instance.GetFVMsg(recvArray));
                }
                return;
            }

            while (len > 3)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                unsafe
                {
                    dataLen = Marshal.SizeOf(rtInfo.head.ctlreg);
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_FV rtCell = (APCI_Change_Cell_FV)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_FV));
                rtReport.id = rtCell.byFvCode;
                rtReport.type = type;
                rtReport.value = rtCell.wValue;
                rtEventArg.RtDataList.Add(rtReport);

                dataLen = Marshal.SizeOf(rtInfo.head.ctlreg);
                len -= dataLen;
                
            }

            if (flag == 0xFF)
            {
                isSystemReflashed = true;
            }
            else
            {
                if (FuncFixValue.Instance.nFvSN == fvtype)
                {
                    IsFvDataReflashed = true;
                    isRtDataReflashed = false;
                }
            }
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }

        }

        /*
        * 解析连续的帧数据
        * */
        public void ExtractData_Change_YK(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            string s = RecvDataFormat(recvArray);

            YKOperation(rtInfo, recvArray);
        }

        /*
        * 解析文件传输的帧数据
        * */
        public int filelen = 0;//文件内容大小
        public short wfileName = 0;
        public void ExtractData_FILE(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            int nfilelen = 0;           
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);          

            string s = RecvDataFormat(recvArray);
            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU)+ 3;//减去头+数据单元+信息体地址
            len -= dataLen;
            
            Array.Copy(recvArray, dataLen, recvArray, 0, len);
            unsafe
            {
                dataLen = Marshal.SizeOf(typeof(short));
                Marshal.Copy(recvArray, 0, ptr, dataLen);
            }
            short fileName = (short)Marshal.PtrToStructure(ptr, typeof(short));
            wfileName = (short)((fileName&0xFF)&0x0F);
            
            if (rtInfo.ASDU.byType == (byte)ParseType.FILE)
            {
                len -= dataLen;
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
            }
            else
            {
                len -= 3;
                Array.Copy(recvArray, 3, recvArray, 0, len);
            }

            unsafe
            {
                dataLen = Marshal.SizeOf(typeof(int));
                Marshal.Copy(recvArray, 0, ptr, dataLen);
            }
            nfilelen = (int)Marshal.PtrToStructure(ptr, typeof(int));            

            if (rtInfo.ASDU.byType == (byte)ParseType.SECTION)
            {
                filelen = nfilelen;
                if (nfilelen == 0)
                {
                    MessageBox.Show(SConfigForm.Instance, "文件为空!");
                    return;
                }
                dataArray = new byte[nfilelen + 4];
                Array.Copy(recvArray, 0, dataArray, 0, 4);
                FileInfoForm fileInfo = new FileInfoForm(wfileName.ToString(), wfileName, nfilelen);
                fileInfo.ShowDialog(SConfigForm.Instance);
            }          

            if ((ParseType)rtInfo.ASDU.byType == ParseType.FINALSEG)
            {
                if (packnum != (filelen - 1) / 230 + 1) 
                {
                    isFinallyArrived = true;
                    crcnum = recvArray[len - 1];
                    return;
                }
                else
                {
                    packnum = 0;
                }

                DocumentMgr.Instance.WriteFile(wfileName.ToString(), dataArray);//写文件               
                if (recvArray[len - 1] != CLinklayerMgr.Instance.GetCRCCode())
                {
                    MessageBox.Show(SConfigForm.Instance,"校验码错误!");
                }
                else
                {
                    string sendString = "文件生成成功！";
                    DataPackage revDataObj = new DataPackage((Byte)3, sendString);
                    lock (ASDUDataMgr.Locker)
                    {
                        ASDUDataMgr.Instance.RevBufList.Add(revDataObj);
                    }
                    MessageBox.Show(SConfigForm.Instance,"文件召唤成功！");
                }
            }
            else
            {
                return;
            }

            Marshal.FreeHGlobal(ptr);
        }

        public void ExtractData_SEG(byte[] recvArray, ParaType type, APCIDataInfo rtInfo)
        {
            packnum++; //计算文件段或者节包数目
            if (filelen == 0) return;
            int len = recvArray.Length;
            int nfilelen = 0;
            ArrayList arrlist = new ArrayList();
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);

            string s = RecvDataFormat(recvArray);
            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + 3;//减去头+数据单元+信息体地址
            len -= dataLen;

            Array.Copy(recvArray, dataLen, recvArray, 0, len);
            Byte fileName = (Byte)(recvArray[0]&0x0F);
            if(fileName != wfileName)
            {
                MessageBox.Show(SConfigForm.Instance,"文件名不正确!");
            }

            unsafe
            {
                dataLen = Marshal.SizeOf(typeof(short));
                Marshal.Copy(recvArray, 1, ptr, dataLen);
            }
            short packNo = (short)Marshal.PtrToStructure(ptr, typeof(short));

            len -= 3;
            Array.Copy(recvArray, 3, recvArray, 0, len);

            unsafe
            {
                dataLen = Marshal.SizeOf(typeof(Byte));
                Marshal.Copy(recvArray, 0, ptr, dataLen);
            }
            nfilelen = (Byte)Marshal.PtrToStructure(ptr, typeof(Byte));
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);

            byte[] fileArrary = new byte[nfilelen];
            Array.Copy(recvArray, 0, fileArrary, 0, nfilelen);//数据，除掉文件长度四个字节后的所有数据           
            
            InsertRecvArray(fileArrary, packNo);   //开辟内存空间，往里面填充数据        

            if (packnum == (filelen-1)/230 + 1)
            {
                SetCRCSUM(dataArray);                

                if (isFinallyArrived)
                {
                    DocumentMgr.Instance.WriteFile(wfileName.ToString(), dataArray);//写文件  
                    if (crcnum != CLinklayerMgr.Instance.GetCRCCode())
                    {
                        MessageBox.Show(SConfigForm.Instance,"校验码错误!");
                    }
                    else
                    {
                        string sendString = "文件生成成功！";
                        DataPackage revDataObj = new DataPackage((Byte)3, sendString);
                        lock (ASDUDataMgr.Locker)
                        {
                            ASDUDataMgr.Instance.RevBufList.Add(revDataObj);
                        }
                        MessageBox.Show(SConfigForm.Instance,"文件召唤成功！");
                    }
                    crcnum = 0;
                    isFinallyArrived = false;
                }
            }

            Marshal.FreeHGlobal(ptr);
        }

        public int packnum = 0;
        private Byte crcnum = 0;
        private bool isFinallyArrived = false;
        private void InsertRecvArray(byte[]recvArray,short num)
        {            
            int len = recvArray.Length;
            int nPos = 0;
            if (len == 0) return;
            if (len == 0xE6)
            {
                if (num == 0)
                {
                    nPos = 4;
                }
                else
                {
                    nPos = num * len + 4;
                } 
            }
            else
            {
                nPos = num * 0xE6 + 4;
            }
            
            Array.Copy(recvArray, 0, dataArray, nPos, recvArray.Length);
        }               

        public override void SendStopCMD()
        {
            base.SendStopCMD();
            if (!rtClient.IsConnected())
            {
                OnStopRtLink();
            }

            APCIFrameHead rtDataInfo = new APCIFrameHead();
            rtDataInfo.symbol = START_SYMBOL;
            rtDataInfo.length = (byte)Marshal.SizeOf(rtDataInfo.ctlreg);
            rtDataInfo.ctlreg.wctlSN = APCI_STOPDT_Act;            
            RevBufList.Clear();
            CLinklayerMgr.Instance.SendCMD(rtDataInfo);            
            CLinklayerMgr.Instance.ResetSN();
        }

        public byte[] columnArray = null;
        public byte[] dataArray = null;
        public short recordCount = 0;
        public short measCount = 0;
        public void ParseFileData(byte[] recvArray)
        {
            int len = recvArray.Length;
            if (len == 0) return;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);

            int dataLen = Marshal.SizeOf(typeof(short));
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);

            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, dataLen);
            }
            short month = (short)Marshal.PtrToStructure(ptr, typeof(short));
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);

            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, sizeof(short));
            }
            recordCount = (short)Marshal.PtrToStructure(ptr, typeof(short));//记录条数
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);

            unsafe
            {
                Marshal.Copy(recvArray, 0, ptr, sizeof(short));
            }
            measCount = (short)Marshal.PtrToStructure(ptr, typeof(short));//测点条数

            columnArray = new byte[measCount * sizeof(short)];
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);
            Array.Copy(recvArray, 0, columnArray, 0, measCount * sizeof(short));
            
            dataLen = measCount * sizeof(short);
            len -= dataLen;
            Array.Copy(recvArray, dataLen, recvArray, 0, len);
            Array.Copy(recvArray, 0, dataArray, 0, recvArray.Length);
        }        

        private void SetCRCSUM(byte[] recvArray)
        {
            CLinklayerMgr.Instance.CleanCRCCode();
            for (int i = 4; i < recvArray.Length; i++)
            {
                CLinklayerMgr.Instance.SetCRCCode(dataArray[i]);
            }           
        }

        /*
         *遥控下发和执行
         */
        public void CallupYKlist(ushort infoaddr,Byte type,short cot,Byte value)
        {
            APCIDataInfo rtDataInfo = new APCIDataInfo();
            rtDataInfo.head.symbol = START_SYMBOL; 
            rtDataInfo.head.ctlreg.wctlSN = CLinklayerMgr.Instance.nSendSN;
            rtDataInfo.head.ctlreg.wctlRN = CLinklayerMgr.Instance.nRecvSN;
            rtDataInfo.head.length = (byte)(Marshal.SizeOf(rtDataInfo.head.ctlreg) + Marshal.SizeOf(rtDataInfo.rtASDU) + Marshal.SizeOf(rtDataInfo.ASDU)-Marshal.SizeOf(typeof(short)));
            rtDataInfo.ASDU.byType = type;
            rtDataInfo.ASDU.byQS = 1;
            rtDataInfo.ASDU.wCOT = cot;
            rtDataInfo.ASDU.wCommAddr = iec_type.wCommAddr;
            rtDataInfo.rtASDU.wInfoAddr = infoaddr;
            rtDataInfo.rtASDU.byDataIndex = value;
            CLinklayerMgr.Instance.SendCMD(rtDataInfo);
            CLinklayerMgr.Instance.SetSendConfirmCount(CLinklayerMgr.Instance.nRecvSN);
            CLinklayerMgr.Instance.SetNotifySendSN();
        }

        private void YKOperation(APCIDataInfo rtInfo,byte[] recvArray)
        {
            if (rtInfo.ASDU.byType == C_SC_NA_1_SYMBOL)
            {
                if (CLinklayerMgr.Instance.GetYKFlag(recvArray))
                {
                    if (MessageBox.Show(SConfigForm.Instance,"确定执行遥控操作？", "注意", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        if ((recvArray[recvArray.Length - 2]&0x01) == 1)
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_SC_NA_1_SYMBOL, 6, 0x01);
                        }
                        else
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_SC_NA_1_SYMBOL, 6, 0x00);
                        }
                    }
                    else
                    {
                        if ((recvArray[recvArray.Length - 2]&0x2) == 1)
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_SC_NA_1_SYMBOL, 8, 0x01);
                        }
                        else
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_SC_NA_1_SYMBOL, 8, 0x00);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(SConfigForm.Instance, CLinklayerMgr.Instance.GetYKMsg(recvArray), Global.GetString("信息", "Information"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
            }
            else
            {
                if (CLinklayerMgr.Instance.GetYKFlag(recvArray))
                {
                    if (MessageBox.Show(SConfigForm.Instance,"确定执行遥控操作？", "注意", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        if (recvArray[recvArray.Length - 2] == 0x81)
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_DC_NA_1_SYMBOL, 6, 0x81);
                        }
                        else
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_DC_NA_1_SYMBOL, 6, 0x80);
                        }
                    }
                    else
                    {
                        if (recvArray[recvArray.Length - 2] == 0x81)
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_DC_NA_1_SYMBOL, 8, 0x81);
                        }
                        else
                        {
                            CallupYKlist(rtInfo.rtASDU.wInfoAddr, C_DC_NA_1_SYMBOL, 8, 0x82);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(SConfigForm.Instance,CLinklayerMgr.Instance.GetYKMsg(recvArray));
                }
            }
        }

        private void ExtractData_System(byte[] recvArray, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            short id = 0;

            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(typeof(short));
            len -= dataLen;
            string s = RecvDataFormat(recvArray);
            if (len <= sizeof(short))
            {                
                return;
            }
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();

            while (len > 0)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(Byte));
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YX_LX rtCell = (APCI_Change_Cell_YX_LX)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YX_LX));
                rtReport.id = id;
                rtReport.type = 0;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);
                id++;
                len -= dataLen;

            }           
            isSystemReflashed = true;
           
            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }

        }

        private void ExtractData_BoardInfo(byte[] recvArray, APCIDataInfo rtInfo)
        {
            int len = recvArray.Length;
            IntPtr ptr = Marshal.AllocHGlobal(recvArray.Length);
            short id = 0;

            int dataLen = Marshal.SizeOf(rtInfo.head) + Marshal.SizeOf(rtInfo.ASDU) + Marshal.SizeOf(rtInfo.rtASDU) + Marshal.SizeOf(typeof(short));
            len -= dataLen;
            string s = RecvDataFormat(recvArray);
            if (len <= sizeof(short))
            {
                return;
            }
            RTData_Report rtReport = new RTData_Report();
            RtDataEventArgs rtEventArg = new RtDataEventArgs();

            while (len > 0)
            {
                Array.Copy(recvArray, dataLen, recvArray, 0, len);
                unsafe
                {
                    dataLen = Marshal.SizeOf(typeof(Byte));
                    Marshal.Copy(recvArray, 0, ptr, dataLen);
                }
                APCI_Change_Cell_YX_LX rtCell = (APCI_Change_Cell_YX_LX)Marshal.PtrToStructure(ptr, typeof(APCI_Change_Cell_YX_LX));
                rtReport.id = id;
                rtReport.type = 0;
                rtReport.value = rtCell.valL;
                rtEventArg.RtDataList.Add(rtReport);
                id++;
                len -= dataLen;

            }
            isVersionReflashed = true;

            Marshal.FreeHGlobal(ptr);

            if (RtDataReportEvent != null)
            {
                RtDataReportEvent(this, rtEventArg);
            }

        }         
        //End Region
    }
}
