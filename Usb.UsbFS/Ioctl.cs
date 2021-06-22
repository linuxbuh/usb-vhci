/*
 * Ioctl.cs -- USB-FS related classes
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
using System.Runtime.InteropServices;

namespace Usb.UsbFS
{
	[CLSCompliant(false)]
	public static class Ioctl
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_ctrltransfer
		{
			public byte bRequestType;
			public byte bRequest;
			public ushort wValue;
			public ushort wIndex;
			public ushort wLength;
			public uint timeout; // in milliseconds
			[MarshalAs(UnmanagedType.LPArray,
			           ArraySubType=UnmanagedType.U1,
			           SizeParamIndex=3)]
			public byte[] data;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_bulktransfer
		{
			public uint ep;
			public uint len;
			public uint timeout; // in milliseconds
			[MarshalAs(UnmanagedType.LPArray,
			           ArraySubType=UnmanagedType.U1,
			           SizeParamIndex=3)]
			public byte[] data;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_ctrltransfer_ptr
		{
			public byte bRequestType;
			public byte bRequest;
			public ushort wValue;
			public ushort wIndex;
			public ushort wLength;
			public uint timeout; // in milliseconds
			public IntPtr data;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_bulktransfer_ptr
		{
			public uint ep;
			public uint len;
			public uint timeout; // in milliseconds
			public IntPtr data;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_setinterface
		{
			public uint iface;
			public uint altsetting;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_disconnectsignal
		{
			public uint signr;
			public IntPtr context;
		}
		
		public const int USBDEVFS_MAXDRIVERNAME = 255;
		
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
		public struct usbdevfs_getdriver
		{
			public uint iface;
			[MarshalAs(UnmanagedType.ByValTStr,
			           SizeConst=(USBDEVFS_MAXDRIVERNAME + 1))]
			public string driver;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_connectinfo
		{
			public uint devnum;
			public byte slow;
		}
		
		public const uint USBDEVFS_URB_SHORT_NOT_OK   = 1U;
		public const uint USBDEVFS_URB_ISO_ASAP       = 2U;

		public const byte USBDEVFS_URB_TYPE_ISO       = 0;
		public const byte USBDEVFS_URB_TYPE_INTERRUPT = 1;
		public const byte USBDEVFS_URB_TYPE_CONTROL   = 2;
		public const byte USBDEVFS_URB_TYPE_BULK      = 3;

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_iso_packet_desc
		{
			public uint length;
			public uint actual_length;
			public uint status;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_urb
		{
			public byte type;
			public byte endpoint;
			public int status;
			public uint flags;
			public IntPtr buffer;
			public int buffer_length;
			public int actual_length;
			public int start_frame;
			public int number_of_packets;
			public int error_count;
			public uint signr; // signal to be sent on error, 0 if none should
			                   // be sent
			public IntPtr usercontext;
		}

		// ioctls for talking directly to drivers
		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_ioctl
		{
			public int ifno; // interface 0..N ; negative numbers reserved
			public int ioctl_code; // MUST encode size + direction of data so
			                       // the macros in <asm/ioctl.h> give correct
			                       // values
			public IntPtr data; // param buffer (in, or out)
		}
		
		// You can do most things with hubs just through control messages,
		// except find out which device connects to which port.
		[StructLayout(LayoutKind.Sequential)]
		public struct usbdevfs_hub_portinfo
		{
			public byte nports; // number of downstream ports in this hub
			[MarshalAs(UnmanagedType.ByValArray,
			           ArraySubType=UnmanagedType.U1,
			           SizeConst=127)]
			public byte[] port; // e.g. port 3 connects to device 27
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct pollfd
		{
			public int fd;
			public short events;
			public short revents;
		}

		public static readonly int USBDEVFS_CONTROL;
		public static readonly int USBDEVFS_BULK;
		public static readonly int USBDEVFS_RESETEP;
		public static readonly int USBDEVFS_SETINTERFACE;
		public static readonly int USBDEVFS_SETCONFIGURATION;
		public static readonly int USBDEVFS_GETDRIVER;
		public static readonly int USBDEVFS_SUBMITURB;
		public static readonly int USBDEVFS_DISCARDURB;
		public static readonly int USBDEVFS_REAPURB;
		public static readonly int USBDEVFS_REAPURBNDELAY;
		public static readonly int USBDEVFS_DISCSIGNAL;
		public static readonly int USBDEVFS_CLAIMINTERFACE;
		public static readonly int USBDEVFS_RELEASEINTERFACE;
		public static readonly int USBDEVFS_CONNECTINFO;
		public static readonly int USBDEVFS_IOCTL;
		public static readonly int USBDEVFS_HUB_PORTINFO;
		public static readonly int USBDEVFS_RESET;
		public static readonly int USBDEVFS_CLEAR_HALT;
		public static readonly int USBDEVFS_DISCONNECT;
		public static readonly int USBDEVFS_CONNECT;

		public const int URB_SHORT_NOT_OK = 0x0001;
		public const int URB_ISO_ASAP     = 0x0002;
		public const int URB_ZERO_PACKET  = 0x0040;

		public const int O_RDONLY = 0x00000000;
		public const int O_WRONLY = 0x00000001;
		public const int O_RDWR   = 0x00000002;
		public const short POLLOUT    = 0x0004;
		public const short POLLERR    = 0x0008;
		public const short POLLHUP    = 0x0010;
		public const short POLLWRNORM = 0x0100;

		// from "linux-2.6.24.2/drivers/usb/core/devio.c"
		public const int MAX_USBFS_BUFFER_SIZE = 16384; // wtf? why so small

		[DllImport("libc",
		           EntryPoint="open",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int open([In, MarshalAs(UnmanagedType.LPTStr)]
		                              string pathname,
		                              int flags);
		[DllImport("libc",
		           EntryPoint="close",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int close(int fd);
		[DllImport("libc",
		           EntryPoint="poll",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int poll([In, MarshalAs(UnmanagedType.LPArray,
		                                   ArraySubType=UnmanagedType.Struct,
		                                   SizeParamIndex=1)]
		                              pollfd[] fds,
		                              uint nfds,
		                              int timeout);
		[DllImport("libc",
		           EntryPoint="poll",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int poll(IntPtr fds,
		                              uint nfds,
		                              int timeout);
		[DllImport("libc",
		           EntryPoint="poll",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int poll(ref pollfd fds,
		                              uint nfds,
		                              int timeout);
		[DllImport("libc",
		           EntryPoint="pread",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern IntPtr pread(int fd,
		                                  IntPtr buf,
		                                  UIntPtr count,
		                                  IntPtr offset);
		[DllImport("libc",
		           EntryPoint="pread",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public unsafe static extern IntPtr pread
		                             (int fd,
		                              [Out, MarshalAs(UnmanagedType.SysInt)]
		                              void *buf,
		                              UIntPtr count,
		                              IntPtr offset);

		[DllImport("libc",
		           EntryPoint="write",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public unsafe static extern IntPtr write
		                             (int fd,
		                              [Out, MarshalAs(UnmanagedType.SysInt)]
		                              void *buf,
		                              UIntPtr count);

		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref uint arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               out IntPtr arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               IntPtr arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_ctrltransfer arg,
		                               int wLength);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_bulktransfer arg,
		                               int len);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_ctrltransfer_ptr arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_bulktransfer_ptr arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_setinterface arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               ref usbdevfs_getdriver arg);
		// This overload is unusable, since USB-FS remembers the address of the
		// URB structure and the address of the data buffer in it. Later, when
		// reaping the URB it writes to exactly these addresses. But these
		// addresses are only valid during the .NET marshalling while
		// submitting the URB.
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_urb arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               [In] ref usbdevfs_disconnectsignal arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               out usbdevfs_connectinfo arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               ref usbdevfs_ioctl arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true)]
		public static extern int ioctl(int fd,
		                               int cmd,
		                               out usbdevfs_hub_portinfo arg);
		
		static Ioctl()
		{
			unchecked
			{
				const int _IOC_MAGIC     = (int)'U';

				const int _IOC_NRBITS    = 8;
				const int _IOC_TYPEBITS  = 8;
				const int _IOC_SIZEBITS  = 14;
				//const int _IOC_DIRBITS   = 2;

				//const int _IOC_NRMASK    = (1 << _IOC_NRBITS) - 1;
				//const int _IOC_TYPEMASK  = (1 << _IOC_TYPEBITS) - 1;
				//const int _IOC_SIZEMASK  = (1 << _IOC_SIZEBITS) - 1;
				//const int _IOC_DIRMASK   = (1 << _IOC_DIRBITS) - 1;

				const int _IOC_NRSHIFT   = 0;
				const int _IOC_TYPESHIFT = _IOC_NRSHIFT + _IOC_NRBITS;
				const int _IOC_SIZESHIFT = _IOC_TYPESHIFT + _IOC_TYPEBITS;
				const int _IOC_DIRSHIFT  = _IOC_SIZESHIFT + _IOC_SIZEBITS;

				const int _IOC_NONE      = 0;
				const int _IOC_WRITE     = 1;
				const int _IOC_READ      = 2;

				// NOTE: There is a confusing thing about USB-FS: Most but not
				// all IOCTLs have wrong READ/WRITE flags. So don't rely on
				// them!

				// Handles CONTROL URBs synchronously
				USBDEVFS_CONTROL =
					((_IOC_WRITE | _IOC_READ) /* WRITE! */  << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(0                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_ctrltransfer))
					                                        << _IOC_SIZESHIFT);

				// Handles BULK URBs synchronously
				USBDEVFS_BULK =
					((_IOC_WRITE | _IOC_READ) /* WRITE! */  << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(2                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_bulktransfer))
					                                        << _IOC_SIZESHIFT);

				// Resets data-toggle bit to DATA0
				USBDEVFS_RESETEP =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(3                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(uint))           << _IOC_SIZESHIFT);

				USBDEVFS_SETINTERFACE =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(4                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_setinterface))
					                                        << _IOC_SIZESHIFT);

				USBDEVFS_SETCONFIGURATION =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(5                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(uint))           << _IOC_SIZESHIFT);

				USBDEVFS_GETDRIVER =
					(_IOC_WRITE /* actually: READ/WRITE */  << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(8                                      << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_getdriver))
					                                        << _IOC_SIZESHIFT);

				// see REAPURB for important explanation
				USBDEVFS_SUBMITURB =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(10                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_urb))   << _IOC_SIZESHIFT);

				// Kills an URB. The argument of this IOCTL is the user mode
				// address of the URB. The URB gets completely discarded; a
				// call to REAP_URB is not needed after this.
				USBDEVFS_DISCARDURB =
					(_IOC_NONE /* <- this one is correct */ << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(11                                     << _IOC_NRSHIFT) |
					(0                                      << _IOC_SIZESHIFT);

				// Returns the user mode address of the next completed URB. The
				// address is the same one you submitted to the kernel using
				// the SUBMITURB IOCTL.
				// If there is no completed URB in the queue, REAPURB sleeps
				// interruptable until there is one.
				// REAPURB writes the URB structure and its data buffer.
				// Because of this, SUBMITURB can't be easyly marshalled just
				// like that.
				USBDEVFS_REAPURB =
					(_IOC_WRITE /* actually: READ */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(12                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(IntPtr))         << _IOC_SIZESHIFT);

				// same as REAPURB, but returns immediately, even if there is
				// no URB to be reaped
				USBDEVFS_REAPURBNDELAY =
					(_IOC_WRITE /* actually: READ */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(13                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(IntPtr))         << _IOC_SIZESHIFT);

				// Sets a signal, which gets associated with this file
				// descriptor. And this is all. It gets stored, but never used
				// inside the kernel module.
				// -> not implemented in Linux 2.6.24.2
				USBDEVFS_DISCSIGNAL =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(14                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_disconnectsignal))
					                                        << _IOC_SIZESHIFT);

				USBDEVFS_CLAIMINTERFACE =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(15                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(uint))           << _IOC_SIZESHIFT);

				USBDEVFS_RELEASEINTERFACE =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(16                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(uint))           << _IOC_SIZESHIFT);

				// Querys the address of the USB device.
				// The second field of the structure is true if the device
				// operates at low speed.
				USBDEVFS_CONNECTINFO =
					(_IOC_WRITE /* actually: READ */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(17                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_connectinfo))
					                                        << _IOC_SIZESHIFT);

				// CONNECT and DISCONNECT have to be 'tunneled' through this
				// IOTCL. Also, it can pass IOCTLs to the driver which is
				// currently connected with the given interface.
				USBDEVFS_IOCTL =
					((_IOC_WRITE | _IOC_READ) /* correct */ << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(18                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_ioctl)) << _IOC_SIZESHIFT);

				// This one doesn't exist. :-(
				USBDEVFS_HUB_PORTINFO =
					(_IOC_READ /* would be correct */       << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(19                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(usbdevfs_hub_portinfo))
					                                        << _IOC_SIZESHIFT);

				// Resets the USB device.
				USBDEVFS_RESET =
					(_IOC_NONE /* correct */                << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(20                                     << _IOC_NRSHIFT) |
					(0                                      << _IOC_SIZESHIFT);

				// Calls usb_clear_halt
				USBDEVFS_CLEAR_HALT =
					(_IOC_READ /* actually: WRITE */        << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(21                                     << _IOC_NRSHIFT) |
					(Marshal.SizeOf(typeof(uint))           << _IOC_SIZESHIFT);

				// Cannot be called directly. Has to be 'tunneled' through the
				// IOCTL called 'IOCTL' (see above).
				// Disconnects the currently assigned driver from the
				// interface. The interface number gets passed to the
				// 'tunnel-IOCTL'. If 'you' have claimed the interface, then
				// this should have the same effect as RELEASEINTERFACE.
				USBDEVFS_DISCONNECT =
					(_IOC_NONE /* correct */                << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(22                                     << _IOC_NRSHIFT) |
					(0                                      << _IOC_SIZESHIFT);

				// Cannot be called directly. Has to be 'tunneled' through the
				// IOCTL called 'IOCTL' (see above).
				// Scans the whole bus to which this device is attached to, and
				// connects all interfaces with matching drivers. Again: This
				// is done to ALL devices on this bus, not only this one.
				USBDEVFS_CONNECT =
					(_IOC_NONE /* correct */                << _IOC_DIRSHIFT) |
					(_IOC_MAGIC                             << _IOC_TYPESHIFT) |
					(23                                     << _IOC_NRSHIFT) |
					(0                                      << _IOC_SIZESHIFT);
			}
		}
	}
}
