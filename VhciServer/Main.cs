/*
 * Main.cs -- VHCI server
 *
 * Copyright (C) 2008-2009 Conemis AG Karlsruhe Germany
 * Copyright (C) 2008-2015 Michael Singer <michael@a-singer.de>
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
using System.Diagnostics;
#if MONO_1_9_SIGHANDLER
using Mono.Unix;
using Mono.Unix.Native;
#endif

namespace VhciServer
{
	public static class MainClass
	{
		private const string me = "VhciServer.exe";
		private const string me2 = "VhciServer";
		private readonly static string ver =
			System.Reflection.Assembly.GetExecutingAssembly().
				GetName().Version.ToString();

		[STAThread]
		public static int Main(string[] args)
		{
			/*
			Console.WriteLine(Vhci.Ioctl.IOCREGISTER.ToString("x2"));
			Console.WriteLine(Vhci.Ioctl.IOCPORTSTAT.ToString("x2"));
			Console.WriteLine(Vhci.Ioctl.IOCFETCHWORK.ToString("x2"));
			Console.WriteLine(Vhci.Ioctl.IOCGIVEBACK.ToString("x2"));
			Console.WriteLine(Vhci.Ioctl.IOCFETCHDATA.ToString("x2"));
			Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vhci.Ioctl.vhci_ioc_work)).ToString());
			Console.WriteLine(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(Vhci.Ioctl.vhci_ioc_work), "work").ToString());
			Console.WriteLine(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(Vhci.Ioctl.vhci_ioc_work), "type").ToString());
			Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vhci.Ioctl.vhci_ioc_work_union)).ToString());
			Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vhci.Ioctl.vhci_ioc_urb)).ToString());
			Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vhci.Ioctl.vhci_ioc_port_stat)).ToString());
			return 0;
			*/
		
			bool debug = false;
			string numStr = null;
			string portStr = null;
			for(int i = 0; i < args.Length; i++)
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
					case "--num-ports":
					case "-n":
						if(i + 1 >= args.Length)
						{
							Console.WriteLine("expecting number of usb ports");
							return 1;
						}
						if(numStr != null)
						{
							Console.WriteLine("number of usb ports specified more than once");
							return 1;
						}
						numStr = args[++i];
						break;
					case "--port":
					case "-p":
						if(i + 1 >= args.Length)
						{
							Console.WriteLine("expecting port number");
							return 1;
						}
						if(portStr != null)
						{
							Console.WriteLine("port number specified more than once");
							return 1;
						}
						portStr = args[++i];
						break;
					case "--version":
					case "-V":
						Copyright();
						return 0;
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
					if(numStr == null) numStr = args[i];
					else if(portStr == null) portStr = args[i];
					else
					{
						Help();
						return 1;
					}
				}
			}
			if(numStr == null)
			{
				Console.WriteLine("number of usb ports not specified");
				return 1;
			}
			byte numPorts = byte.Parse(numStr);
			if(portStr == null)
			{
				Console.WriteLine("port number not specified");
				return 1;
			}
			int tcpPort = int.Parse(portStr);
			Copyright();
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
			Vhci.LocalHcd hcd = Vhci.LocalHcd.Create(numPorts);
			Console.WriteLine("Created " + hcd.BusID + " (usb bus #" +
			                  hcd.UsbBusNum + ")");
			Vhci.Net.TcpHcdServer srv =
				new Vhci.Net.TcpHcdServer(hcd, new System.Net.IPEndPoint
				                          (System.Net.IPAddress.Any,
				                           tcpPort));
#if MONO_1_9_SIGHANDLER
			srv.DoEvents += delegate(object sender, EventArgs e)
			{
				if(sigint.Reset() || sigterm.Reset())
					srv.Stop();
			};
#else
			Console.CancelKeyPress += delegate(object sender,
			                                   ConsoleCancelEventArgs e)
			{
				e.Cancel = true;
				srv.Stop();
			};
#endif
			srv.Run();
			hcd.Dispose();
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
			                  "[-n] NUMPORTS [-p] PORT");
			Console.WriteLine();
			Console.WriteLine("OPTIONS:");
			Console.WriteLine(" --help              " +
			                  "this help");
			Console.WriteLine(" --debug             " +
			                  "debug output");
			Console.WriteLine(" -V --version        " +
			                  "print version");
			Console.WriteLine();
			Console.WriteLine("NUMPORTS:");
			Console.WriteLine(" the number of usb ports the vhci should have");
			Console.WriteLine();
			Console.WriteLine("PORT:");
			Console.WriteLine(" the tcp port");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine(" " + me + " 4 1138");
			Console.WriteLine(" creates a local vhci with four usb ports" +
			                  " and listens at tcp port 1138.");
		}
	}
}
