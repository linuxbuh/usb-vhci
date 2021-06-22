/*
 * Device.cs -- USB device enumeration interfaces
 *
 * Copyright (C) 2009-2010 Michael Singer <michael@a-singer.de>
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
using System.Globalization;

namespace Usb.Enumeration
{
	public static class Device
	{
		private static readonly int[] nullBranch = new int[0];

		public static IDevice FromBranch(IHub hub, int[] branch)
		{
			if(hub == null) throw new ArgumentNullException("hub");
			if(branch == null) throw new ArgumentNullException("branch");
			int bl = branch.Length;
			if(bl == 0 || (bl == 1 && branch[0] == 0)) return hub;
			if(branch[0] <= 0 || branch[0] > hub.Count)
				throw new ArgumentException("", "branch");
			IDevice child = hub[branch[0] - 1];
			if(child == null || !(child is IHub))
			{
				if(bl == 1) return child;
				throw new ArgumentException("", "branch");
			}
			int[] subBranch = new int[bl - 1];
			Array.Copy(branch, 1, subBranch, 0, bl - 1);
			return FromBranch((IHub)child, subBranch);
		}

		public static string BranchArrayToString(int[] branch)
		{
			if(branch == null) throw new ArgumentNullException("branch");
			int l = branch.Length;
			string[] x = new string[l];
			for(int i = 0; i < l; i++)
				x[i] = branch[i].ToString();
			return string.Join(".", x);
		}

		public static int[] BranchStringToArray(string branch)
		{
			if(branch == null) throw new ArgumentNullException("branch");
			if(branch == "") return nullBranch;
			string[] x = branch.Split('.');
			int l = x.Length;
			int[] res = new int[l];
			for(int i = 0; i < l; i++)
			{
				int n;
				if(!int.TryParse(x[i], out n))
					throw new ArgumentException("", "branch");
				res[i] = n;
			}
			return res;
		}

		public static IHub FindRootHub(IEnumerable<IHub> busses, int bus)
		{
			if(busses == null) throw new ArgumentNullException("busses");
			foreach(IHub rh in busses)
				if(rh.Bus == bus)
					return rh;
			return null;
		}

		public static IDevice FromAddress(IDevice dev, byte adr)
		{
			if(dev == null) throw new ArgumentNullException("dev");
			if(dev.Address == adr)
				return dev;
			if(dev is IHub)
			{
				foreach(IDevice child in (IHub)dev)
				{
					if(child != null)
					{
						Usb.Enumeration.IDevice res = FromAddress(child, adr);
						if(res != null)
							return res;
					}
				}
			}
			return null;
		}

		public static IDevice FromVendorAndProduct(IEnumerable<IDevice> devices,
		                                           short vid,
		                                           short pid)
		{
			if(devices == null) throw new ArgumentNullException("devices");
			foreach(Usb.Enumeration.IDevice dev in devices)
			{
				Usb.Enumeration.IDevice res =
					FromVendorAndProduct(dev, vid, pid);
				if(res != null)
					return res;
			}
			return null;
		}

		public static IDevice FromVendorAndProduct(IDevice dev,
		                                           short vid,
		                                           short pid)
		{
			if(dev == null) throw new ArgumentNullException("dev");
			DeviceDescriptor dd = dev.GetDescriptor();
			if(dd != null && dd.VendorID == vid && dd.ProductID == pid)
				return dev;
			if(dev is IHub)
			{
				foreach(IDevice child in (IHub)dev)
				{
					if(child != null)
					{
						Usb.Enumeration.IDevice res =
							FromVendorAndProduct(child, vid, pid);
						if(res != null)
							return res;
					}
				}
			}
			return null;
		}

		public static int[] ToBranch(IDevice dev)
		{
			if(dev == null) throw new ArgumentNullException("dev");
			IHub parent = dev.Parent;
			if(parent == null) return nullBranch;
			int i = parent.IndexOf(dev);
			if(i < 0) throw new ArgumentException("", "dev");
			int[] pb = parent.Branch;
			int pbl = pb.Length;
			int[] b = new int[pbl + 1];
			pb.CopyTo(b, 0);
			b[pbl] = i + 1;
			return b;
		}

		public static int[] ToBranch(IHub hub, int port)
		{
			int[] pb = hub.Branch;
			if(port == 0) return pb;
			int pbl = pb.Length;
			int[] b = new int[pbl + 1];
			pb.CopyTo(b, 0);
			b[pbl] = port;
			return b;
		}

		public static void ParseBusAndBranch(string str,
		                                     out int bus,
		                                     out int[] branch)
		{
			if(str == null) throw new ArgumentNullException("str");
			if(str.ToLower().StartsWith("usb"))
			{
				string sbus = str.Substring(3);
				if(!int.TryParse(sbus, out bus))
					throw new ArgumentException("", "str");
				branch = nullBranch;
				return;
			}
			int i = str.IndexOf('-');
			if(i >= 0)
			{
				string sbus = str.Substring(0, i);
				string sbr = str.Substring(i + 1);
				if(!int.TryParse(sbus, out bus))
					throw new ArgumentException("", "str");
				branch = nullBranch;
				int z;
				if(!(int.TryParse(sbr, out z) && z == 0)) // NOT <bus>-0
				{
					try
					{
						branch = BranchStringToArray(sbr);
					}
					catch(ArgumentException)
					{
						throw new ArgumentException("", "str");
					}
				}
			}
			else
			{
				throw new ArgumentException("", "str");
			}
		}

		public static bool TryParseBusAndBranch(string str,
		                                        out int bus,
		                                        out int[] branch)
		{
			if(str == null) goto retFalse;
			try
			{
				ParseBusAndBranch(str, out bus, out branch);
			}
			catch(ArgumentException)
			{
				goto retFalse;
			}
			return true;

		retFalse:
			bus = 0;
			branch = null;
			return false;
		}

		public static IDevice FromString(IEnumerable<IHub> busses, string str)
		{
			if(busses == null) throw new ArgumentNullException("busses");
			if(str == null) throw new ArgumentNullException("str");
			int bus;
			int[] branch;
			if(TryParseBusAndBranch(str, out bus, out branch))
			{
				IHub rh = FindRootHub(busses, bus);
				if(rh == null) return null;
				return FromBranch(rh, branch);
			}
			int i = str.IndexOf('.');
			if(i >= 0)
			{
				string sbus = str.Substring(0, i);
				string sadr = str.Substring(i + 1);
				byte adr;
				if(!int.TryParse(sbus, out bus) ||
				   !byte.TryParse(sadr, out adr))
					throw new ArgumentException("", "str");
				IHub rh = FindRootHub(busses, bus);
				if(rh == null) return null;
				return FromAddress(rh, adr);
			}
			i = str.IndexOf(':');
			if(i >= 0)
			{
				string svid = str.Substring(0, i);
				string spid = str.Substring(i + 1);
				ushort vid, pid;
				if(!ushort.TryParse(svid,
				                    NumberStyles.HexNumber,
				                    null,
				                    out vid) ||
				   !ushort.TryParse(spid,
				                    NumberStyles.HexNumber,
				                    null,
				                    out pid))
					throw new ArgumentException("", "str");
				return FromVendorAndProduct((IEnumerable<IDevice>)busses,
				                            unchecked((short)vid),
				                            unchecked((short)pid));
			}
			throw new ArgumentException("", "str");
		}

		public static string ToString(IDevice dev, DeviceStringFormat format)
		{
			if(dev == null) throw new ArgumentNullException("dev");
			string bus = dev.Bus.ToString();
			switch(format)
			{
			case DeviceStringFormat.BusAndAddress:
				return bus + "." + dev.Address.ToString();
			case DeviceStringFormat.VendorAndProduct:
				DeviceDescriptor dd = dev.GetDescriptor();
				if(dd == null) return null;
				return unchecked((ushort)(dd.VendorID)).ToString("x4") + ":" +
				       unchecked((ushort)(dd.ProductID)).ToString("x4");
			default:
				string branch = BranchArrayToString(dev.Branch);
				if(branch == "")
				{
					if(format == DeviceStringFormat.BusAndBranch)
						return bus + "-0";
					else // DeviceStringFormat.BusAndBranchOther
						return "usb" + bus;
				}
				return bus + "-" + branch;
			}
		}

		public static string ToString(IDevice dev)
		{
			return ToString(dev, DeviceStringFormat.BusAndBranch);
		}

		public static string ToString(IHub hub, int port)
		{
			if(hub == null) throw new ArgumentNullException("hub");
			string bus = hub.Bus.ToString();
			string branch = BranchArrayToString(ToBranch(hub, port));
			if(branch == "") return bus + "-0";
			return bus + "-" + branch;
		}
	}
}
