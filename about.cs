using System;
using System.Reflection;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Power2000.General.StandardDef;


namespace Power2000.CommuCfg
{
	/// <summary>
	/// About 的摘要说明。
	/// </summary>
	public class About : System.Windows.Forms.Form
	{
		/// <summary>
		/// 必需的设计器变量。
		/// </summary>
		private System.ComponentModel.Container components = null;

		public About()
		{
			//
			// Windows 窗体设计器支持所必需的
			//
			InitializeComponent();

			//
			// TODO: 在 InitializeComponent 调用后添加任何构造函数代码
			//

            linkLabel1.Visible = false;
            //label6.Visible = false;
			Version v = Assembly.GetEntryAssembly().GetName().Version;
			this.versionLabel.Text = v.ToString();//v.Major + "." + v.Minor + "." + v.Build;

			SetTitle(this.Text);
		}

		public void SetTitle(string title)
		{
			string s = string.Format(" -- {0}: {1}",Global.GetString("启动","Start"),
				AppEnvioment.StartTime);
			this.Text = title + s;
		}

		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
        private System.Windows.Forms.LinkLabel linkLabel1;
		private System.Windows.Forms.Label registerInfoLabel;
		private System.Windows.Forms.Label versionLabel;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label6;
        private LinkLabel linkLabel_ShowVersionHis;
        private Label label_Date;

		private static About instance= null;
		public static void ShowAboutForm()
		{
			if (instance == null)
			{
				instance = new About();
			}

            AppEnvioment.CenterAssist(instance);
			instance.ShowDialog();
		}
		
		private static bool isInit = false;
		public static void InitAboutForm(string title, string moduleName, VersionItem version, Image programIco)
		{
			if(isInit)
				return;

			isInit = true;
			if (instance == null)
			{
				instance = new About();
			}
			
			if (title != string.Empty && title != null)
			{
				instance.SetTitle(title);
			}


            instance.versionLabel.Text = string.Format("{0}(内部版本 {1})", version.Version, version.InterVersion);

            instance.label_Date.Text = version.Date;
			
			if (programIco != null)
			{
				instance.pictureBox1.Image = programIco;
			}
			else
			{				
				instance.pictureBox1.Image = GetFileIcon(System.Reflection.Assembly.GetEntryAssembly().Location).ToBitmap();
			}
		}	
	
		#region 获得应用程序图标. 外部如果使用，请调用Win32Wrap中的实现.		
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
			internal struct SHFILEINFO
		{ 
			internal IntPtr hIcon; 
			internal int    iIcon; 
			internal int   dwAttributes; 
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)]
			internal string szDisplayName; 
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)]
			internal string szTypeName; 
		}

		internal enum ShellFileInfoFlags 
		{
			SHGFI_ICON              = 0x000000100,    
			SHGFI_SMALLICON         = 0x000000001,
			SHGFI_USEFILEATTRIBUTES = 0x000000010,  
		}

		public enum FileAttributeFlags
		{
			FILE_ATTRIBUTE_NORMAL				=     0x00000080,
		}

		[DllImport("shell32.dll", CharSet=CharSet.Auto)] 
		private static extern IntPtr SHGetFileInfo(string drivePath, int fileAttributes,
			ref SHFILEINFO fileInfo, uint countBytesFileInfo, uint flags);
		[DllImport("User32.dll")]
		private static extern int DestroyIcon( IntPtr hIcon );

		private static System.Drawing.Icon GetFileIcon(string name)//, IconSize size, bool linkOverlay)
		{
			SHFILEINFO shfi = new SHFILEINFO();
			uint flags = (uint)ShellFileInfoFlags.SHGFI_ICON | (uint)ShellFileInfoFlags.SHGFI_USEFILEATTRIBUTES;
			flags += (uint)ShellFileInfoFlags.SHGFI_SMALLICON;
			SHGetFileInfo(	name, 
				(int)FileAttributeFlags.FILE_ATTRIBUTE_NORMAL, 
				ref shfi, 
				(uint) System.Runtime.InteropServices.Marshal.SizeOf(shfi), 
				flags );

			// Copy (clone) the returned icon to a new object, thus allowing us to clean-up properly
			System.Drawing.Icon icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(shfi.hIcon).Clone();
			DestroyIcon( shfi.hIcon );		// Cleanup
			return icon;
		}
#endregion

		/// <summary>
		/// 清理所有正在使用的资源。
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows 窗体设计器生成的代码
		/// <summary>
		/// 设计器支持所需的方法 - 不要使用代码编辑器修改
		/// 此方法的内容。
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.versionLabel = new System.Windows.Forms.Label();
            this.registerInfoLabel = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.linkLabel_ShowVersionHis = new System.Windows.Forms.LinkLabel();
            this.label_Date = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // label2
            // 
            this.label2.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            this.label3.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // label4
            // 
            this.label4.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // linkLabel1
            // 
            this.linkLabel1.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.linkLabel1, "linkLabel1");
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.TabStop = true;
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // versionLabel
            // 
            this.versionLabel.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.versionLabel, "versionLabel");
            this.versionLabel.Name = "versionLabel";
            // 
            // registerInfoLabel
            // 
            this.registerInfoLabel.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.registerInfoLabel, "registerInfoLabel");
            this.registerInfoLabel.Name = "registerInfoLabel";
            // 
            // label1
            // 
            this.label1.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label1, "label1");
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Name = "label1";
            // 
            // label5
            // 
            this.label5.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label5, "label5");
            this.label5.ForeColor = System.Drawing.Color.White;
            this.label5.Name = "label5";
            // 
            // label6
            // 
            this.label6.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label6, "label6");
            this.label6.ForeColor = System.Drawing.Color.MediumBlue;
            this.label6.Name = "label6";
            this.label6.Click += new System.EventHandler(this.label6_Click);
            // 
            // linkLabel_ShowVersionHis
            // 
            resources.ApplyResources(this.linkLabel_ShowVersionHis, "linkLabel_ShowVersionHis");
            this.linkLabel_ShowVersionHis.Name = "linkLabel_ShowVersionHis";
            this.linkLabel_ShowVersionHis.TabStop = true;
            this.linkLabel_ShowVersionHis.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_ShowVersionHis_LinkClicked);
            // 
            // label_Date
            // 
            this.label_Date.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.label_Date, "label_Date");
            this.label_Date.Name = "label_Date";
            // 
            // About
            // 
            resources.ApplyResources(this, "$this");
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.linkLabel_ShowVersionHis);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.label_Date);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.versionLabel);
            this.Controls.Add(this.registerInfoLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "About";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
		#endregion

		protected override void OnClosing(CancelEventArgs e)
		{
			e.Cancel = true;
			this.Hide();
		}

		private void linkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
		{	
			System.Diagnostics.Process.Start("http://www.lnint.com");			
		}

		private void pictureBox1_Click(object sender, System.EventArgs e)
		{
			if(Control.ModifierKeys == Keys.Control)
			{
				MessageBox.Show(this,
					String.Format( Power2000.General.StandardDef.Global.GetString("程序路径是: {0}","App Path is: {0}"),Assembly.GetEntryAssembly().Location),
					Power2000.General.StandardDef.Global.GetString("程序路径","App Path"),MessageBoxButtons.OK);
			}				
		}

        private void linkLabel_ShowVersionHis_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            VersionHistoryForm.Instance.ShowDialog();
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

	}
}
