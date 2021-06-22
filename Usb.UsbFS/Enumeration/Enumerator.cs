/*
 * Enumerator.cs -- USB-FS device enumeration classes
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
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb.UsbFS.Enumeration
{
	public sealed class Enumerator : Usb.Enumeration.IEnumerator
	{
		private sealed class DevInfo
		{
			public int Bus;
			public int Port;
			public int PortCount;
			public DataRate Rate;
			public byte Parent;
			public byte Address;
		}

		private const string pathDevDir = "/dev/bus/usb";
		private const string pathSysDir = "/sys/bus/usb/devices";
		private const string pathProcMnt = "/proc/bus/usb";
		private const string pathProcDevFile = pathProcMnt + "/devices";

		public Enumerator()
		{
		}

		public bool IsAvailable
		{
			// TODO: check for Linux system
			// TODO: check if mountpoint (in procfs)
			get
			{
				if(Directory.Exists(pathDevDir) &&
				   Directory.Exists(pathSysDir))
					return true;
				return File.Exists(pathProcDevFile);
			}
		}

		public bool IsUsingDeprecated
		{
			get
			{
				return IsAvailable &&
				       (!Directory.Exists(pathDevDir) ||
				        !Directory.Exists(pathSysDir));
			}
		}

		private const int maxRetries = 1;

		public IEnumerable<Usb.Enumeration.IHub> Scan()
		{
			if(!IsAvailable)
				throw new InvalidOperationException("inavailable");
			if(IsUsingDeprecated)
				return ScanProc();
			else
				return ScanSys();
		}

		private static bool ReadIntFromFile(string file, out int val)
		{
			string str;
			if(ReadStringFromFile(file, out str))
			{
				return int.TryParse(str, out val);
			}
			else
			{
				val = 0;
				return false;
			}
		}

		private static bool ReadStringFromFile(string file, out string val)
		{
			int fd = -1;
			long ret;
			byte[] buffer;
			try
			{
				fd = Ioctl.open(file, Ioctl.O_RDONLY);
				if(fd < 0)
				{
					int errno = Marshal.GetLastWin32Error();
					if(errno == 2) // ENOENT
					{
						val = null;
						return false;
					}
					throw new IOException("Couldn't open " + file + ". (" +
					                      errno.ToString() + ")");
				}
				buffer = new byte[32];
				unsafe
				{
					fixed(byte* pin = buffer)
						ret = Ioctl.pread(fd, pin, new UIntPtr(32U),
						                  IntPtr.Zero).ToInt64();
				}
			}
			finally
			{
				if(fd != -1)
					while(Ioctl.close(fd) == -1 &&
					      Marshal.GetLastWin32Error() == 4); // EINTR
			}
			if(ret < 0)
			{
				int errno = Marshal.GetLastWin32Error();
				throw new IOException("Errno: " +
				                      errno.ToString());
			}
			else
			{
				if(ret > 32L)
				{
					throw new IOException
						("Unexpected behavior: " +
						 "Syscall pread read to much");
				}
				else
				{
					System.Text.StringBuilder sb =
						new System.Text.StringBuilder();
					for(int i = 0; i < (int)ret; i++)
					{
						byte b = buffer[i];
						if(b < 32 || b > 127) break;
						sb.Append((char)b);
					}
					val = sb.ToString();
					return true;
				}
			}
		}

		private static Usb.Enumeration.IHub[] ScanSys()
		{
			int retries = 0;
		retry:
			bool lastTry = retries++ > maxRetries;
			List<DevInfo> dev = new List<DevInfo>();
			DirectoryInfo sysDir =
				new DirectoryInfo(pathSysDir);
			foreach(DirectoryInfo dir in sysDir.GetDirectories())
			{
				int bus;
				int[] br;
				if(Usb.Enumeration.Device.TryParseBusAndBranch(dir.Name,
				                                               out bus,
				                                               out br))
				{
					DevInfo d = new DevInfo();
					d.Bus = bus;
					int brl = br.Length;
					if(brl != 0) d.Port = br[brl - 1] - 1;
					int i;
					if(!ReadIntFromFile(dir.FullName + "/busnum", out i))
						continue;
					if(bus != i) continue;
					if(brl != 0)
					{
						// get parent device address
						string parentStr;
						if(brl == 1)
						{
							parentStr = "usb" + bus.ToString();
						}
						else
						{
							int[] brp = new int[brl - 1];
							Array.Copy(br, brp, brl - 1);
							parentStr = bus.ToString() + "-" +
								Usb.Enumeration.Device.BranchArrayToString(brp);
						}
						if(!ReadIntFromFile(pathSysDir + "/" + parentStr +
						                    "/devnum",
						                    out i))
							continue;
						if(i < 1 || i > 127) continue;
						d.Parent = (byte)i;
					}
					if(!ReadIntFromFile(dir.FullName + "/devnum", out i))
						continue;
					if(i < 1 || i > 127) continue;
					d.Address = (byte)i;
					string s;
					if(!ReadStringFromFile(dir.FullName + "/speed", out s))
						continue;
					switch(s)
					{
					case "1.5":  d.Rate = DataRate.Low;   break;
					case "12":   d.Rate = DataRate.Full;  break;
					case "480":  d.Rate = DataRate.High;  break;
					case "5000": d.Rate = DataRate.Super; break;
					default: throw new InvalidDataException();
					}
					if(ReadIntFromFile(dir.FullName + "/maxchild", out i))
						d.PortCount = i;
					if(d.Bus <= 0 ||
					   d.Parent > 127 ||
					   d.Port < 0 ||
					   d.Address == 0 ||
					   d.Address > 127 ||
					   d.PortCount < 0)
						throw new InvalidDataException();
					dev.Add(d);
				}
			}
			Usb.Enumeration.IHub[] res = GetTrees(dev, pathDevDir, lastTry);
			if(res == null) goto retry;
			return res;
		}

		private static Usb.Enumeration.IHub[] ScanProc()
		{
			int retries = 0;
		retry:
			bool lastTry = retries++ > maxRetries;
			int fd = -1;
			MemoryStream mem = new MemoryStream();
			try
			{
				fd = Ioctl.open(pathProcDevFile, Ioctl.O_RDONLY);
				if(fd < 0)
				{
					int errno = Marshal.GetLastWin32Error();
					if(errno == 2) // ENOENT
						throw new FileNotFoundException("", pathProcDevFile);
					throw new IOException("Couldn't open " +
					                      pathProcDevFile +
					                      ". (" + errno.ToString() +
					                      ")");
				}
				byte[] buffer = new byte[4096];
				long ret;
				int pos = 0;
				do
				{
					unsafe
					{
						fixed(byte* pin = buffer)
							ret = Ioctl.pread(fd, pin, new UIntPtr(4096U),
							                  new IntPtr(pos)).ToInt64();
					}
					if(ret < 0)
					{
						int errno = Marshal.GetLastWin32Error();
						throw new IOException("Errno: " +
						                      errno.ToString());
					}
					else if(ret > 0)
					{
						if(ret > 4096L)
						{
							throw new IOException
								("Unexpected behavior: " +
								 "Syscall pread read to much");
						}
						else
						{
							mem.Write(buffer, 0, (int)ret);
							pos += (int)ret;
						}
					}
				} while(ret > 0);
			}
			finally
			{
				if(fd != -1)
					while(Ioctl.close(fd) == -1 &&
					      Marshal.GetLastWin32Error() == 4); // EINTR
			}
			mem.Position = 0;
			StreamReader r = new StreamReader(mem);
			List<DevInfo> dev = new List<DevInfo>();
			string line;
			while((line = r.ReadLine()) != null)
			{
				if(line.StartsWith("T:"))
				{
					DevInfo d = new DevInfo();
					string spd;
					try
					{
						d.Bus = int.Parse(GetValue(line, "Bus"));
						d.Parent = byte.Parse(GetValue(line, "Prnt"));
						d.Port = int.Parse(GetValue(line, "Port"));
						d.Address = byte.Parse(GetValue(line, "Dev#"));
						spd = GetValue(line, "Spd");
						d.PortCount = int.Parse(GetValue(line, "MxCh"));
					}
					catch(Exception e)
					{
						throw new InvalidDataException("", e);
					}
					switch(spd)
					{
					case "1.5":  d.Rate = DataRate.Low;   break;
					case "12":   d.Rate = DataRate.Full;  break;
					case "480":  d.Rate = DataRate.High;  break;
					case "5000": d.Rate = DataRate.Super; break;
					default: throw new InvalidDataException();
					}
					if(d.Bus <= 0 ||
					   d.Parent > 127 ||
					   d.Port < 0 ||
					   d.Address == 0 ||
					   d.Address > 127 ||
					   d.PortCount < 0)
						throw new InvalidDataException();
					dev.Add(d);
				}
			}
			r.Close();
			Usb.Enumeration.IHub[] res = GetTrees(dev, pathProcMnt, lastTry);
			if(res == null) goto retry;
			return res;
		}

		private static Usb.Enumeration.IHub[] GetTrees(List<DevInfo> dev,
		                                               string usbfsRoot,
		                                               bool lastTry)
		{
			List<RootHub> bus = new List<RootHub>();
			foreach(DevInfo d in dev)
			{
				if(d.Parent == 0)
				{
					byte[] desc = GetDesc(usbfsRoot, d.Bus, d.Address);
					if(desc == null)
					{
						if(lastTry) continue;
						else return null;
					}
					RootHub rh = new RootHub(d.Address,
					                         d.Rate,
					                         desc,
					                         usbfsRoot,
					                         d.Bus);
					bus.Add(rh);
					rh.ports = new Device[d.PortCount];
					if(!DoChilds(dev, usbfsRoot, rh, lastTry))
						return null;
				}
			}
			int bc = bus.Count;
			Usb.Enumeration.IHub[] hubarr = new Usb.Enumeration.IHub[bc];
			for(int i = 0; i < bc; i++)
				hubarr[i] = bus[i];
			return hubarr;
		}

		private static bool DoChilds(List<DevInfo> dev,
		                             string usbfsRoot,
		                             Hub hub,
		                             bool lastTry)
		{
			foreach(DevInfo d in dev)
			{
				if(d.Bus == hub.Bus && d.Parent == hub.Address)
				{
					byte[] desc = GetDesc(usbfsRoot, d.Bus, d.Address);
					if(desc == null)
					{
						if(lastTry) continue;
						else return false;
					}
					Device ch;
					if(d.PortCount > 0)
					{
						Hub chh = new Hub(hub,
						                  d.Address,
						                  d.Rate,
						                  desc,
						                  usbfsRoot);
						chh.ports = new Device[d.PortCount];
						if(!DoChilds(dev, usbfsRoot, chh, lastTry))
							return false;
						ch = chh;
					}
					else ch = new Device(hub,
					                     d.Address,
					                     d.Rate,
					                     desc,
					                     usbfsRoot);
					if(d.Port >= hub.Count)
						throw new InvalidDataException();
					hub.ports[d.Port] = ch;
				}
			}
			return true;
		}

		private static string GetValue(string line, string key)
		{
			int i = line.IndexOf(key + "=");
			if(i < 0) return null;
			string r = line.Substring(i + key.Length + 1).TrimStart();
			i = r.IndexOf(' ');
			if(i < 0) return r;
			return r.Substring(0, i);
		}

		private static byte[] GetDesc(string usbfsRoot, int bus, byte adr)
		{
			using(DeviceFile f =
			      new DeviceFile(Device.GetPath(usbfsRoot, bus, adr), true))
				return f.GetDescriptors();
		}

		private object tag;

		public object Tag
		{
			get { return tag; }
			set { tag = value; }
		}
	}
}
