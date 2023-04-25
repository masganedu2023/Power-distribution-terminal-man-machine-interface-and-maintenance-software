using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Power2000.General.CommTools;

namespace Power2000.CommuCfg
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCI_DATA_S         //处理遥信、遥测
    {
        public ushort wInfoAddr;			// 信息体地址
        public Byte byType;				    // 总召或分组召唤
        public Byte byCrc;				    // 校验码
        public Byte byEnd;			        // 结束标志
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIDataInfo_S           //处理遥信、遥测
    {
        public APCI_ASDU_S ASDU;            // 数据单元
        public APCI_DATA_S DATA;              // 数据信息
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIFileInfo_S           //处理遥信、遥测
    {
        public APCI_ASDU_S ASDU;            // 数据单元
        public APCI_ASDU_FILE_S DATA;              // 数据信息
    }

    public struct APCIFVInfo_S
    {
        public APCI_ASDU_S ASDU;               // 数据单元
        public APCI_ASDU_FV_S DATA;          //信息体
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCI_ASDU_SNTP_S
    {
        public short wInfoAddr;			     // 信息体地址
        public APCI_SOE_TIME time;           // 年月日时分毫秒
        public Byte byCrc;				    // 校验码
        public Byte byEnd;			        // 结束标志
    }

    public struct APCISNTPInfo_S
    {
        public APCI_ASDU_S ASDU;               // 数据单元
        public APCI_ASDU_SNTP_S rtASDU;          //信息体
    }

    public struct APCISPANInfo_S
    {
        public APCI_ASDU_S ASDU;               // 数据单元
        public APCI_ASDU_SPAN_S rtASDU;          //信息体
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct APCIDSPInfo_S
    {
        public APCI_ASDU_S ASDU;               // 数据单元
        public APCI_ASDU_DSP_S rtASDU;          //信息体
    }
    //end

    public class C101LinklayerMgr : PtlmoduleMgr
    {
        public bool m_bIsResponse = false;					    // 是否回复报文      
        public int m_SConferm_Timer;					// S型确认帧发送倒数计时器
        public int m_UTest_Timer;						// U型测试帧发送倒数计时器
        public int m_UCon_TimeOff;						// U型帧测试确认倒数计时器

        //public event RtDataReportEventHandler RtDataReportEvent;
        //public event LinkEventHandler LinkEvent;

        private static C101LinklayerMgr instance;
        public static C101LinklayerMgr Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new C101LinklayerMgr();
                }
                return instance;
            }
        }
             

        public void RebuiltLinklayer()
        {
            InitialLinkLayer();
        }

        /*
        * 发送短帧
        * */
        public void SendShortFrame(Byte control)
        {
            byte frameLen = (byte)(4 + CommonSetting.ProtocolSetting.wLinkAddrLen);
            byte[] shortFrame = new byte[frameLen];

            shortFrame[0] = SHORT_SYMBOL;
            shortFrame[1] = control;
            shortFrame[2] = (byte)(CommonSetting.ProtocolSetting.wLinkaddr & 0x00FF);
            shortFrame[frameLen - 1] = END_SYMBOL;
            shortFrame[frameLen - 2] = (byte)(shortFrame[1] + shortFrame[2]);
            if(CommonSetting.ProtocolSetting.wLinkAddrLen == 2)
            {
                shortFrame[3] = (byte)((CommonSetting.ProtocolSetting.wLinkaddr >> 8) & 0x00FF);
                shortFrame[frameLen - 2] += shortFrame[3];
            }

            SendFrameToSerial(shortFrame);

            SConfigForm.Instance.t2 = 0;
            m_bIsResponse = true;
        }

        /*
         * 发送总召操作召唤
         * */
        //public void SendFrame_CallAll(Byte control,Byte byqs, Byte cot,Byte callType)
        public void SendFrame_CallAll()
        {
            APCIFrameHead_101 header = new APCIFrameHead_101();

            //FormatSendFrameHeader_101(ref header);
            header.Symbol = header.symbolStart = START_SYMBOL;
            header.length = 0;

            if (CommonSetting.ProtocolSetting.byBanlance == 1)
            {
                if (CommonSetting.ProtocolSetting.byBanDirection == 1)
                {
                    header.ctrlreg = 0xC4;
                }
                else
                {
                    header.ctrlreg = 0x43;
                }
            }
            else
            {
                header.ctrlreg = 0x43;
            }

            //APCIFrameHead_101 uFrameInfo = new APCIFrameHead_101();
            //uFrameInfo.Symbol = START_SYMBOL;
            //uFrameInfo.length = 0x09;
            //uFrameInfo.frameLen = uFrameInfo.length ;
            //uFrameInfo.symbolStart = START_SYMBOL;
            //uFrameInfo.ctrlreg = control;

            byte[] sendByteArray = new byte[bufLen];
            byte msgLen = FormatSendDataASDUHeader(ref sendByteArray, SIYAO_SYMBOL, 1, (byte)COTType.Active);
            msgLen = FormatSendDataInfoAddr(ref sendByteArray, msgLen, 0);
            sendByteArray[msgLen++] = 20;

            SendFrame(header, sendByteArray, msgLen);
        }

        /*
         * 发送命令给下位机，公用接口
         * */
        public void SendFrame(APCIFrameHead_101 head, byte[] asduBytes, int nLen)
        {
            head.length = head.frameLen = (byte)(1 + nLen + CommonSetting.ProtocolSetting.wLinkAddrLen); // 控制域+链路地址+ASDU长度
            int headLen = Marshal.SizeOf(head);
            IntPtr sendBuf = Marshal.AllocCoTaskMem(headLen);
            if (sendBuf == IntPtr.Zero)
                return;

            Marshal.StructureToPtr(head, sendBuf, false);
            
            //byte[] sendByteArray = new byte[bufLen];
            byte[] sendArr = new byte[headLen + CommonSetting.ProtocolSetting.wLinkAddrLen + nLen + 2];

            int pos = 0;
            for (; pos < headLen; pos++)
            {
                sendArr[pos] = Marshal.ReadByte(sendBuf, pos);
            }
            //Array.Copy(sendByteArray, 0, sendArr, 0, headLen);
            sendArr[pos++] = Utility.GetByte_L(CommonSetting.ProtocolSetting.wLinkaddr);
            if (CommonSetting.ProtocolSetting.wLinkAddrLen == 2)
            {
                sendArr[pos++] = Utility.GetByte_H(CommonSetting.ProtocolSetting.wLinkaddr);
            }
            
            if (nLen > 0)
            {
                Array.Copy(asduBytes, 0, sendArr, pos, nLen);
            }
            sendArr[sendArr.Length - 2] = GetCRCCode(sendArr, headLen - 1, sendArr.Length - 3);
            sendArr[sendArr.Length - 1] = END_SYMBOL;

            SendFrameToSerial(sendArr);
        }

        public void SendFrameToSerial(byte[] frameArray)
        {
            string s = SendDataFormat(frameArray);
            if (SConfigForm.Instance.IsOpenLog)
            {
                Log.FileName = DocumentMgr.Instance.LogFilePath + "配网终端收发报文.log";
                Log.Report(LogLevel.Info, "", s);
            }

            ShowSendInfo(frameArray);
            try
            {
                serialClient.BeginSend(frameArray, frameArray.Length);
                serialClient.BeginReceive(1000);
            }
            catch (SocketException e)
            {
                Log.Report(LogLevel.Exception, "配网自动化终端综合维护软件", String.Format("Socket异常 错误码：{0}", e.ErrorCode));
            }
        }

        public byte GetCRCCode(byte[] bytesArray, int startIndex, int endIndex)
        {
            byte crc = 0;

            for (int index = startIndex; index <= endIndex; index++ )
            {
                crc += bytesArray[index];
            }

            return crc;
        }

        /*
         * 链路初始化
         * */
        public void InitialLinkLayer()
        {
            if (CommonSetting.ProtocolSetting.byBanlance == 0)
            {
                SendShortFrame(0x49);
            }
            else
            {
                if (iec_type.byBanDirection == 0)
                {
                    SendShortFrame(0x49);
                }
                else
                {
                    SendShortFrame(0xC9);
                }
            }
            IsAllCallUpFinished = true;
        }

        public byte RecvControl = 0;
        /*
        *接收数据处理
        */
        //public Byte CtrlCode = 0;
        public bool isReset = false;//复位链路确认标志
        public override bool OnRecvData(byte[] recvArray)
        {
            IsRecvFinished = false;
            m_bIsResponse = false;
            SConfigForm.Instance.t3 = 0;
            //int dataLen = 0;
            int nLength = recvArray.Length;
            //IntPtr ptr = Marshal.AllocHGlobal(RecvData.Length);
            if (recvArray[0] == SHORT_SYMBOL)
            {
                if (nLength < 4 + CommonSetting.ProtocolSetting.wLinkAddrLen)
                {
                    return false;
                }
            }
            else if (recvArray[0] == START_SYMBOL && recvArray[3] == START_SYMBOL && recvArray[1] == recvArray[2])
            {
                if (nLength < recvArray[1] + 6)
                {
                    return false;
                }
                  
                //unsafe
                //{
                    //Marshal.Copy(RecvData, 0, ptr, sizeof(APCIFrameHead_S));
                //}
            }
            else
            {
                return false;
            }

            if (SConfigForm.Instance.IsOpenLog)
            {
                string s = RecvDataFormat(recvArray);
                Log.FileName = DocumentMgr.Instance.LogFilePath + "配网终端收发报文.log";
                Log.Report(LogLevel.Info, "", s);
            }
            ShowRecvInfo(recvArray);

            RecvControl = recvArray[4];
            if(recvArray[0] == SHORT_SYMBOL)
            {
                if (CommonSetting.ProtocolSetting.byBanlance == 0)
                {
                    DealWithUnBalanceShortFrame(recvArray);
                }
                else
                {
                    DealWithBalanceShortFrame(recvArray[1]);
                }
            }
            else
            {
                IsRecvFinished = true;
                byte control = recvArray[4];
                if ((control & 0x80) == 0x80)
                {
                    SendShortFrame(0x00);
                }
                else
                {
                    SendShortFrame(0x80);
                }
                SerialPortDataMgr.Instance.ParseFrame(recvArray);
            }

            return true;
            //APCIFrameHead_S head = (APCIFrameHead_S)Marshal.PtrToStructure(ptr, typeof(APCIFrameHead_S));
            //Marshal.FreeHGlobal(ptr);

            //if (head.Symbol == 0x68)
            //{
            //    CtrlCode = head.ctrlreg;
            //    dataLen = head.length + Marshal.SizeOf(typeof(APCIFrameHead_S));
            //}
            //else if(head.Symbol == 0x10)
            //{
            //    dataLen = Marshal.SizeOf(typeof(APCIFrameHead_S)) - 1;
            //}
            //else
            //{
            //    return false ;
            //}
            //byte[] recvArray = new byte[dataLen];
            //string s = RecvDataFormat(RecvData);
            //if (dataLen > nLength)
                //return false ;
            //Array.Copy(RecvData, 0, recvArray, 0, dataLen);

            //if (SConfigForm.Instance.IsOpenLog)
            //{
            //    Log.FileName = DocumentMgr.Instance.LogFilePath + "配网终端收发报文.log";
            //    Log.Report(LogLevel.Info, "", s);
            //}

            //ShowRecvInfo(recvArray);

            //if (IsAPCIShortOrLong(head))
            //{
            //    if (CommonSetting.ProtocolSetting.byBanlance == 0)
            //    {
            //        DealWithShortFrame(head);
            //    }
            //    else
            //    {
            //        ManageBanlanceShortFrame(head.length);
            //    }
            //    return IsRecvFinished;
            //}
            //else
            //{             
            //}

            //return true;
        }

        /*
        * 判断是否是短帧
        */
        //public bool IsAPCIShortOrLong(APCIFrameHead_S head)
        //{
        //    bool isUFrame = false;
        //   if(head.Symbol == SHORT_SYMBOL)
        //   {
        //        isUFrame = true;
        //   }
        //   else 
        //   {
        //       isUFrame = false;
        //   }          

        //    return isUFrame;
        //}

        /*
        * 序列化发送数据
        */
        public override string SendDataFormat(byte[] revArray)
        {
            string formatString = null;
            string strDesc = null;
            if (revArray == null)
                return null;

            formatString = "Tx:" + BitConverter.ToString(revArray, 0, revArray.Length).Replace("-", " ");

            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;

            int fixedFrmLen = 4 + CommonSetting.ProtocolSetting.wLinkAddrLen;

            if (revArray.Length <= fixedFrmLen)
            {
                strDesc += "   固定帧:";
            }
            else
            {
                strDesc += "   可变帧:";
                byte type = revArray[CommonSetting.Frame_Pos_Symbol_101];
                switch (type)
                {
                    case (byte)ParseType.GYC:
                    case (byte)ParseType.BYC:
                    case (byte)ParseType.YC:
                        strDesc += "遥测操作";
                        break;
                    case (byte)ParseType.YX:
                    case (byte)ParseType.DS:
                        strDesc += "遥信操作";
                        break;
                    case (byte)ParseType.YK:
                        strDesc += "遥控操作";
                        break;
                    case (byte)NL_GET_SOE_SYMBOL:
                    case (byte)ParseType.DSOE:
                        strDesc += "SOE操作";
                        break;
                    case M_FT_NA_1_SYMBOL:
                        strDesc += "故障事件";
                        break;
                    case C_IC_NA_1_SYMBOL:
                    case M_IT_NB_I_SYMBOL:
                    case M_IT_TC_I_SYMBOL:
                        strDesc += "遥脉操作";
                        break;
                    case (byte)ParseType.SNTP:
                        strDesc += "对时操作";
                        break;
                    case (byte)ParseType.FV:

                    case C_SR_NA_1_SYMBOL:
                    case C_RR_NA_1_SYMBOL:
                    case C_RS_NA_1_SYMBOL:
                    case C_WS_NA_1_SYMBOL:
                        strDesc += "定值操作";
                        break;
                    case (byte)ParseType.FILESEL:
                    case (byte)ParseType.FILE:
                    case (byte)ParseType.SEG:
                    case (byte)ParseType.SECTION:
                    case (byte)ParseType.FINALSEG:

                    case F_FR_NA_1_SYMBOL:
                        strDesc += "文件操作";
                        break;
                    case (byte)ParseType.All:
                        strDesc += "总召操作";
                        break;
                    /*case (byte)ParseType.SPAN:
                        strDesc += "量程设置";
                        break;
                    case (byte)ParseType.MONITOR:
                        strDesc += "监视报文";
                        break;*/
                    case (byte)ParseType.INIT:
                        break;
                    case (byte)ParseType.TEST:
                        strDesc += "测试命令";
                        break;


                    ///////////维护标识符
                    case NL_GET_NETPORTSETTINGG:
                        strDesc += "网口参数";
                        break;
                    case NL_WRITE_NETPORTSETTINGG:
                        strDesc += "网口参数";
                        break;
                    case NL_GET_SERIALPORTSETTING:
                        strDesc += "串口参数";
                        break;
                    case NL_WRITE_SERIALPORTSETTING:
                        strDesc += "串口参数";
                        break;
                    case NL_GET_TERMINALINFO:
                        strDesc += "终端信息";
                        break;
                    case NL_GET_HISTORYRECORD:
                        strDesc += "历史记录";
                        break;
                    case NL_RESTART_TERMINAL:
                        strDesc += "软重启";
                        break;
                    case NL_GET_YCADJUSTSET:
                        strDesc += "校准系数";
                        break;
                    case NL_SET_YCADJUSTSET:
                        strDesc += "校准系数";
                        break;
                    case NL_GET_DSSETTING:
                        strDesc += "双点设置";
                        break;
                    case NL_SET_DSSETTING:
                        strDesc += "双点设置";
                        break;
                    case NL_EVENT_RESET:
                        strDesc += "事件复归";
                        break;
                    case NL_EVENT_CLEAR:
                        strDesc += "清除记录";
                        break;
                    case NL_EVENT_DEFAULT:
                        strDesc += "默认参数";
                        break;
                    case NL_SET_VIRTUALVALUE:
                    case NL_SET_VIRTUALCMD:
                        strDesc += "置数操作";
                        break;
                    case NL_WAVE_START:
                        strDesc += "启动录波";
                        break;
                    case NL_PARACONF_READ:
                        strDesc += "参数配置";
                        break;
                    case NL_PARACONF_WRITE:
                        strDesc += "参数配置";
                        break;
                    default:
                        strDesc += "其他操作";
                        break;
                }
            }

            formatString += Environment.NewLine + strDesc;
            formatString = formatString + " (" + dateTime.Hour + ":" + dateTime.Minute + ":" + dateTime.Second + "."
                   + dateTime.Millisecond + ")";

            return formatString;
        }

        /* *  
        * 序列化发送数据四遥信息数据报文，并解析报文
        * */
        public override string RecvDataFormat(byte[] revArray)
        {
            string formatString = "";
            string strDesc = "";
            if (revArray == null)
                return null;

            formatString = "Rx:" + BitConverter.ToString(revArray, 0, revArray.Length).Replace("-", " ");

            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;

            int fixedFrmLen = 4 + CommonSetting.ProtocolSetting.wLinkAddrLen;

            if(revArray.Length <= fixedFrmLen)
            {
                strDesc += "   固定帧:";
            }
            else
            {
                strDesc += "   可变帧:";
                byte type = revArray[CommonSetting.Frame_Pos_Symbol_101];
                switch (type)
                {
                    case (byte)ParseType.GYC:
                    case (byte)ParseType.BYC:
                    case (byte)ParseType.YC:
                        strDesc += "遥测操作";
                        break;
                    case (byte)NL_GET_SOE_SYMBOL:
                    case (byte)ParseType.DS:
                        strDesc += "遥信操作";
                        break;
                    case (byte)ParseType.YK:
                        strDesc += "遥控操作";
                        break;
                    case (byte)ParseType.SOE:
                    case (byte)ParseType.DSOE:
                        strDesc += "SOE操作";
                        break;
                    case M_FT_NA_1_SYMBOL:
                        strDesc += "故障事件";
                        break;
                    case C_IC_NA_1_SYMBOL:
                    case M_IT_NB_I_SYMBOL:
                    case M_IT_TC_I_SYMBOL:
                        strDesc += "遥脉操作";
                        break;
                    case (byte)ParseType.SNTP:
                        strDesc += "对时操作";
                        break;
                    case (byte)ParseType.FV:

                    case C_SR_NA_1_SYMBOL:
                    case C_RR_NA_1_SYMBOL:
                    case C_RS_NA_1_SYMBOL:
                    case C_WS_NA_1_SYMBOL:
                        strDesc += "定值操作";
                        break;
                    case (byte)ParseType.FILESEL:
                    case (byte)ParseType.FILE:
                    case (byte)ParseType.SEG:
                    case (byte)ParseType.SECTION:
                    case (byte)ParseType.FINALSEG:

                    case F_FR_NA_1_SYMBOL:
                        strDesc += "文件操作";
                        break;
                    case (byte)ParseType.All:
                        strDesc += "总召操作";
                        break;
                    /*case (byte)ParseType.SPAN:
                        strDesc += "量程设置";
                        break;
                    case (byte)ParseType.MONITOR:
                        strDesc += "监视报文";
                        break;*/
                    case (byte)ParseType.INIT:
                        break;
                    case (byte)ParseType.TEST:
                        strDesc += "测试命令";
                        break;


                    ///////////维护标识符
                    case NL_GET_NETPORTSETTINGG:
                        strDesc += "网口参数";
                        break;
                    case NL_WRITE_NETPORTSETTINGG:
                        strDesc += "网口参数";
                        break;
                    case NL_GET_SERIALPORTSETTING:
                        strDesc += "串口参数";
                        break;
                    case NL_WRITE_SERIALPORTSETTING:
                        strDesc += "串口参数";
                        break;
                    case NL_GET_TERMINALINFO:
                        strDesc += "终端信息";
                        break;
                    case NL_GET_HISTORYRECORD:
                        strDesc += "历史记录";
                        break;
                    case NL_RESTART_TERMINAL:
                        strDesc += "软重启";
                        break;
                    case NL_GET_YCADJUSTSET:
                        strDesc += "校准系数";
                        break;
                    case NL_SET_YCADJUSTSET:
                        strDesc += "校准系数";
                        break;
                    case NL_GET_DSSETTING:
                        strDesc += "双点设置";
                        break;
                    case NL_SET_DSSETTING:
                        strDesc += "双点设置";
                        break;
                    case NL_EVENT_RESET:
                        strDesc += "事件复归";
                        break;
                    case NL_EVENT_CLEAR:
                        strDesc += "清除记录";
                        break;
                    case NL_EVENT_DEFAULT:
                        strDesc += "默认参数";
                        break;
                    case NL_SET_VIRTUALVALUE:
                    case NL_SET_VIRTUALCMD:
                        strDesc += "置数操作";
                        break;
                    case NL_WAVE_START:
                        strDesc += "启动录波";
                        break;
                    case NL_PARACONF_READ:
                        strDesc += "参数配置";
                        break;
                    case NL_PARACONF_WRITE:
                        strDesc += "参数配置";
                        break;
                    default:
                        strDesc += "其他操作";
                        break;
                }
            }

            formatString += Environment.NewLine + strDesc;
            formatString = formatString + " (" + dateTime.Hour + ":" + dateTime.Minute + ":" + dateTime.Second + "."
                   + dateTime.Millisecond + ")";            

            return formatString;
        }

        /*
     * 将发送报文加入到报文监视窗口
     * */
        public override void ShowSendInfo(byte[] recvArray)
        {
            String recvString = "";
            recvString = SendDataFormat(recvArray);

            DataPackage revDataObj = new DataPackage(0, recvString);
            lock (Locker)
            {
                RevBufList.Add(revDataObj);
            }
        }

        /*
      * 将接受报文加入到报文监视窗口
      * */
        public override void ShowRecvInfo(byte[] recvArray)
        {
            String recvString = "";

            //获取接收帧的信息
            recvString = RecvDataFormat(recvArray);

            DataPackage revDataObj = new DataPackage(FrameDirection.RX_PORT, recvString);
            lock (Locker)
            {
                RevBufList.Add(revDataObj);
            }
        }

        /*
        * 短帧处理
        */
        public bool isRecvFlag = false;//判断先一次发送FCB是否取反
        public bool IsCallAllEnd = false;
        private void DealWithUnBalanceShortFrame(byte[] recvArray)
        {
            byte ctrlCode = recvArray[1];
            if (IsAllCallUpFinished)
            {
                SendShortFrame(0x40);
                isReset = true;
                IsAllCallUpFinished = false;
            }
            else
            {
                if (isReset)
                {
                    //SendFrame_CallAll(0x53, 0x01, 0x06, 0x14);
                    SendFrame_CallAll();
                    isReset = false;
                }
                else
                {
                    if ((ctrlCode & 0x20) != 0)//功能码ACD为1有一级数据
                    {
                        SendFirstData();
                    }
                    else
                    {
                        if (!IsCallAllEnd)
                        {
                            SendSecondData();
                        }
                        else
                        {
                            switch (ctrlCode & 0x0F)//控制码
                           {
                               case 0:
                                   break;
                               case 8:
                                   break;
                               case 9:
                                   if (ParamSetting_Serial.Instance.Time == SConfigForm.Instance.t3 * 1000)
                                   {
                                       SendSecondData();
                                       SConfigForm.Instance.t3 = 0;
                                   }
                                   break;
                               case 11:
                                   break;
                           }
                           
                        }
                    }
                    
                }
            }
        }

        /*
        * 发送一级数据
        */
        private void SendFirstData()
        {
            if (isRecvFlag)
            {
                isRecvFlag = false;
                SendShortFrame(0x7A);
            }
            else
            {
                isRecvFlag = true;
                SendShortFrame(0x5A);
            }
        }

        /*
        * 发送二级数据
        */
        public void SendSecondData()
        {
            if (isRecvFlag)
            {                
                SendShortFrame(0x5B);
                isRecvFlag = false;
            }
            else
            {                
                SendShortFrame(0x7B);
                isRecvFlag = true;
            }
        }

        /*
        * 平衡式短帧管理
        */
        public void DealWithBalanceShortFrame(byte ctlreg)
        {
            SendAckConfirm(ctlreg);
        }

        /*
         * 确认ACK
         */
        public void SendAckConfirm(byte ctlreg)
        {
            byte sendCtrl = 0xFF;
            if (isInitLinkLayerFinished)
            {

                return;
            }

            //功能码
            switch (ctlreg & 0x7F)
            {
                case 0x40:
                    sendCtrl = 0x00;
                    //sendCtrl = 0x09;
                    //SendFrame_CallAll(0x73, 0x01, 0x06, 0x14);
                    break;
                case 0x49:
                    sendCtrl = 0x0B;
                    break;
                case 0x0B:
                    sendCtrl = 0x00;
                    break;
            }

            if (sendCtrl == 0xFF)
            {
                return;
            }

            //DIR
            if (iec_type.byBanlance == 1 && iec_type.byBanDirection == 1)
            {
                sendCtrl |= 0x80; 
            }

            //FRM
            if ((ctlreg & 0x40) == 0x00)
            {
                sendCtrl |= 0x40; 
            }

            SendShortFrame(sendCtrl);

        }

        /*
       * 计算数组的校验码
       */
        private byte caculateNum(byte[] sendBuffer,byte control)
        {
            byte crc = 0;
            for (int i = 0; i < sendBuffer.Length - 2; i++)
            {
                crc += sendBuffer[i];
            }
            crc += (byte)(control + iec_type.wLinkaddr);
            return crc;
        }

        public bool OpenSerialPort()
        {
            bool ret = false;
            if(serialClient.OpenSerialPort())
            {
                OnRtLinkChanged(true);
                ret = true;
            }
            return ret;
        }

        public override void Close()
        {
            serialClient.mySerialPort.Close();
            serialClient.mySerialPort.Dispose();

            if (!IsConnected)
            {
                OnRtLinkChanged(false);
            }
        }
        public override bool IsConnected
        {
            get
            {
                return serialClient.mySerialPort.IsOpen;
            }
        }
        /**
         * 量程设置下发
         **/
        //public void SendLongFrame(Byte control, ushort infoaddr, Byte type, byte cot, short value, byte frq)
        //{
        //    APCIFrameHead_S head = new APCIFrameHead_S();
        //    APCISPANInfo_S rtDataInfo = new APCISPANInfo_S();
        //    head.Symbol = START_SYMBOL;
        //    head.length = 0x0B;
        //    head.frameLen = head.length;
        //    head.symbolStart = START_SYMBOL;
        //    head.ctrlreg = control;
        //    head.linkAddr = iec_type.wLinkaddr;
        //    rtDataInfo.ASDU.byType = type;
        //    rtDataInfo.ASDU.byQS = 1;
        //    rtDataInfo.ASDU.wCOT = cot;
        //    rtDataInfo.ASDU.wCommAddr = iec_type.wCommAddr;
        //    rtDataInfo.rtASDU.wInfoAddr = infoaddr;
        //    rtDataInfo.rtASDU.wValue = value;
        //    rtDataInfo.rtASDU.byFRQ = frq;
        //    rtDataInfo.rtASDU.byCrc = (byte)(control + iec_type.wLinkaddr + type + 1 + cot + iec_type.wCommAddr + (infoaddr & 0xFF) + ((infoaddr & 0xFF00) >> 8) + frq + (value & 0xFF) + ((value & 0xFF00) >> 8));
        //    rtDataInfo.rtASDU.byEnd = END_SYMBOL;
        //    IntPtr sendBuf = Marshal.AllocCoTaskMem(bufLen);
        //    if (sendBuf == IntPtr.Zero)
        //        return;

        //    Marshal.StructureToPtr(rtDataInfo, sendBuf, false);
        //    int sendLen = 0;

        //    sendLen = Marshal.SizeOf(rtDataInfo);

        //    byte[] sendByteArray = new byte[bufLen];
        //    for (int i = 0; i < sendLen; i++)
        //    {
        //        sendByteArray[i] = Marshal.ReadByte(sendBuf, i);
        //    }
        //    SendFrame(head, sendByteArray, sendLen);
        //}

        /*
         * DSP程序下载下发
         */
        //public void SendLongFrame(byte[] data,short deviceID)
        //{
        //    APCIFrameHead_S head = new APCIFrameHead_S();
        //    head.Symbol = START_SYMBOL;
        //    head.length = 0x0E;
        //    head.frameLen = head.length;
        //    head.symbolStart = START_SYMBOL;
        //    head.ctrlreg = 0x09;
        //    head.linkAddr = iec_type.wLinkaddr;
        //    APCIDSPInfo_S rtDataInfo = new APCIDSPInfo_S();
        //    rtDataInfo.ASDU.byType = M_FV_TA_1_SYMBOL;
        //    rtDataInfo.ASDU.byQS = 0;
        //    rtDataInfo.ASDU.wCOT = 0x05;
        //    rtDataInfo.ASDU.wCommAddr = iec_type.wCommAddr;
        //    rtDataInfo.rtASDU.byControl = 0;
        //    rtDataInfo.rtASDU.wDevAddr = deviceID;
        //    rtDataInfo.rtASDU.byLength = 0x06;
        //    rtDataInfo.rtASDU.bySerCode = 0x57;
        //    rtDataInfo.rtASDU.byVSQ = 0;
        //    rtDataInfo.rtASDU.byDegment = 0;
        //    rtDataInfo.rtASDU.byCrcType = 0x55;
        //    rtDataInfo.rtASDU.wCRC = (ushort)GetCRCCode(data);
        //    rtDataInfo.rtASDU.byCrc = (byte)(head.ctrlreg + iec_type.wLinkaddr + M_FV_TA_1_SYMBOL + 0x05 + 0x06 + iec_type.wCommAddr + deviceID + 0x57 + 0x55 + rtDataInfo.rtASDU.wCRC);
        //    rtDataInfo.rtASDU.byEnd = 0x16;

        //    IntPtr sendBuf = Marshal.AllocCoTaskMem(bufLen);
        //    if (sendBuf == IntPtr.Zero)
        //        return;

        //    Marshal.StructureToPtr(rtDataInfo, sendBuf, false);
        //    int sendLen = 0;

        //    sendLen = Marshal.SizeOf(rtDataInfo);

        //    byte[] sendByteArray = new byte[bufLen];
        //    for (int i = 0; i < sendLen; i++)
        //    {
        //        sendByteArray[i] = Marshal.ReadByte(sendBuf, i);
        //    }

        //    SendFrame(head, sendByteArray, sendLen);
        //}
        //END REGION
    }

    
}
