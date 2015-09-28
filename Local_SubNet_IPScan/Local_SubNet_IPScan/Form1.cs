using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Local_SubNet_IPScan
{
	public partial class Form1 : Form
	{

		List<DeviceInfo> _listd = new List<DeviceInfo>();
		bool _Stopflag = false;

		public Form1()
		{
			InitializeComponent();
			CheckForIllegalCrossThreadCalls = false;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Thread t = new Thread(new ThreadStart(GetResults));
			t.Start();
			label1.ForeColor = System.Drawing.Color.Red;
			label1.Text = "Processing ...";
			label1.Visible = true;
			Application.DoEvents();
		}

		private void GetResults()
		{
			richTextBox1.Clear();
			ScanIPDevice scd = new ScanIPDevice();


			_listd = scd.GetDevices();

			if (_Stopflag )
			{
				return;
			}

			foreach (DeviceInfo dd in _listd)
			{
				richTextBox1.AppendText("IP: " + dd.IPAddr + "\n");
				richTextBox1.AppendText("Mac: " + dd.MacAddr + "\n");
				richTextBox1.AppendText("-----------------------------------------------");
				richTextBox1.AppendText("\n");

			}
			label1.ForeColor = System.Drawing.Color.Green;
			label1.Text = "Completed ! Scan is over.";
		}

		private void Form1_Load(object sender, EventArgs e)
		{

		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			_Stopflag = true;

			foreach(DeviceInfo d in _listd )
			{
				d.StopThread();
			}
		}

	}
}
