/*
 * Main.cs -- VHCI client
 *
 * Copyright (C) 2007-2009 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2015 Michael Singer <michael@a-singer.de>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to the Free Software Foundation, Inc.,
 * 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if MONO_1_9_SIGHANDLER
using Mono.Unix;
using Mono.Unix.Native;
#endif

namespace VhciClient
{
	public static class MainClass
	{
		private const string me = "VhciClient.exe";
		private const string me2 = "VhciClient";
		private readonly static string ver =
			System.Reflection.Assembly.GetExecutingAssembly().
				GetName().Version.ToString();

		[STAThread]
		public static int Main(string[] args)
		{
			bool showInfo = false;
			bool noProbe = false;
			bool probe = false;
			bool debug = false;
			string devStr = null;
			string hcdStr = "";
			for(int i = 0, j = 0; i < args.Length; i++)
			{
				if(args[i].Length > 0 && args[i][0] == '-')
				{
					switch(args[i])
					{
					case "--help":
						Copyright();
						Console.WriteLine();
						Help();
						return 0;
					case "--list":
					case "-l":
						List();
						return 0;
					case "--version":
					case "-V":
						Copyright();
						return 0;
					case "--info":
					case "-i":
						showInfo = true;
						break;
					case "--no-probe":
						noProbe = true;
						probe = false;
						break;
					case "--probe":
						probe = true;
						noProbe = false;
						break;
					case "--debug":
						debug = true;
						break;
					default:
						Help();
						return 1;
					}
				}
				else
				{
					if(j == 0) devStr = args[i];
					else if(j == 1) hcdStr = args[i];
					else
					{
						Help();
						return 1;
					}
					j++;
				}
			}
			if(devStr == null)
			{
				Help();
				return 1;
			}
			Usb.Enumeration.IEnumerator enm =
				new Usb.UsbFS.Enumeration.Enumerator();
			IEnumerable<Usb.Enumeration.IHub> busses = enm.Scan();
			if(showInfo)
			{
				Usb.Enumeration.Device.FromString(busses, devStr).
					GetDescriptor().Dump(Console.Out, "");
				return 0;
			}
			Copyright();
			System.Net.IPEndPoint ipep = null;
			if(hcdStr != "")
			{
				int i = hcdStr.LastIndexOf(':'); // LastIndexOf because of IPv6
				if(i < 0)
				{
					Console.WriteLine("Missing colon in HOST:PORT tuple");
					return 1;
				}
				string hstr = hcdStr.Substring(0, i);
				string pstr = hcdStr.Substring(i + 1);
				ipep = new System.Net.IPEndPoint
					(System.Net.Dns.GetHostAddresses(hstr)[0], int.Parse(pstr));
			}
			if(debug)
			{
				ConsoleTraceListener condebug = new ConsoleTraceListener(false);
				Debug.Listeners.Add(condebug);
			}
#if MONO_1_9_SIGHANDLER
			Stdlib.SetSignalAction(Signum.SIGINT, SignalAction.Ignore);
			Stdlib.SetSignalAction(Signum.SIGTERM, SignalAction.Ignore);
			UnixSignal sigint = new UnixSignal(Signum.SIGINT);
			UnixSignal sigterm = new UnixSignal(Signum.SIGTERM);
#endif
			Usb.Enumeration.IDevice d =
				Usb.Enumeration.Device.FromString(busses, devStr);
			Usb.IUsbDevice dev = d.Acquire();
			Vhci.Hcd hcd;
			if(ipep != null)
			{
				try
				{
					hcd = new Vhci.Net.TcpHcd(1, ipep);
				}
				catch(Vhci.Net.InsufficientPortsException)
				{
					Console.WriteLine("The remote host controller has not enough free ports left.");
					return 1;
				}
			}
			else
			{
				hcd = Vhci.LocalHcd.Create(1);
				Console.WriteLine("Created " + ((Vhci.LocalHcd)hcd).BusID +
				                  " (usb bus #" + ((Vhci.LocalHcd)hcd).
				                  UsbBusNum + ")");
			}
			Vhci.Forwarders.SingleDeviceForwarder fw =
				new Vhci.Forwarders.SingleDeviceForwarder(hcd, dev);
#if MONO_1_9_SIGHANDLER
			fw.DoEvents += delegate(object sender, EventArgs e)
			{
				if(sigint.Reset() || sigterm.Reset())
					fw.Stop();
			};
#else
			Console.CancelKeyPress += delegate(object sender,
			                                   ConsoleCancelEventArgs e)
			{
				e.Cancel = true;
				fw.Stop();
			};
#endif
			try { fw.Run(); }
			catch(Usb.DeviceDisconnectedException)
			{
				Console.WriteLine("Device disconnected");
			}
			catch(System.Net.Sockets.SocketException e)
			{
				if(e.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown)
					Console.WriteLine("Connection closed by remote host");
				else
					Console.WriteLine("Connection error");
			}
			hcd.Dispose();
			dev.Dispose();
			if(!noProbe)
			{
				if(probe || Usb.UsbFS.Enumeration.Device.DriversAutoProbe)
				{
					try { d.DriversProbe(); }
					catch(System.IO.IOException) { }
				}
			}
			return 0;
		}

		private static void Copyright()
		{
			Console.WriteLine(me2 + " " + ver);
			Console.WriteLine("Copyright © 2007-2009 Conemis AG Karlsruhe Germany");
			Console.WriteLine("Copyright © 2007-2015 Michael Singer <michael@a-singer.de>");
			Console.WriteLine("This is free software; see the source for copying conditions.  There is NO");
			Console.WriteLine("warranty; not even for MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.");
		}

		private static void Help()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine(" " + me + " [OPTIONS] " +
			                  "USBDEVICE [HOSTCONTROLLER]");
			Console.WriteLine();
			Console.WriteLine("OPTIONS:");
			Console.WriteLine(" --help              " +
			                  "this help");
			Console.WriteLine(" -l --list [IFC]     " +
			                  "list all usb devices");
			Console.WriteLine(" -i --info           " +
			                  "print information about device and exit");
			Console.WriteLine(" --no-probe          " +
			                  "don't probe the device on exit");
			Console.WriteLine(" --probe             " +
			                  "probe the device on exit, even if" +
			                  " /sys/bus/usb/drivers_autoprobe is set to 0");
			Console.WriteLine(" --debug             " +
			                  "debug output");
			Console.WriteLine(" -V --version        " +
			                  "print version");
			Console.WriteLine();
			Console.WriteLine("USBDEVICE:");
			Console.WriteLine(" [IFC,]BUS.ADR   or  " +
			                  " [IFC,]BUS-USBPORT[.USBPORT[.USBPORT[...]]]");
			Console.WriteLine("  or   [IFC,]VENDORID:PRODUCTID");
			Console.WriteLine();
			Console.WriteLine("HOSTCONTROLLER:");
			Console.WriteLine(" HOST:PORT");
			Console.WriteLine();
			Console.WriteLine("IFC:");
			Console.WriteLine(" usbfs               " +
			                  "use usbfs interface (default)");
			Console.WriteLine();
			Console.WriteLine("BUS:");
			Console.WriteLine(" the usb bus id");
			Console.WriteLine();
			Console.WriteLine("ADR:");
			Console.WriteLine(" the usb device address");
			Console.WriteLine();
			Console.WriteLine("USBPORT:");
			Console.WriteLine(" usb port number (first port is 1, not 0)");
			Console.WriteLine();
			Console.WriteLine("VENDORID, PRODUCTID:");
			Console.WriteLine(" vendor/product id in hex (without 0x prefix)");
			Console.WriteLine();
			Console.WriteLine("HOST:");
			Console.WriteLine(" the hostname or ip address of the host" +
			                  " which provides the vhci");
			Console.WriteLine();
			Console.WriteLine("PORT:");
			Console.WriteLine(" the tcp port");
			Console.WriteLine();
			Console.WriteLine("Remarks:");
			Console.WriteLine(" If HOSTCONTROLLER is omitted, a local" +
			                  " vhci will be created instead of");
			Console.WriteLine(" connecting to a (remote) host.");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine(" " + me + " -l");
			Console.WriteLine(" lists all available usb devices.");
			Console.WriteLine();
			Console.WriteLine(" " + me + " 5.2");
			Console.WriteLine(" creates a local vhci and plugs the usb" +
			                  " device with address 2 of bus 5 into it.");
			Console.WriteLine();
			Console.WriteLine(" " + me + " 3-1.2 192.168.0.23:1138");
			Console.WriteLine(" connects to a remote vhci on 192.168.0.23" +
			                  " at tcp port 1138 and plugs the usb device");
			Console.WriteLine(" which is (physically) plugged into port 2" +
			                  " of the usb hub which is plugged into");
			Console.WriteLine(" port 1 of the root hub which belongs to" +
			                  " usb bus 3, into it.");
		}

		private static void List()
		{
			Usb.Enumeration.IEnumerator e =
				new Usb.UsbFS.Enumeration.Enumerator();
			IEnumerable<Usb.Enumeration.IHub> bus = e.Scan();
			foreach(Usb.Enumeration.IHub rh in bus)
			{
				Console.WriteLine("[RootHub of Bus# " +
				                  rh.Bus.ToString() + "]");
				ListPorts(rh);
			}
		}

		private static void ListPorts(Usb.Enumeration.IHub hub)
		{
			for(int i = 0; i < hub.Count; i++)
			{
				if(hub[i] == null)
				{
					Console.WriteLine(Usb.Enumeration.Device.ToString(hub,
					                                                  i + 1) +
					                  " [unconnected port]");
				}
				else if(hub[i] is Usb.Enumeration.IHub)
				{
					Console.WriteLine(hub[i].ToString() + " [Hub]");
					ListPorts((Usb.Enumeration.IHub)hub[i]);
				}
				else
				{
					Usb.DeviceDescriptor d = null;
					try
					{
						d = hub[i].GetDescriptor();
					}
					catch(System.IO.IOException)
					{
						Console.WriteLine(hub[i].ToString() +
						                  " [I/O error while checking device " +
						                  "type (address: " +
						                  hub[i].Address.ToString() + ")]");
						continue;
					}
					if(d != null)
					{
						Usb.ClassCodeTriple cct = d.ClassCodeTriple;
						if(cct == Usb.ClassCodeTriple.Null &&
						   d.IsUsingConfigurationIndex(1) &&
						   d[1].IsUsingInterfaceIndex(0) &&
						   d[1][0].IsUsingAlternateSettingIndex(0))
							cct = d[1][0][0].ClassCodeTriple;
						if(cct != Usb.ClassCodeTriple.Null)
						{
							string cs = cct.BaseClassString();
							Console.WriteLine(hub[i].ToString() +
							                  " [" + cs + " (address: " +
							                  hub[i].Address.ToString() + ")]");
							continue;
						}
					}
					Console.WriteLine(hub[i].ToString() +
					                  " [unknown device (address: " +
					                  hub[i].Address.ToString() + ")]");
				}
			}
		}
	}
}
