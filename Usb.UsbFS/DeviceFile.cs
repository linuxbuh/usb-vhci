/*
 * DeviceFile.cs -- USB-FS related classes
 *
 * Copyright (C) 2008 Conemis AG Karlsruhe Germany
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb.UsbFS
{
	public class DeviceFile : IDisposable
	{
		private int fd;
		private string path;
		private volatile bool disconnected;

		public event EventHandler Disconnected;

		public DeviceFile(string path, bool ro)
		{
			this.path = path;
			fd = Ioctl.open(path, ro ? Ioctl.O_RDONLY : Ioctl.O_RDWR);
			if(fd == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == 2) // ENOENT
					throw new System.IO.FileNotFoundException("", path);
				throw new System.IO.IOException("Couldn't open " +
				                                path +
				                                ". (" + errno.ToString() + ")");
			}
		}

		public DeviceFile(string path) : this(path, false)
		{
		}

		protected virtual void OnDisconnected(EventArgs e)
		{
			if(Disconnected != null)
				Disconnected(this, e);
		}

		public string Path
		{
			get { return path; }
		}

		public int FileDescriptor
		{
			get { return fd; }
		}

		~DeviceFile()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if(fd != -1)
			{
				while(Ioctl.close(fd) == -1 &&
				      Marshal.GetLastWin32Error() == 4); // EINTR
				fd = -1;
			}
		}

		private Exception ExceptionFromErrno(int errno)
		{
			switch(errno)
			{
			case 19: //ENODEV
				if(!disconnected)
				{
					disconnected = true;
					OnDisconnected(null);
				}
				return new Usb.DeviceDisconnectedException();
			default:
				return new System.IO.IOException("Errno: " + errno.ToString());
			}
		}

		public void SetInterface(byte ifc, byte alt)
		{
			Ioctl.usbdevfs_setinterface d;
			d.iface = ifc;
			d.altsetting = alt;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_SETINTERFACE, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void SetConfiguration(byte config)
		{
			uint d = config;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_SETCONFIGURATION, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public string GetDriver(byte ifc)
		{
			Ioctl.usbdevfs_getdriver d;
			d.iface = ifc;
			d.driver = null;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_GETDRIVER, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
			return d.driver;
		}

		public void ClaimInterface(byte ifc)
		{
			uint d = ifc;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_CLAIMINTERFACE, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void ReleaseInterface(byte ifc)
		{
			uint d = ifc;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_RELEASEINTERFACE, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void ConnectInfo(out byte devnum, out bool lowspeed)
		{
			Ioctl.usbdevfs_connectinfo d;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_CONNECTINFO, out d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
			devnum = (byte)d.devnum;
			lowspeed = d.slow != 0x00;
		}

		public void Reset()
		{
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_RESET) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void ClearHalt(byte epadr)
		{
			uint d = epadr;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_CLEAR_HALT, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void Connect(byte ifc)
		{
			Ioctl.usbdevfs_ioctl d;
			d.ioctl_code = Ioctl.USBDEVFS_CONNECT;
			d.ifno = ifc;
			d.data = IntPtr.Zero;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_IOCTL, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public bool Disconnect(byte ifc)
		{
			Ioctl.usbdevfs_ioctl d;
			d.ioctl_code = Ioctl.USBDEVFS_DISCONNECT;
			d.ifno = ifc;
			d.data = IntPtr.Zero;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_IOCTL, ref d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == 61) return false; // ENODATA
				throw ExceptionFromErrno(errno);
			}
			return true;
		}

		[CLSCompliant(false)]
		public int ControlTransfer(ref Ioctl.usbdevfs_ctrltransfer urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_CONTROL,
			                      ref urb,
			                      urb.wLength);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		[CLSCompliant(false)]
		public int BulkTransfer(ref Ioctl.usbdevfs_bulktransfer urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_BULK,
			                      ref urb,
			                      (int)urb.len);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		[CLSCompliant(false)]
		public int ControlTransfer(ref Ioctl.usbdevfs_ctrltransfer_ptr urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_CONTROL,
			                      ref urb);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		[CLSCompliant(false)]
		public int BulkTransfer(ref Ioctl.usbdevfs_bulktransfer_ptr urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_BULK,
			                      ref urb);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		public int ControlTransfer(IntPtr urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_CONTROL,
			                      urb);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		public int BulkTransfer(IntPtr urb)
		{
			int ret = Ioctl.ioctl(fd,
			                      Ioctl.USBDEVFS_BULK,
			                      urb);
			return (ret == -1) ? -Marshal.GetLastWin32Error() : ret;
		}

		public void DiscardUrb(IntPtr urb)
		{
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_DISCARDURB, urb) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public void SubmitUrb(IntPtr urb)
		{
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_SUBMITURB, urb) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				throw ExceptionFromErrno(errno);
			}
		}

		public IntPtr ReapUrb()
		{
			IntPtr d;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_REAPURB, out d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == 4) return IntPtr.Zero; // EINTR
				throw ExceptionFromErrno(errno);
			}
			return d;
		}

		public IntPtr ReapUrbNoDelay()
		{
			IntPtr d;
			if(Ioctl.ioctl(fd, Ioctl.USBDEVFS_REAPURBNDELAY, out d) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == 11) return IntPtr.Zero; // EAGAIN
				throw ExceptionFromErrno(errno);
			}
			return d;
		}

		public byte[] GetDescriptors()
		{
			System.IO.MemoryStream mem = new System.IO.MemoryStream();
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
					throw ExceptionFromErrno(errno);
				}
				else if(ret > 0)
				{
					if(ret > 4096L)
					{
						throw new System.IO.IOException
							("Unexpected behavior: Syscall pread read to much");
					}
					else
					{
						mem.Write(buffer, 0, (int)ret);
						pos += (int)ret;
					}
				}
			} while(ret > 0);
			return mem.ToArray();
		}

		public void Poll(int timeout, out bool discon, out bool idle)
		{
			Ioctl.pollfd p;
			p.fd = fd;
			p.events = unchecked((short)(ushort)(uint)
			                     ((int)(ushort)Ioctl.POLLERR |
			                      (int)(ushort)Ioctl.POLLHUP |
			                      (int)(ushort)Ioctl.POLLOUT |
			                      (int)(ushort)Ioctl.POLLWRNORM));
			p.revents = 0;
			if(Ioctl.poll(ref p, 1U, timeout) < 0)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno != 4) // EINTR
					throw ExceptionFromErrno(errno);
			}
			discon = ((p.revents & Ioctl.POLLERR) != 0) ||
			         ((p.revents & Ioctl.POLLHUP) != 0);
			idle   = ((p.revents & Ioctl.POLLOUT) != 0) ||
			         ((p.revents & Ioctl.POLLWRNORM) != 0);
		}

		public bool PollDiscon(int timeout)
		{
			Ioctl.pollfd p;
			p.fd = fd;
			p.events = unchecked((short)(ushort)(uint)
			                     ((int)(ushort)Ioctl.POLLERR |
			                      (int)(ushort)Ioctl.POLLHUP));
			p.revents = 0;
			if(Ioctl.poll(ref p, 1U, timeout) < 0)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno != 4) // EINTR
					throw ExceptionFromErrno(errno);
			}
			return ((p.revents & Ioctl.POLLERR) != 0) ||
			       ((p.revents & Ioctl.POLLHUP) != 0);
		}

		public bool PollIdle(int timeout)
		{
			Ioctl.pollfd p;
			p.fd = fd;
			p.events = unchecked((short)(ushort)(uint)
			                     ((int)(ushort)Ioctl.POLLOUT |
			                      (int)(ushort)Ioctl.POLLWRNORM));
			p.revents = 0;
			if(Ioctl.poll(ref p, 1U, timeout) < 0)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno != 4) // EINTR
					throw ExceptionFromErrno(errno);
			}
			return ((p.revents & Ioctl.POLLOUT) != 0) ||
			       ((p.revents & Ioctl.POLLWRNORM) != 0);
		}

		public void UpdateDescriptorActiveState(DeviceDescriptor dev)
		{
			UpdateDescriptorActiveState(dev, false);
		}

		public void UpdateDescriptorActiveState(DeviceDescriptor dev,
		                                        bool claimAll)
		{
			if(dev == null) throw new ArgumentNullException("dev");

			// Query active configuration
			Ioctl.usbdevfs_ctrltransfer urb;
			urb.data = new byte[4];
			urb.timeout = UsbDevice.ControlTimeout;
			urb.bRequestType = 0x80;
			urb.bRequest = ControlUrb.GET_CONFIGURATION;
			urb.wValue = 0;
			urb.wIndex = 0;
			urb.wLength = 1;
			int ret = ControlTransfer(ref urb);
			if(ret < 0)
			{
				int errno = Marshal.GetLastWin32Error();
				// Some devices are STALLing the GET_CONFIGURATION request if
				// they only support one configuration.
				if(errno != 32 && // EPIPE
				   errno != 75)   // EOVERFLOW
					throw ExceptionFromErrno(errno);
			}
			// Set active config, iff GET_CONFIGURATION didn't STALL
			if(ret == 1) dev.ActiveConfigurationIndex = urb.data[0];

			// Reset state of interfaces belonging to inactive configs
			foreach(ConfigurationDescriptor conf in dev.Configurations)
				if(conf.Index != urb.data[0])
					foreach(Interface ifc in conf.Interfaces)
						ifc.IsActive = false;

			// If device is in configured state, query alternate settings of
			// each interface of the active config
			if(dev.ActiveConfiguration != null)
			{
				urb.bRequestType = 0x81;
				urb.bRequest = ControlUrb.GET_INTERFACE;
				urb.wValue = 0;
				urb.wLength = 1;
				foreach(KeyValuePair<byte, Interface> kvp
				        in dev.ActiveConfiguration)
				{
					try
					{
						if(claimAll)
						{
							Disconnect(kvp.Key);
							ClaimInterface(kvp.Key);
							try { kvp.Value.Owner = GetDriver(kvp.Key); }
							catch { kvp.Value.Owner = null; }
							kvp.Value.IsActive = true;
						}
						else
						{
							string owner = null;
							try { owner = GetDriver(kvp.Key); }
							catch { }
							kvp.Value.Owner = owner;
							kvp.Value.IsActive = owner != null;
						}
					}
					catch { kvp.Value.IsActive = false; }

					urb.wIndex = kvp.Key;
					urb.data[0] = 0;
					ret = ControlTransfer(ref urb);
					if(ret < 0)
					{
						int errno = Marshal.GetLastWin32Error();
						// Some devices are STALLing the GET_INTERFACE request if
						// they only support one alternate setting.
						if(errno != 32 && // EPIPE
						   errno != 16 && // EBUSY:
						                  //   if another driver has
						                  //    claimed this ifc, usbfs
						                  //    returns this error
						   errno != 113)  // EHOSTUNREACH:
						                  //   if the device was never probed
						                  //    by the kernel, then this error
						                  //    could happen
							throw ExceptionFromErrno(errno);
					}
					else if(ret == 1)
						kvp.Value.ActiveAlternateSettingIndex = urb.data[0];
				}
			}
		}
	}
}
