using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Reflection;
using System.ComponentModel;
using System.Drawing;
using System.Data;

using Power2000.General.CommTools;

namespace Power2000.CommuCfg
{

    public class PortController : NodeController
    {
        IList physicPortlist = null;
        //private Type type;
        public PortController(IList list, Type t)
            : base(t)
        {
            this.physicPortlist = list;
            //type = t;
        }

        public override Control GetUserUI()
        {
            InitTopLable();
            DataGridView dataGridView = InitDataGridView();
            SetDataGridViewDataSource(dataGridView, physicPortlist);
            return dataGridView;
        }

        public override void InitDataGridViewEvent(DataGridView gridView)
        {
            base.InitDataGridViewEvent(gridView);
            gridView.CellContentClick += new DataGridViewCellEventHandler(DataGridView_CellContentClick);      
        }
      
        public void UpdateLinkChannel()
        {
            ArrayList allPhyLinks = new ArrayList();
            allPhyLinks.AddRange(XmlFile.Instance.EthernetList);
            allPhyLinks.AddRange(XmlFile.Instance.CanbusList);
            allPhyLinks.AddRange(XmlFile.Instance.SerialList);
            allPhyLinks.AddRange(XmlFile.Instance.ModemList);
            foreach (LinkBase phyLink in allPhyLinks)
            {
                phyLink.LogicChannel = "无";
            }
            foreach (Logiclinks logLink in XmlFile.Instance.LogiclinksList)
            {
                LinkBase linkObj1 = Utility.GetPhysicalLink(logLink.First);
                if (linkObj1 != null)
                    linkObj1.LogicChannel = logLink.Name;

                LinkBase linkObj2 = Utility.GetPhysicalLink(logLink.Second);
                if (linkObj2 != null)
                    linkObj2.LogicChannel = logLink.Name;
            }
        }

        //protected override ContextMenuStrip InitDataGridViewMenu()
        //{
        //    return null;
        //}

        protected override ArrayList GetComboBoxColumnDataSource(PropertyDescriptor pd)
        {
            ArrayList list = new ArrayList();

            foreach (Protocols protocol in XmlFile.Instance.ProtocolsList)
            {
                ComboItem item = new ComboItem(protocol.ID, protocol.ID + @" " + protocol.Description);
                list.Add(item);
            }
            ComboItem uItem = new ComboItem(-1, "未设置");
            list.Add(uItem);
            list.Sort();
            return list;

        }

        private void SetPortParam(DataGridViewCell paramCell)
        {
            DataGridViewButtonCell butnCell = paramCell as DataGridViewButtonCell;
            string text = butnCell.Value == null ? "" : butnCell.Value.ToString();
            StringParser parser = new StringParser(text);
            ParamSetting_Serial paramEdit = new ParamSetting_Serial(butnCell);
            if (parser.Parts.Length == 4)
            {
                foreach (string item in paramEdit.baudRate.Items)
                {
                    if (item == parser.Parts[0])
                        paramEdit.baudRate.SelectedItem = item;
                }
                foreach (VerifyStyle item in paramEdit.comboVerify.Items)
                {
                    if (item.ToString() == parser.Parts[1])
                        paramEdit.comboVerify.SelectedItem = item;
                }
                foreach (string item in paramEdit.dataBit.Items)
                {
                    if (item == parser.Parts[2])
                        paramEdit.dataBit.SelectedItem = item;
                }
                foreach (string item in paramEdit.stopBit.Items)
                {
                    if (item == parser.Parts[3])
                        paramEdit.stopBit.SelectedItem = item;
                }
            }
            paramEdit.StartPosition = FormStartPosition.CenterParent;
            paramEdit.ShowDialog();
        }

        private void SetPortExtParam(DataGridViewCell extParamCell)
        {
            DataGridViewButtonCell butnCell = extParamCell as DataGridViewButtonCell;
            LinkBase link = curDataGrid.Rows[butnCell.RowIndex].DataBoundItem as LinkBase;
            if (link == null)
                return;

            Protocols protocols = link.GetProtocols();
            if (protocols == null)
                return;
            if (butnCell.Value!=null)
            {
                protocols.SetExpandParamList(butnCell.Value.ToString());
            }

            ExpandParamEdit paramEdit = new ExpandParamEdit();
            paramEdit.SetListViewItems(protocols.ExpandParamList);
            paramEdit.ShowDialog();

            if (paramEdit.IsFinish)
            {
                butnCell.Value = paramEdit.Vparam;
                protocols.SetExpandParamList(paramEdit.Vparam);

            }
        }

        private void DataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            this.curDataGrid = sender as DataGridView;
            DataGridViewCell curCell = curDataGrid.CurrentCell;
            if ((curCell as DataGridViewButtonCell) != null && e.RowIndex != -1)
            {
                if (curDataGrid.Columns[e.ColumnIndex].DataPropertyName == "Param")
                {
                    SetPortParam(curCell);
                }
                else if (curDataGrid.Columns[e.ColumnIndex].DataPropertyName == "ExParam")
                {
                    SetPortExtParam(curCell);
                }
            }
        }


        private ArrayList GetSubListSource()
        {
            ArrayList list = new ArrayList();
            foreach (Protocols prot in XmlFile.Instance.ProtocolsList)
            {
                ComboItem item = new ComboItem(prot.ID, prot.ID + @" " + prot.Description);
                list.Add(item);
            }
            ComboItem uItem = new ComboItem(-1, "未设置");
            list.Add(uItem);
            list.Sort();
            return list;
        }

