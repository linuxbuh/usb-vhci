/*
 * Device.cs -- USB-FS device enumeration classes
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
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Usb.UsbFS.Enumeration
{
	public class Device : Usb.Enumeration.IDevice
	{
		private Hub parent;
		private byte address;
		private DataRate rate;
		private byte[] desc;
		private string usbfsRoot;
		private object tag;

		internal Device(Hub parent,
		                byte address,
		                DataRate rate,
		                byte[] desc,
		                string usbfsRoot)
		{
			this.parent = parent;
			this.address = address;
			this.rate = rate;
			this.desc = desc;
			this.usbfsRoot = usbfsRoot;
		}

		public Usb.Enumeration.IHub Parent
		{
			get { return parent; }
		}

		public virtual Usb.Enumeration.IHub Root
		{
			get { return parent == null ? null : parent.Root; }
		}

		public virtual int Tier
		{
			get
			{
				if(parent == null)
				{
					return -1;
				}
				else
				{
					int pt = parent.Tier;
					return pt > 0 ? pt + 1 : pt - 1;
				}
			}
		}

		public virtual int Bus
		{
			get { return parent == null ? -1 : parent.Bus; }
		}

		public byte Address
		{
			get { return address; }
		}

		public int[] Branch
		{
			get { return Usb.Enumeration.Device.ToBranch(this); }
		}

		public DataRate DataRate
		{
			get { return rate; }
		}

		public Usb.DeviceDescriptor GetDescriptor()
		{
			DeviceDescriptor dev =
				new Usb.DeviceDescriptor(desc, Usb.Endianness.System);
			DeviceFile usbfs;
			try { usbfs = new DeviceFile(Path); }
			catch(IOException) { return dev; }
			try { usbfs.UpdateDescriptorActiveState(dev); }
			finally { usbfs.Dispose(); }
			return dev;
		}

		public virtual bool IsAcquireable
		{
			get { return System.IO.File.Exists(Path); }
		}

		public Usb.IUsbDevice Acquire()
		{
			if(IsAcquireable)
				return new UsbDevice(Path);
			throw new InvalidOperationException("inacquireable");
		}

		public string Path
		{
			get { return GetPath(usbfsRoot, Bus, address); }
		}

		public static string GetPath(string usbfsRoot, int bus, byte address)
		{
			return string.Format("{0}/{1:D3}/{2:D3}",
			                     usbfsRoot,
			                     bus,
			                     address);
		}

		public override sealed string ToString()
		{
			return Usb.Enumeration.Device.ToString(this);
		}

		public string ToString(Usb.Enumeration.DeviceStringFormat format)
		{
			return Usb.Enumeration.Device.ToString(this, format);
		}

		public object Tag
		{
			get { return tag; }
			set { tag = value; }
		}

		private const string probePath     = "/sys/bus/usb/drivers_probe";
		private const string autoProbePath = "/sys/bus/usb/drivers_autoprobe";

		public void DriversProbe()
		{
			// Probe the device itself, just in case, the device was never
			// probed before (if /sys/bus/usb/drivers_autoprobe is set to 0).
			// This only works for root.
			string id = ToString(Usb.Enumeration.DeviceStringFormat.
			                     BusAndBranchOther);
			DriversProbeIntern(id);

			// Connect the interfaces (Should work for non-root users)
			ConfigurationDescriptor conf = GetDescriptor().ActiveConfiguration;
			if(conf != null)
			{
				DeviceFile usbfs;
				try { usbfs = new DeviceFile(Path); }
				catch(IOException) { return; }
				try
				{
					foreach(byte ifc in conf.InterfaceIndices)
					{
						try { usbfs.Connect(ifc); }
						catch(IOException) { }
					}
				}
				finally { usbfs.Dispose(); }

				// Usbfs fails if the device was never probed before (if
				// /sys/bus/usb/drivers_autoprobe is set to 0), so try it
				// another way. This only works for root.
				id = ToString();
				foreach(byte ifc in conf.InterfaceIndices)
					DriversProbeIntern(id + ":" + conf.Index.ToString() +
					                   "." + ifc.ToString());
			}
		}

		private static void DriversProbeIntern(string id)
		{
			int fd = -1;
			long ret;
			byte[] buffer = new byte[id.Length];
			for(int i = 0; i < buffer.Length; i++)
				buffer[i] = checked((byte)id[i]);
			try
			{
				fd = Ioctl.open(probePath, Ioctl.O_WRONLY);
				if(fd < 0)
				{
					/*
					int errno = Marshal.GetLastWin32Error();
					if(errno == 2) // ENOENT
						throw new FileNotFoundException("", probePath);
					throw new IOException("Couldn't open " + probePath + ". (" +
					                      errno.ToString() + ")");
					*/
					return;
				}
				unsafe
				{
					fixed(byte* pin = buffer)
						ret = Ioctl.write(fd, pin,
						                  new UIntPtr(unchecked((ulong)buffer.
						                                        Length))
						                  ).ToInt64();
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
		}

		public static bool DriversAutoProbe
		{
			get
			{
				int fd = -1;
				long ret;
				byte val;
				try
				{
					fd = Ioctl.open(autoProbePath, Ioctl.O_RDONLY);
					if(fd < 0)
					{
						int errno = Marshal.GetLastWin32Error();
						if(errno == 2) // ENOENT
							return false;
						throw new IOException("Couldn't open " + autoProbePath +
						                      ". (" + errno.ToString() + ")");
					}
					unsafe
					{
						ret = Ioctl.pread(fd, &val, new UIntPtr(1U),
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
				return val == unchecked((byte)'1');
			}
			set // only root can do this
			{
				int fd = -1;
				long ret;
				try
				{
					fd = Ioctl.open(autoProbePath, Ioctl.O_WRONLY);
					if(fd < 0)
					{
						int errno = Marshal.GetLastWin32Error();
						if(errno == 2) // ENOENT
							throw new FileNotFoundException("", autoProbePath);
						throw new IOException("Couldn't open " + autoProbePath +
						                      ". (" + errno.ToString() + ")");
					}
					byte val = unchecked(value ? (byte)'1' : (byte)'0');
					unsafe
					{
						ret = Ioctl.write(fd, &val, new UIntPtr(1U)).ToInt64();
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
			}
		}
	}
}
