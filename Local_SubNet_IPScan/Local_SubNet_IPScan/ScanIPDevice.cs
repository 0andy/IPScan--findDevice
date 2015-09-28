using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Linq;

namespace Local_SubNet_IPScan
{
	public class ScanIPDevice
	{
		private String _IP = "";
		private String _IPSubMask = "";
		private String _IPSubnet0 = "";
		private String _IPBroadCast = "";


		private Dictionary<String, DeviceInfo> _dicDeviceInfo = new Dictionary<String, DeviceInfo>();
		private List<DeviceInfo> _listfounddevice = new List<DeviceInfo>();

		private List<Task<PingReply>> _pingTasks = new List<Task<PingReply>>();

		public ScanIPDevice()
		{
		}


		public List<DeviceInfo> GetDevices()
		{
			try
			{
				//for offline device, you could run arp -d to clear arp table first

				//0 Init all device
				InitIPDevice();

				//1 ping broadcast
				PingBroadCast();

				//2 first time get arp result
				GetARPInfo(false);

				//3 ping other ip not in arp table list
				foreach (String k in _dicDeviceInfo.Keys)
				{
					DeviceInfo d = new DeviceInfo("");
					d = _dicDeviceInfo[k];

					if (!d.isFound)
					{
						_pingTasks.Add(d.PingAsync(d.IPAddr));
					}

					//4 record these device have pinged in arp table
					if (_pingTasks.Count == 100)
					{
						Task.WaitAll(_pingTasks.ToArray());
						GetARPInfo(true);
					}
				}

				//5 record the last part devices
				Task.WaitAll(_pingTasks.ToArray());
				GetARPInfo(true);

				//watch.[Stop]()
				//Dim elapsedMs = watch.ElapsedMilliseconds
				//MessageBox.Show(elapsedMs.ToString)



				//return founded device
				foreach (DeviceInfo ddd in _dicDeviceInfo.Values)
				{
					if (ddd.isFound)
					{
						_listfounddevice.Add(ddd);
					}
				}

				return _listfounddevice;
			}
			catch (Exception e)
			{
				throw new Exception("Throw  'arp -a'   Error", e);
			}

		}

		private void InitIPDevice()
		{

			//Get subnet and broadcast
			NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();


			foreach (NetworkInterface ni in adapters)
			{
				if (ni.OperationalStatus != OperationalStatus.Up)
				{
					// The interface is disabled; skip it.
					continue;
				}

				IPInterfaceProperties ipProp = ni.GetIPProperties();

				foreach (UnicastIPAddressInformation ip in ipProp.UnicastAddresses)
				{
					if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
					{
						// The IP address is not an IPv4 address.
						continue;
					}
					_IP = ip.Address.ToString();
					_IPSubMask = ip.IPv4Mask.ToString();
					break;
				}
				break;
			}


			byte[] mask = IPAddress.Parse(_IPSubMask).GetAddressBytes();
			byte[] iprev = IPAddress.Parse(_IP).GetAddressBytes();
			// Network id - network address
			byte[] netid = BitConverter.GetBytes(BitConverter.ToUInt32(iprev, 0) & BitConverter.ToUInt32(mask, 0));
			// Binary inverted netmask
			UInt32 inmask = BitConverter.ToUInt32(mask, 0);
			//byte[] inv_mask = IPAddress.Parse((~inmask).ToString()).GetAddressBytes();
			byte[] inv_mask = mask.Select(r => (Byte)(~r)).ToArray(); //Select(r => |r).ToArray();
			// Broadcast address
			byte[] brCast = BitConverter.GetBytes(BitConverter.ToUInt32(netid, 0) ^ BitConverter.ToUInt32(inv_mask, 0));

			_IPBroadCast = new IPAddress(brCast).ToString();
			_IPSubnet0 = new IPAddress(netid).ToString();


			fillCollection(netid, brCast);

		}


		private void fillCollection(byte[] ip1, byte[] ip2)
		{

			for (UInt32 n = BitConverter.ToUInt32(ip1.Reverse().ToArray(), 0) + 1; n <= BitConverter.ToUInt32(ip2.Reverse().ToArray(), 0) - 1; n++)
			{
				string ipstr = new System.Net.IPAddress(BitConverter.GetBytes(n).Reverse().ToArray()).ToString();
				DeviceInfo d = new DeviceInfo(ipstr);
				_dicDeviceInfo.Add(ipstr, d);
			}
		}

		private void PingBroadCast()
		{
			Ping pCurrent = new Ping();
			PingReply pReply = default(PingReply);
			try
			{
				pReply = pCurrent.Send(_IPBroadCast);

				if (pReply.Status == IPStatus.Success)
				{
				}

			}
			catch (Exception ex)
			{
			}
		}


		private void GetARPInfo(bool isPinged)
		{
			try
			{



				foreach (var arp in GetARPAResult().Split(new char[] { '\n', '\r' }))
				{
					if (!string.IsNullOrEmpty(arp))
					{

						var pieces = (from piece in arp.Split(new char[] { ' ', '\t' })
									  where !string.IsNullOrEmpty(piece)
									  select piece).ToArray();


						if (pieces.Length == 3)
						{
							if (isPinged)
							{
								if (_dicDeviceInfo.Keys.Contains(pieces[0]) && _dicDeviceInfo[pieces[0]].isPinged)
								{
									_dicDeviceInfo[pieces[0]].isFound = true;
									_dicDeviceInfo[pieces[0]].MacAddr = pieces[1];
								}
							}
							else
							{
								if (_dicDeviceInfo.Keys.Contains(pieces[0]))
								{
									_dicDeviceInfo[pieces[0]].isFound = true;
									_dicDeviceInfo[pieces[0]].MacAddr = pieces[1];
								}
							}

						}
					}
				}

			}
			catch (Exception e)
			{
				throw new Exception("Throw 'arp -a'   Error", e);
			}
		}

		private string GetARPAResult()
		{
			Process myp = null;
			string output = string.Empty;

			try
			{
				myp = Process.Start(new ProcessStartInfo("arp", "-a")
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true
				});

				output = myp.StandardOutput.ReadToEnd();

				myp.Close();
			}
			catch (Exception ex)
			{
				throw new Exception("Error ScanDecice:  display 'arp -a' Results", ex);
			}
			finally
			{
				if (myp != null)
				{
					myp.Close();
				}
			}

			return output;
		}
	}


	public class DeviceInfo
	{
		public String IPAddr { get; set; }
		public String MacAddr { get; set; }

		public bool isPinged { get; set; }
		public bool isFound { get; set; }
		Thread t = null;

		public DeviceInfo(string ip)
		{
			IPAddr = ip;
			isPinged = false;
			isFound = false;
		}

		public Task<PingReply> PingAsync(string address)
		{
			var tcs = new TaskCompletionSource<PingReply>();

			t = new Thread(new ThreadStart(() =>
			{
				Ping ping = new Ping();
				ping.PingCompleted += (obj, sender) =>
				{
					tcs.SetResult(sender.Reply);
				};

				ping.SendAsync(address, new object());


				if (tcs.Task.Result.Status.Equals(IPStatus.Success) || tcs.Task.Result.Status.Equals(IPStatus.TimedOut))
				{
					isPinged = true;
				}
			}
			  ));

			t.Start();

			return tcs.Task;
		}

		public void StopThread()
		{
			if (t != null)
			{
				if (t.IsAlive )
				{
					t.Abort();

				}

			}
		}


	}




}
