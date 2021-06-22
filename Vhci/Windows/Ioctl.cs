/*
 * Ioctl.cs -- VHCI related classes
 *
 * Copyright (C) 2010-2016 Michael Singer <michael@a-singer.de>
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
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Vhci.Windows
{
	[CLSCompliant(false)]
	public static class Ioctl
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct SP_DEVICE_INTERFACE_DATA
		{
			public uint cbSize;
			public Guid InterfaceClassGuid;
			public uint Flags;
			public IntPtr Reserved;
		}
		public static readonly uint SP_DEVICE_INTERFACE_DATA_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)));

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct SP_DEVICE_INTERFACE_DETAIL_DATA
		{
			public uint cbSize;
			public fixed byte DevicePath[1];
		}

		public static readonly uint SP_DEVICE_INTERFACE_DETAIL_DATA_SIZE =
			(Marshal.SizeOf(typeof(IntPtr)) > 4) ?
			unchecked((uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA))) :
			unchecked((uint)(sizeof(uint) + Marshal.SystemDefaultCharSize));

		// VIRTUSB_REGISTER: Used as input buffer of IOCREGISTER.
		// If output buffers length is greater than 0, then it receives a single
		// UINT32, which represents the ID of the file context.
		[StructLayout(LayoutKind.Sequential)]
		public struct VIRTUSB_REGISTER
		{
			public byte Version;   // Set to 1
			public byte PortCount; // Max number of devices
		}
		public static readonly uint VIRTUSB_REGISTER_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_REGISTER)));

		// VIRTUSB_PORT_STAT: May be used as input buffer of IOCPORTSTAT.
		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct VIRTUSB_PORT_STAT
		{
			public ushort Status;
			public ushort Change;
			public byte PortIndex;
			public byte Flags;
			public byte _reserved1, _reserved2;
		}
		public static readonly uint VIRTUSB_PORT_STAT_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_PORT_STAT)));

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct VIRTUSB_SETUP_PACKET
		{
			public byte bmRequestType;
			public byte bRequest;
			public ushort wValue;
			public ushort wIndex;
			public ushort wLength;
		}

		[StructLayout(LayoutKind.Sequential, Size=32)]
		public struct VIRTUSB_URB
		{
			public ulong Handle;
			public VIRTUSB_SETUP_PACKET SetupPacket; // only for control urbs
			public uint BufferLength;                // number of bytes
			                                         // allocated for the buffer
			public uint PacketCount;                 // number of iso packets
			public ushort Interval;
			public ushort Flags;
			public byte Address;
			public byte Endpoint;                    // endpoint incl. direction
			public byte Type;
			public byte _reserved;
		}
		public static readonly uint VIRTUSB_URB_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_URB)));

		[StructLayout(LayoutKind.Explicit, Size=32)]
		public struct VIRTUSB_WORK_UNION
		{
			[FieldOffset(0)]
			public VIRTUSB_URB Urb;
			[FieldOffset(0)]
			public VIRTUSB_PORT_STAT PortStat;
		}

		// VIRTUSB_WORK: Used as output buffer of IOCFETCHWORK.
		[StructLayout(LayoutKind.Sequential, Size=40)]
		public struct VIRTUSB_WORK
		{
			public VIRTUSB_WORK_UNION Work;
			public byte Type;
			public uint _reserved;
		}
		public static readonly uint VIRTUSB_WORK_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_WORK)));

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct VIRTUSB_ISO_PACKET_DATA
		{
			public uint Offset;
			public uint PacketLength;
		}
		public static readonly uint VIRTUSB_ISO_PACKET_DATA_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_ISO_PACKET_DATA)));

		// VIRTUSB_URB_DATA: Used as input buffer of IOCFETCHDATA.
		// Output buffer receives the content of the URB its buffer.
		// Special case for ISO-URBs: Packet-offsets and -lengths are written
		//                            into the input buffer!
		[StructLayout(LayoutKind.Sequential, Size=24)]
		public unsafe struct VIRTUSB_URB_DATA
		{
			public ulong Handle;     // IN:  handle which identifies the urb
			public uint _reserved;
			public uint PacketCount; // IN:  number of iso packets
			public fixed ulong // VIRTUSB_ISO_PACKET_DATA
				IsoPackets[1];       // OUT: iso packets
		}
		public static readonly uint VIRTUSB_URB_DATA_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_URB_DATA)));

		[StructLayout(LayoutKind.Sequential, Size=8)]
		public struct VIRTUSB_ISO_PACKET_GIVEBACK
		{
			public uint PacketActual;
			public int Status;
		}
		public static readonly uint VIRTUSB_ISO_PACKET_GIVEBACK_SIZE =
			unchecked((uint)
			          Marshal.SizeOf(typeof(VIRTUSB_ISO_PACKET_GIVEBACK)));

		// VIRTUSB_GIVEBACK: Used as input buffer of IOCGIVEBACK.
		// For IN-URBs: Output buffer is also used as input; it contains the
		//              content of the URB its buffer. The length of the output
		//              buffer has to match BufferActual.
		[StructLayout(LayoutKind.Sequential, Size=32)]
		public unsafe struct VIRTUSB_GIVEBACK
		{
			public ulong Handle;      // IN:  handle which identifies the urb
			public uint BufferActual; // IN:  number of bytes which were
			                          //      actually transfered (for IN-ISOs
			                          //      BufferActual has to be equal to
			                          //      BufferLength; for OUT-ISOs this
			                          //      value will be ignored)
			public uint PacketCount;  // IN:  for ISO (has to match with the
			                          //      value from the urb)
			public uint ErrorCount;   // IN:  for ISO
			public int Status;        // IN:  (ignored for ISO URBs)
			public fixed ulong // VIRTUSB_ISO_PACKET_GIVEBACK
				IsoPackets[1];        // IN:  iso packets
		}
		public static readonly uint VIRTUSB_GIVEBACK_SIZE =
			unchecked((uint)Marshal.SizeOf(typeof(VIRTUSB_GIVEBACK)));

		// {1697DB2E-AF9B-4ffe-A83B-05DE2C2E33E5}
		public static readonly Guid GUID_DEVINTERFACE_VIRTUSB_BUS =
			new Guid(0x1697db2eU, 0xaf9b, 0x4ffe,
			         0xa8, 0x3b, 0x5, 0xde, 0x2c, 0x2e, 0x33, 0xe5);

		// {7DEAB311-76FE-4cdc-BF60-08F2CEF16DD7}
		public static readonly Guid GUID_DEVCLASS_VIRTUSB_BUS =
			new Guid(0x7deab311U, 0x76fe, 0x4cdc,
			         0xbf, 0x60, 0x8, 0xf2, 0xce, 0xf1, 0x6d, 0xd7);

		private const int _IOC_METHODBITS = 2;
		private const int _IOC_FUNCBITS   = 12;
		private const int _IOC_ACCESSBITS = 2;
		//private const int _IOC_TYPEBITS   = 16;

		//private const uint _IOC_METHODMASK = (1U << _IOC_METHODBITS) - 1U;
		//private const uint _IOC_FUNCMASK   = (1U << _IOC_FUNCBITS) - 1U;
		//private const uint _IOC_ACCESSMASK = (1U << _IOC_ACCESSBITS) - 1U;
		//private const uint _IOC_TYPEMASK   = (1U << _IOC_TYPEBITS) - 1U;

		private const int _IOC_METHODSHIFT = 0;
		private const int _IOC_FUNCSHIFT   = _IOC_METHODSHIFT + _IOC_METHODBITS;
		private const int _IOC_ACCESSSHIFT = _IOC_FUNCSHIFT + _IOC_FUNCBITS;
		private const int _IOC_TYPESHIFT   = _IOC_ACCESSSHIFT + _IOC_ACCESSBITS;

		private const byte _IOC_METHOD_BUFFERED   = 0;
		//private const byte _IOC_METHOD_IN_DIRECT  = 1;
		//private const byte _IOC_METHOD_OUT_DIRECT = 2;
		private const byte _IOC_METHOD_NEITHER    = 3;

		private const byte _IOC_FILE_ANY_ACCESS   = 0;
		//private const byte _IOC_FILE_READ_ACCESS  = 1;
		//private const byte _IOC_FILE_WRITE_ACCESS = 2;

		public const ushort VIRTUSB_IOC_DEVICE_TYPE = 0xaf9b;

		public const uint VIRTUSB_IOCREGISTER  = unchecked(
			((uint)VIRTUSB_IOC_DEVICE_TYPE          << _IOC_TYPESHIFT) |
			(0x0800U                                << _IOC_FUNCSHIFT) |
			((uint)_IOC_METHOD_BUFFERED             << _IOC_METHODSHIFT) |
			((uint)_IOC_FILE_ANY_ACCESS             << _IOC_ACCESSSHIFT));

		public const uint VIRTUSB_IOCPORTSTAT  = unchecked(
			((uint)VIRTUSB_IOC_DEVICE_TYPE          << _IOC_TYPESHIFT) |
			(0x0801U                                << _IOC_FUNCSHIFT) |
			((uint)_IOC_METHOD_NEITHER              << _IOC_METHODSHIFT) |
			((uint)_IOC_FILE_ANY_ACCESS             << _IOC_ACCESSSHIFT));

		public const uint VIRTUSB_IOCFETCHWORK = unchecked(
			((uint)VIRTUSB_IOC_DEVICE_TYPE          << _IOC_TYPESHIFT) |
			(0x0802U                                << _IOC_FUNCSHIFT) |
			((uint)_IOC_METHOD_BUFFERED             << _IOC_METHODSHIFT) |
			((uint)_IOC_FILE_ANY_ACCESS             << _IOC_ACCESSSHIFT));

		public const uint VIRTUSB_IOCGIVEBACK  = unchecked(
			((uint)VIRTUSB_IOC_DEVICE_TYPE          << _IOC_TYPESHIFT) |
			(0x0803U                                << _IOC_FUNCSHIFT) |
			((uint)_IOC_METHOD_NEITHER              << _IOC_METHODSHIFT) |
			((uint)_IOC_FILE_ANY_ACCESS             << _IOC_ACCESSSHIFT));

		public const uint VIRTUSB_IOCFETCHDATA = unchecked(
			((uint)VIRTUSB_IOC_DEVICE_TYPE          << _IOC_TYPESHIFT) |
			(0x0804U                                << _IOC_FUNCSHIFT) |
			((uint)_IOC_METHOD_NEITHER              << _IOC_METHODSHIFT) |
			((uint)_IOC_FILE_ANY_ACCESS             << _IOC_ACCESSSHIFT));

		public const ushort VIRTUSB_PORT_STAT_CONNECT        = 0x0001;
		public const ushort VIRTUSB_PORT_STAT_ENABLE         = 0x0002;
		public const ushort VIRTUSB_PORT_STAT_SUSPEND        = 0x0004;
		public const ushort VIRTUSB_PORT_STAT_OVER_CURRENT   = 0x0008;
		public const ushort VIRTUSB_PORT_STAT_RESET          = 0x0010;
		public const ushort VIRTUSB_PORT_STAT_POWER          = 0x0100;
		public const ushort VIRTUSB_PORT_STAT_LOW_SPEED      = 0x0200;
		public const ushort VIRTUSB_PORT_STAT_HIGH_SPEED     = 0x0400;
		//public const ushort VIRTUSB_PORT_STAT_TEST           = 0x0800;
		//public const ushort VIRTUSB_PORT_STAT_INDICATOR      = 0x1000;

		public const ushort VIRTUSB_PORT_STAT_C_CONNECT      = 0x0001;
		public const ushort VIRTUSB_PORT_STAT_C_ENABLE       = 0x0002;
		public const ushort VIRTUSB_PORT_STAT_C_SUSPEND      = 0x0004;
		public const ushort VIRTUSB_PORT_STAT_C_OVER_CURRENT = 0x0008;
		public const ushort VIRTUSB_PORT_STAT_C_RESET        = 0x0010;

		public const ushort VIRTUSB_PORT_STAT_MASK =
			VIRTUSB_PORT_STAT_CONNECT |
			VIRTUSB_PORT_STAT_ENABLE |
			VIRTUSB_PORT_STAT_SUSPEND |
			VIRTUSB_PORT_STAT_OVER_CURRENT |
			VIRTUSB_PORT_STAT_RESET |
			VIRTUSB_PORT_STAT_POWER |
			VIRTUSB_PORT_STAT_LOW_SPEED |
			VIRTUSB_PORT_STAT_HIGH_SPEED;

		public const ushort VIRTUSB_PORT_STAT_C_MASK =
			VIRTUSB_PORT_STAT_C_CONNECT |
			VIRTUSB_PORT_STAT_C_ENABLE |
			VIRTUSB_PORT_STAT_C_SUSPEND |
			VIRTUSB_PORT_STAT_C_OVER_CURRENT |
			VIRTUSB_PORT_STAT_C_RESET;

		public const byte VIRTUSB_WORK_TYPE_PORT_STAT                 = 0;
		public const byte VIRTUSB_WORK_TYPE_PROCESS_URB               = 1;
		public const byte VIRTUSB_WORK_TYPE_CANCEL_URB                = 2;

		public const byte VIRTUSB_URB_TYPE_ISO                        = 0;
		public const byte VIRTUSB_URB_TYPE_INT                        = 1;
		public const byte VIRTUSB_URB_TYPE_CONTROL                    = 2;
		public const byte VIRTUSB_URB_TYPE_BULK                       = 3;

		public const ushort VIRTUSB_URB_FLAGS_SHORT_TRANSFER_OK       = 0x0002;
		public const ushort VIRTUSB_URB_FLAGS_START_ISO_TRANSFER_ASAP = 0x0004;

		public const byte VIRTUSB_PORT_STAT_FLAGS_RESUMING            = 0x01;

		public const int ERROR_FILE_NOT_FOUND      = 2;
		public const int ERROR_ACCESS_DENIED       = 5;
		public const int ERROR_INSUFFICIENT_BUFFER = 122;
		public const int ERROR_NO_MORE_ITEMS       = 259;
		public const int ERROR_OPERATION_ABORTED   = 995;
		public const int ERROR_IO_PENDING          = 997;
		public const int ERROR_REQUEST_ABORTED     = 1235;
		public const int ERROR_UNKNOWN_REVISION    = 1305;

		public const uint DIGCF_DEFAULT         = 0x00000001;
		public const uint DIGCF_PRESENT         = 0x00000002;
		public const uint DIGCF_ALLCLASSES      = 0x00000004;
		public const uint DIGCF_PROFILE         = 0x00000008;
		public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

		public const uint GENERIC_READ          = 0x80000000;
		public const uint GENERIC_WRITE         = 0x40000000;

		public const uint FILE_SHARE_READ       = 0x00000001;
		public const uint FILE_SHARE_WRITE      = 0x00000002;

		public const uint OPEN_EXISTING         = 3;

		public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
		public const uint FILE_FLAG_OVERLAPPED  = 0x40000000;

		[DllImport("setupapi",
		           EntryPoint="SetupDiGetClassDevs",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern IntPtr
			SetupDiGetClassDevs([In] ref Guid ClassGuid,
			                    IntPtr Enumerator,
			                    IntPtr hwndParent,
			                    uint Flags);
		[DllImport("setupapi",
		           EntryPoint="SetupDiGetClassDevs",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=true)]
		public static extern IntPtr
			SetupDiGetClassDevs(IntPtr ClassGuid,
			                    [In, MarshalAs(UnmanagedType.LPTStr)]
			                    string Enumerator,
			                    IntPtr hwndParent,
			                    uint Flags);
		[DllImport("setupapi",
		           EntryPoint="SetupDiEnumDeviceInterfaces",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern bool
			SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet,
			                            IntPtr DeviceInfoData,
			                            [In] ref Guid InterfaceClassGuid,
			                            uint MemberIndex,
			                            ref SP_DEVICE_INTERFACE_DATA
			                            DeviceInterfaceData);
		[DllImport("setupapi",
		           EntryPoint="SetupDiGetDeviceInterfaceDetail",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern bool
			SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet,
			                                [In] ref SP_DEVICE_INTERFACE_DATA
			                                DeviceInterfaceData,
			                                IntPtr DeviceInterfaceDetailData,
			                                uint DeviceInterfaceDetailDataSize,
			                                out uint RequiredSize,
			                                IntPtr DeviceInfoData);
		[DllImport("setupapi",
		           EntryPoint="SetupDiDestroyDeviceInfoList",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern bool
			SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

		[DllImport("kernel32",
		           EntryPoint="CreateFile",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=true)]
		public static extern SafeFileHandle
			CreateFile([In, MarshalAs(UnmanagedType.LPTStr)]
			           string lpFileName,
			           uint dwDesiredAccess,
			           uint dwShareMode,
			           IntPtr lpSecurityAttributes,
			           uint dwCreationDisposition,
			           uint dwFlagsAndAttributes,
			           IntPtr hTemplateFile);

		[DllImport("kernel32",
		           EntryPoint="DeviceIoControl",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static unsafe extern bool
			DeviceIoControl(SafeFileHandle hDevice,
			                uint dwIoControlCode,
			                void* lpInBuffer,
			                uint nInBufferSize,
			                void* lpOutBuffer,
			                uint nOutBufferSize,
			                [Out] uint* lpBytesReturned,
			                NativeOverlapped* lpOverlapped);

		[DllImport("kernel32",
		           EntryPoint="GetOverlappedResult",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static unsafe extern bool
			GetOverlappedResult(SafeFileHandle hFile,
			                    NativeOverlapped* lpOverlapped,
			                    [Out] uint* lpNumberOfBytesTransferred,
			                    bool bWait);

		[DllImport("kernel32",
		           EntryPoint="CancelIo",
		           ExactSpelling=false,
		           CallingConvention=CallingConvention.Winapi,
		           CharSet=CharSet.Auto,
		           SetLastError=true,
		           PreserveSig=true,
		           BestFitMapping=false,
		           ThrowOnUnmappableChar=false)]
		public static extern bool CancelIo(SafeFileHandle hFile);

		public static uint SIZEOF_VIRTUSB_URB_DATA(uint num_iso_packets)
		{
			unchecked
			{
				return (uint)((int)VIRTUSB_URB_DATA_SIZE +
				              ((int)num_iso_packets - 1) *
				              (int)VIRTUSB_ISO_PACKET_DATA_SIZE);
			}
		}

		public static uint SIZEOF_VIRTUSB_GIVEBACK(uint num_iso_packets)
		{
			unchecked
			{
				return (uint)((int)VIRTUSB_GIVEBACK_SIZE +
				              ((int)num_iso_packets - 1) *
				              (int)VIRTUSB_ISO_PACKET_GIVEBACK_SIZE);
			}
		}
	}
}
