/*
 * Ioctl.cs -- VHCI related classes
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
using System.Runtime.InteropServices;

namespace Vhci.Linux
{
	[CLSCompliant(false)]
	public static class Ioctl
	{
		[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
		public struct vhci_ioc_register
		{
			public int id;
			public int usb_busnum;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst=20)]
			public string bus_id;
			public byte port_count;
		}

		// Not all ARM processors are able to do unaligned memory access therefore
		// Size%4 should be 0. Mono should do this on its own, but it doesn't.
		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct vhci_ioc_port_stat
		{
			public ushort status;
			public ushort change;
			public byte index;
			public byte flags;
		}

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct vhci_ioc_setup_packet
		{
			public byte bmRequestType;
			public byte bRequest;
			public ushort wValue;
			public ushort wIndex;
			public ushort wLength;
		}

		[StructLayout(LayoutKind.Sequential, Pack=4)]
		public struct vhci_ioc_urb
		{
			public vhci_ioc_setup_packet setup_packet;
			public int buffer_length;
			public int interval;
			public int packet_count;
			public ushort flags;
			public byte address;
			public byte endpoint;
			public byte type;
		}

		[StructLayout(LayoutKind.Explicit, Pack=4)]
		public struct vhci_ioc_work_union
		{
			[FieldOffset(0)]
			public vhci_ioc_urb urb;
			[FieldOffset(0)]
			public vhci_ioc_port_stat port;
		}

		[StructLayout(LayoutKind.Sequential, Pack=4)]
		public struct vhci_ioc_work
		{
			public ulong handle;
			public vhci_ioc_work_union work;
			public short timeout;
			public byte type;
		}

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct vhci_ioc_iso_packet_data
		{
			public uint offset;
			public uint packet_length;
		}

		[StructLayout(LayoutKind.Sequential, Pack=4)]
		public struct vhci_ioc_urb_data
		{
			public ulong handle;
			public IntPtr buffer;
			public IntPtr iso_packets;
			public int buffer_length;
			public int packet_count;
		}

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct vhci_ioc_iso_packet_giveback
		{
			public uint packet_actual;
			public int status;
		}

		[StructLayout(LayoutKind.Sequential, Pack=4)]
		public struct vhci_ioc_giveback
		{
			public ulong handle;
			public IntPtr buffer;
			public IntPtr iso_packets;
			public int status;
			public int buffer_actual;
			public int packet_count;
			public int error_count;
		}

		public const byte IOC_MAGIC = 138;
		public static readonly uint IOCREGISTER;
		public static readonly uint IOCPORTSTAT;
		public static readonly uint IOCFETCHWORK;
		public static readonly uint IOCGIVEBACK;
		public static readonly uint IOCFETCHDATA;

		public const ushort USB_PORT_STAT_CONNECTION    = 0x0001;
		public const ushort USB_PORT_STAT_ENABLE        = 0x0002;
		public const ushort USB_PORT_STAT_SUSPEND       = 0x0004;
		public const ushort USB_PORT_STAT_OVERCURRENT   = 0x0008;
		public const ushort USB_PORT_STAT_RESET         = 0x0010;
		public const ushort USB_PORT_STAT_POWER         = 0x0100;
		public const ushort USB_PORT_STAT_LOW_SPEED     = 0x0200;
		public const ushort USB_PORT_STAT_HIGH_SPEED    = 0x0400;
		//public const ushort USB_PORT_STAT_TEST          = 0x0800;
		//public const ushort USB_PORT_STAT_INDICATOR     = 0x1000;

		public const ushort USB_PORT_STAT_C_CONNECTION  = 0x0001;
		public const ushort USB_PORT_STAT_C_ENABLE      = 0x0002;
		public const ushort USB_PORT_STAT_C_SUSPEND     = 0x0004;
		public const ushort USB_PORT_STAT_C_OVERCURRENT = 0x0008;
		public const ushort USB_PORT_STAT_C_RESET       = 0x0010;

		public const ushort USB_PORT_STAT_MASK =
			USB_PORT_STAT_CONNECTION |
			USB_PORT_STAT_ENABLE |
			USB_PORT_STAT_SUSPEND |
			USB_PORT_STAT_OVERCURRENT |
			USB_PORT_STAT_RESET |
			USB_PORT_STAT_POWER |
			USB_PORT_STAT_LOW_SPEED |
			USB_PORT_STAT_HIGH_SPEED;

		public const ushort USB_PORT_STAT_C_MASK =
			USB_PORT_STAT_C_CONNECTION |
			USB_PORT_STAT_C_ENABLE |
			USB_PORT_STAT_C_SUSPEND |
			USB_PORT_STAT_C_OVERCURRENT |
			USB_PORT_STAT_C_RESET;

		public const byte VHCI_IOC_WORK_TYPE_PORT_STAT           = 0;
		public const byte VHCI_IOC_WORK_TYPE_PROCESS_URB         = 1;
		public const byte VHCI_IOC_WORK_TYPE_CANCEL_URB          = 2;

		public const byte VHCI_IOC_URB_TYPE_ISO                  = 0;
		public const byte VHCI_IOC_URB_TYPE_INT                  = 1;
		public const byte VHCI_IOC_URB_TYPE_CONTROL              = 2;
		public const byte VHCI_IOC_URB_TYPE_BULK                 = 3;

		public const ushort VHCI_IOC_URB_FLAGS_SHORT_NOT_OK      = 0x0001;
		public const ushort VHCI_IOC_URB_FLAGS_ISO_ASAP          = 0x0002;
		public const ushort VHCI_IOC_URB_FLAGS_ZERO_PACKET       = 0x0040;

		public const byte VHCI_IOC_PORT_STAT_FLAGS_RESUMING      = 0x01;

		public const int O_RDWR = 0x00000002;

		public const string VhciCtrlFile = "/dev/usb-vhci";

		[DllImport("libc",
		           EntryPoint="open",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=true)]
		public static extern int open([In, MarshalAs(UnmanagedType.LPTStr)]
		                              string pathname,
		                              int flags);
		[DllImport("libc",
		           EntryPoint="close",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern int close(int fd);

		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=true,
		           ThrowOnUnmappableChar=false)]
		public static extern int ioctl(int fd,
		                               uint cmd,
		                               ref vhci_ioc_register arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern int ioctl(int fd,
		                               uint cmd,
		                               [In] ref vhci_ioc_port_stat arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern int ioctl(int fd,
		                               uint cmd,
		                               out vhci_ioc_work arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern int ioctl(int fd,
		                               uint cmd,
		                               [In] ref vhci_ioc_giveback arg);
		[DllImport("libc",
		           EntryPoint="ioctl",
		           ExactSpelling=true,
		           CallingConvention=CallingConvention.Cdecl,
		           CharSet=CharSet.Ansi,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern int ioctl(int fd,
		                               uint cmd,
		                               [In] ref vhci_ioc_urb_data arg);

		static Ioctl()
		{
			unchecked
			{
				const int _IOC_NRBITS    = 8;
				const int _IOC_TYPEBITS  = 8;
				const int _IOC_SIZEBITS  = 14;
				//const int _IOC_DIRBITS   = 2;

				//const uint _IOC_NRMASK   = (1U << _IOC_NRBITS) - 1U;
				//const uint _IOC_TYPEMASK = (1U << _IOC_TYPEBITS) - 1U;
				//const uint _IOC_SIZEMASK = (1U << _IOC_SIZEBITS) - 1U;
				//const uint _IOC_DIRMASK  = (1U << _IOC_DIRBITS) - 1U;

				const int _IOC_NRSHIFT   = 0;
				const int _IOC_TYPESHIFT = _IOC_NRSHIFT + _IOC_NRBITS;
				const int _IOC_SIZESHIFT = _IOC_TYPESHIFT + _IOC_TYPEBITS;
				const int _IOC_DIRSHIFT  = _IOC_SIZESHIFT + _IOC_SIZEBITS;

				//const byte _IOC_NONE     = 0;
				const byte _IOC_WRITE    = 1;
				const byte _IOC_READ     = 2;

				IOCREGISTER  =
					((uint)(_IOC_WRITE | _IOC_READ)         << _IOC_DIRSHIFT) |
					((uint)IOC_MAGIC                        << _IOC_TYPESHIFT) |
					(0U                                     << _IOC_NRSHIFT) |
					((uint)Marshal.SizeOf(typeof(vhci_ioc_register))
					                                        << _IOC_SIZESHIFT);

				IOCPORTSTAT  =
					((uint)_IOC_WRITE                       << _IOC_DIRSHIFT) |
					((uint)IOC_MAGIC                        << _IOC_TYPESHIFT) |
					(1U                                     << _IOC_NRSHIFT) |
					((uint)Marshal.SizeOf(typeof(vhci_ioc_port_stat))
					                                        << _IOC_SIZESHIFT);

				IOCFETCHWORK =
					((uint)_IOC_READ                        << _IOC_DIRSHIFT) |
					((uint)IOC_MAGIC                        << _IOC_TYPESHIFT) |
					(2U                                     << _IOC_NRSHIFT) |
					((uint)Marshal.SizeOf(typeof(vhci_ioc_work))
					                                        << _IOC_SIZESHIFT);

				IOCGIVEBACK  =
					((uint)_IOC_WRITE                       << _IOC_DIRSHIFT) |
					((uint)IOC_MAGIC                        << _IOC_TYPESHIFT) |
					(3U                                     << _IOC_NRSHIFT) |
					((uint)Marshal.SizeOf(typeof(vhci_ioc_giveback))
					                                        << _IOC_SIZESHIFT);

				IOCFETCHDATA =
					((uint)_IOC_WRITE                       << _IOC_DIRSHIFT) |
					((uint)IOC_MAGIC                        << _IOC_TYPESHIFT) |
					(4U                                     << _IOC_NRSHIFT) |
					((uint)Marshal.SizeOf(typeof(vhci_ioc_urb_data))
					                                        << _IOC_SIZESHIFT);
			}
		}
	}
}