        public override void UpdateSource()
        {
            //更新通讯口所属通讯连接
            UpdateLinkChannel();
            //更新通道对应规约集合
            for (int i = 0; i < base.curDataGrid.Columns.Count; i++)
            {
                if (curDataGrid.Columns[i].DataPropertyName == "ProtocolID")
                {
                    ArrayList list = GetSubListSource();
                    (curDataGrid.Columns[i] as DataGridViewComboBoxColumn).DataSource = list;
                }
            }
        }

        //protocolID = -1 未设置
        int oldProtocolId = -2;
        protected override void gridView_CellValueChanged(Object sender, DataGridViewCellEventArgs e)
        {
            base.gridView_CellValueChanged(sender, e);
            this.curDataGrid = sender as DataGridView;

            if (curDataGrid.CurrentCell.OwningColumn.DataPropertyName == "ProtocolID")
            {
                int protocolID = (int)(curDataGrid.CurrentCell as DataGridViewComboBoxCell).Value;
                DataGridViewRow row = curDataGrid.Rows[e.RowIndex];
                DataGridViewCell paramCell = GetExtParamCell(row);
                if (paramCell == null)
                    return;

                if (protocolID == -1)
                    paramCell.Value = "";

                if (protocolID != oldProtocolId)
                {
                    Protocols selProtocol = null;
                    foreach (Protocols protocol in XmlFile.Instance.ProtocolsList)
                    {
                        if (protocol.ID == protocolID)
                        {
                            selProtocol = protocol;
                            break;
                        }

                    }
                    if (selProtocol == null)
                    {
                        return;
                    }
                    paramCell.Value = selProtocol.Params;

                }
            }
        }

        private DataGridViewCell GetExtParamCell(DataGridViewRow row)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.OwningColumn.DataPropertyName == "ExParam")
                {
                    return cell;
                }
            }
            return null;
        }

        private void InitTopLable()
        {
            if (topLableControl == null)
            {
                topLableControl = new LableControl();
            }
            topLableControl.Height = 40;
            topLableControl.Controls.Clear();

            Label tag = new Label();
            tag.Width = topLableControl.Width;
            switch (columnType.Name)
            {
                case "Canbus":
                    tag.Text = "CAN口:";
                    break;
                case "Serial":
                    tag.Text = "串口:";
                    break;
                case "Modem":
                    tag.Text = "Modem口:";
                    break;
            }            
            tag.Location = new Point(20, 15);

            Button button = new Button();
            button.Width = 60;
            button.Text = "保存";
            button.Location = new Point(topLableControl.Width - 100, 10);
            button.Click += new EventHandler(Save_Click);
            topLableControl.Controls.Add(button);
            topLableControl.Controls.Add(tag);
        }
        public override void OnObjDeleted(object obj)
        {
            base.OnObjDeleted(obj);

            if(columnType == typeof(Serial))
            {
                foreach (DataGridViewRow row in curDataGrid.Rows)
                {
                    Serial ser = row.DataBoundItem as Serial;
                    ser.ID = row.Index;
                }
            }
            else if (columnType == typeof(Canbus))
            {
                foreach (DataGridViewRow row in curDataGrid.Rows)
                {
                    Canbus can = row.DataBoundItem as Canbus;
                    can.ID = row.Index;
                }
            }
            else if (columnType == typeof(Modem))
            {
                foreach (DataGridViewRow row in curDataGrid.Rows)
                {
                    Modem mod = row.DataBoundItem as Modem;
                    mod.ID = row.Index;
                }
            }
        }

        protected override object ObjNewCreate(object obj, Type t)
        {
            ICloneable c = obj as ICloneable;
            object newObj = null == c ? Activator.CreateInstance(t) : c.Clone();
            if((newObj as Serial) != null)
            {
                (newObj as Serial).ID = GetSerialID();

            }
            else if ((newObj as Canbus) != null)
            {
                (newObj as Canbus).ID = GetCanbusID();
            }
            else if((newObj as Modem) != null)
            {
                (newObj as Modem).ID = GetModemID();
            }
            else
            {
                return newObj;
            }
            return newObj;
        }

        private int GetSerialID()
        {
            if(XmlFile.Instance.SerialList.Count == 0)
            {
                return 0;
            }

            if(isEditing)
            {
                return curDataGrid.SelectedRows[0].Index;
            }

            int id = 0;
            foreach (Serial ser in XmlFile.Instance.SerialList)
            {
                if (ser.ID > id)
                {
                    id = ser.ID;
                }
            }
            return id + 1;
        }

        private int GetCanbusID()
        {
            if (XmlFile.Instance.CanbusList.Count == 0)
            {
                return 0;
            }

            if (isEditing)
            {
                return curDataGrid.SelectedRows[0].Index;
            }

            int id = 0;
            foreach (Canbus can in XmlFile.Instance.CanbusList)
            {
                if (can.ID > id)
                {
                    id = can.ID;
                }
            }
            return id + 1;
        }

        private int GetModemID()
        {
            if (XmlFile.Instance.ModemList.Count == 0)
            {
                return 0;
            }

            if (isEditing)
            {
                return curDataGrid.SelectedRows[0].Index;
            }

            int id = 0;
            foreach (Modem m in XmlFile.Instance.ModemList)
            {
                if (m.ID > id)
                {
                    id = m.ID;
                }
            }
            return id + 1;
        }

        public void Save_Click(object sender, EventArgs e)
        {
            switch (columnType.Name)
            {
                case "Canbus":
                    XmlFile.Instance.SaveSingleElement(CfgFileMgr.Instance.CurCfgFilePath, "canbus");
                    break;
                case "Serial":
                    XmlFile.Instance.SaveSingleElement(CfgFileMgr.Instance.CurCfgFilePath, "serial");
                    break;
                case "Modem":
                    XmlFile.Instance.SaveSingleElement(CfgFileMgr.Instance.CurCfgFilePath, "modem");
                    break;
            }          
        }
        //end
    }
}
