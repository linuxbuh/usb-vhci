/*
 * errno.cs -- USB related classes
 *
 * Copyright (C) 2010 Michael Singer <michael@a-singer.de>
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

namespace Usb.Linux
{
	public static class errno
	{
		// NOTE: The comments are copy&pasted from
		//       Documentation/usb/error-codes.txt
		//       in the linux kernel sources.

		// URB still pending, no results yet
		// (That is, if drivers see this it's a bug.)
		public const int EINPROGRESS = -115;

		// The data read from the endpoint did not fill the specified buffer,
		// and URB_SHORT_NOT_OK was set in urb->transfer_flags.
		public const int EREMOTEIO   = -121;

		// URB was synchronously unlinked by usb_kill_urb
		public const int ENOENT      =   -2;

		// URB was asynchronously unlinked by usb_unlink_urb
		public const int ECONNRESET  = -104;

		// Synchronous USB message functions use this code to indicate timeout
		// expired before the transfer completed, and no other error was
		// reported by HC.
		public const int ETIMEDOUT   = -110;

		// The device or host controller has been disabled due to some problem
		// that could not be worked around, such as a physical disconnect.
		public const int ESHUTDOWN   = -108;

		// Device was removed. Often preceded by a burst of other errors, since
		// the hub driver doesn't detect device removal events immediately.
		public const int ENODEV      =  -19;

		// a) bitstuff error
		// b) no response packet received within the prescribed bus turn-around
		//    time
		// c) unknown USB error
		// (*, **)
		public const int EPROTO      =  -71;

		// a) CRC mismatch
		// b) no response packet received within the prescribed bus turn-around
		//    time
		// c) unknown USB error
		// (*, **)
		public const int EILSEQ      =  -84;

		// Note that often the controller hardware does not
		// distinguish among cases a), b), and c), so a
		// driver cannot tell whether there was a protocol
		// error, a failure to respond (often caused by
		// device disconnect), or some other fault.

		// No response packet received within the prescribed bus turn-around
		// time. This error may instead be reported as -EPROTO or -EILSEQ.
		// (**)
		public const int ETIME       =  -62;

		// The amount of data returned by the endpoint was greater than either
		// the max packet size of the endpoint or the remaining buffer size.
		// "Babble".
		// (*)
		public const int EOVERFLOW   =  -75;

		// Endpoint stalled.  For non-control endpoints, reset this status with
		// usb_clear_halt().
		// (**)
		public const int EPIPE       =  -32;

		// (*) Error codes like -EPROTO, -EILSEQ and -EOVERFLOW normally
		// indicate hardware problems such as bad devices (including firmware)
		// or cables.
		//
		// (**) This is also one of several codes that different kinds of host
		// controller use to indicate a transfer has failed because of device
		// disconnect.  In the interval before the hub driver starts disconnect
		// processing, devices may receive such fault reports for every request.

		// During an IN transfer, the host controller received data from an
		// endpoint faster than it could be written to system memory
		public const int ECOMM       =  -70;

		// During an OUT transfer, the host controller could not retrieve data
		// from system memory fast enough to keep up with the USB data rate
		public const int ENOSR       =  -63;

		// ISO transfer only partially completed look at individual frame status
		// for details
		public const int EXDEV       =  -18;

		// ISO madness, if this happens: Log off and go home
		public const int EINVAL      =  -22;

		// for converting the status of urbs
		// (including iso urbs! for iso packets, use the methods further below)
		public static int ToErrno(UrbStatus status, bool isoUrb)
		{
			switch(status)
			{
			case UrbStatus.Success:             return 0;
			case UrbStatus.Pending:             return EINPROGRESS;
			case UrbStatus.ShortPacket:         return EREMOTEIO;
			case UrbStatus.Error:               return isoUrb ? EXDEV : EPROTO;
			case UrbStatus.Canceled:            return ECONNRESET; // or ENOENT
			case UrbStatus.Timedout:            return ETIMEDOUT;
			case UrbStatus.DeviceDisabled:      return ESHUTDOWN;
			case UrbStatus.DeviceDisconnected:  return ENODEV;
			case UrbStatus.BitStuff:            return EPROTO;
			case UrbStatus.Crc:                 return EILSEQ;
			case UrbStatus.NoResponse:          return ETIME;
			case UrbStatus.Babble:              return EOVERFLOW;
			case UrbStatus.Stall:               return EPIPE;
			case UrbStatus.BufferOverrun:       return ECOMM;
			case UrbStatus.BufferUnderrun:      return ENOSR;
			case UrbStatus.AllIsoPacketsFailed: return isoUrb ? EINVAL : EPROTO;
			default:                            return EPROTO;
			}
		}
		public static UrbStatus FromErrno(int status, bool isoUrb)
		{
			switch(status)
			{
			case 0:           return UrbStatus.Success;
			case EINPROGRESS: return UrbStatus.Pending;
			case EREMOTEIO:   return UrbStatus.ShortPacket;
			case ENOENT:
			case ECONNRESET:  return UrbStatus.Canceled;
			case ETIMEDOUT:   return UrbStatus.Timedout;
			case ESHUTDOWN:   return UrbStatus.DeviceDisabled;
			case ENODEV:      return UrbStatus.DeviceDisconnected;
			case EPROTO:      return UrbStatus.BitStuff;
			case EILSEQ:      return UrbStatus.Crc;
			case ETIME:       return UrbStatus.NoResponse;
			case EOVERFLOW:   return UrbStatus.Babble;
			case EPIPE:       return UrbStatus.Stall;
			case ECOMM:       return UrbStatus.BufferOverrun;
			case ENOSR:       return UrbStatus.BufferUnderrun;
			case EINVAL:      return isoUrb ? UrbStatus.AllIsoPacketsFailed :
			                                  UrbStatus.Error;
			default:          return UrbStatus.Error;
			}
		}

		// for converting the status of iso packets
		public static int ToIsoPacketsErrno(UrbStatus status)
		{
			return ToErrno(status, false);
		}
		public static UrbStatus FromIsoPacketsErrno(int status)
		{
			return FromErrno(status, false);
		}
	}
}
