/*
 * USBD_STATUS.cs -- USB related classes
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

namespace Usb.Windows
{
	public static class USBD_STATUS
	{
		public const int SUCCESS                   = 0x00000000;
		public const int PENDING                   = 0x40000000;

		public const int CRC                       = unchecked((int)0xc0000001);
		public const int BTSTUFF                   = unchecked((int)0xc0000002);
		public const int DATA_TOGGLE_MISMATCH      = unchecked((int)0xc0000003);
		public const int STALL_PID                 = unchecked((int)0xc0000004);
		public const int DEV_NOT_RESPONDING        = unchecked((int)0xc0000005);
		public const int PID_CHECK_FAILURE         = unchecked((int)0xc0000006);
		public const int UNEXPECTED_PID            = unchecked((int)0xc0000007);
		public const int DATA_OVERRUN              = unchecked((int)0xc0000008);
		public const int DATA_UNDERRUN             = unchecked((int)0xc0000009);
		public const int RESERVED1                 = unchecked((int)0xc000000a);
		public const int RESERVED2                 = unchecked((int)0xc000000b);
		public const int BUFFER_OVERRUN            = unchecked((int)0xc000000c);
		public const int BUFFER_UNDERRUN           = unchecked((int)0xc000000d);
		public const int NOT_ACCESSED              = unchecked((int)0xc000000f);
		public const int FIFO                      = unchecked((int)0xc0000010);
		public const int XACT_ERROR                = unchecked((int)0xc0000011);
		public const int BABBLE_DETECTED           = unchecked((int)0xc0000012);
		public const int DATA_BUFFER_ERROR         = unchecked((int)0xc0000013);
		public const int ENDPOINT_HALTED           = unchecked((int)0xc0000030);
		public const int INVALID_URB_FUNCTION      = unchecked((int)0xc0000200);
		public const int INVALID_PARAMETER         = unchecked((int)0xc0000300);
		public const int ERROR_BUSY                = unchecked((int)0xc0000400);
		public const int REQUEST_FAILED            = unchecked((int)0xc0000500);
		public const int INVALID_PIPE_HANDLE       = unchecked((int)0xc0000600);
		public const int NO_BANDWIDTH              = unchecked((int)0xc0000700);
		public const int INTERNAL_HC_ERROR         = unchecked((int)0xc0000800);
		public const int ERROR_SHORT_TRANSFER      = unchecked((int)0xc0000900);
		public const int BAD_START_FRAME           = unchecked((int)0xc0000a00);
		public const int ISOCH_REQUEST_FAILED      = unchecked((int)0xc0000b00);
		public const int FRAME_CONTROL_OWNED       = unchecked((int)0xc0000c00);
		public const int FRAME_CONTROL_NOT_OWNED   = unchecked((int)0xc0000d00);
		public const int NOT_SUPPORTED             = unchecked((int)0xc0000e00);
		public const int INAVLID_CONFIGURATION_DESCRIPTOR
		                                           = unchecked((int)0xc0000f00);
		public const int INSUFFICIENT_RESOURCES    = unchecked((int)0xc0001000);
		public const int SET_CONFIG_FAILED         = unchecked((int)0xc0002000);
		public const int BUFFER_TOO_SMALL          = unchecked((int)0xc0003000);
		public const int INTERFACE_NOT_FOUND       = unchecked((int)0xc0004000);
		public const int INAVLID_PIPE_FLAGS        = unchecked((int)0xc0005000);
		public const int TIMEOUT                   = unchecked((int)0xc0006000);
		public const int DEVICE_GONE               = unchecked((int)0xc0007000);
		public const int STATUS_NOT_MAPPED         = unchecked((int)0xc0008000);
		public const int CANCELED                  = unchecked((int)0xc0010000);
		public const int ISO_NOT_ACCESSED_BY_HW    = unchecked((int)0xc0020000);
		public const int ISO_TD_ERROR              = unchecked((int)0xc0030000);
		public const int ISO_NA_LATE_USBPORT       = unchecked((int)0xc0040000);
		public const int ISO_NOT_ACCESSED_LATE     = unchecked((int)0xc0050000);

		// for converting the status of urbs
		// (including iso urbs! for iso packets, use the methods further below)
		public static int ToUsbdStatus(UrbStatus status, bool isoUrb)
		{
			switch(status)
			{
			case UrbStatus.Success:
				return SUCCESS;
			case UrbStatus.Pending:
				return PENDING;
			case UrbStatus.ShortPacket:
				return ERROR_SHORT_TRANSFER;
			case UrbStatus.Canceled:
				return CANCELED;
			case UrbStatus.Timedout:
				return TIMEOUT;     // TODO: is this right?
			case UrbStatus.DeviceDisabled:
			case UrbStatus.DeviceDisconnected:
				return DEVICE_GONE; // TODO: is there a way to distinguish?
			case UrbStatus.BitStuff:
				return BTSTUFF;
			case UrbStatus.Crc:
				return CRC;
			case UrbStatus.NoResponse:
				return DEV_NOT_RESPONDING;
			case UrbStatus.Babble:
				return BABBLE_DETECTED;
			case UrbStatus.Stall:
				return ENDPOINT_HALTED; // TODO: is this right?
			case UrbStatus.BufferOverrun:
				return BUFFER_OVERRUN;
			case UrbStatus.BufferUnderrun:
				return BUFFER_UNDERRUN;
			case UrbStatus.AllIsoPacketsFailed:
				return isoUrb ? ISOCH_REQUEST_FAILED : INTERNAL_HC_ERROR;
			default:
				return INTERNAL_HC_ERROR;
			}
		}
		public static UrbStatus FromUsbdStatus(int status, bool isoUrb)
		{
			switch(status)
			{
			case SUCCESS:
				return UrbStatus.Success;
			case PENDING:
				return UrbStatus.Pending;
			case ERROR_SHORT_TRANSFER:
				return UrbStatus.ShortPacket;
			case CANCELED:
				return UrbStatus.Canceled;
			case TIMEOUT:
				return UrbStatus.Timedout;
			case DEVICE_GONE:
			case NOT_ACCESSED:
				return UrbStatus.DeviceDisconnected;
			case BTSTUFF:
			case PID_CHECK_FAILURE:
			case UNEXPECTED_PID:
			case DATA_OVERRUN:
			case DATA_UNDERRUN:
			case FIFO:
			case XACT_ERROR:
			case DATA_BUFFER_ERROR:
				return UrbStatus.BitStuff;
			case CRC:
				return UrbStatus.Crc;
			case DEV_NOT_RESPONDING:
				return UrbStatus.NoResponse;
			case BABBLE_DETECTED:
				return UrbStatus.Babble;
			case ENDPOINT_HALTED:
			case STALL_PID:
				return UrbStatus.Stall;
			case BUFFER_OVERRUN:
				return UrbStatus.BufferOverrun;
			case BUFFER_UNDERRUN:
				return UrbStatus.BufferUnderrun;
			case ISOCH_REQUEST_FAILED:
				return isoUrb ? UrbStatus.AllIsoPacketsFailed : UrbStatus.Error;
			default:
				return UrbStatus.Error;
			}
		}

		// for converting the status of iso packets
		public static int ToIsoPacketsUsbdStatus(UrbStatus status)
		{
			switch(status)
			{
			case UrbStatus.Pending:
				return ISO_NOT_ACCESSED_BY_HW; // TODO: is this right?
			default:
				return ToUsbdStatus(status, false);
			}
		}
		public static UrbStatus FromIsoPacketsUsbdStatus(int status)
		{
			switch(status)
			{
			case ISO_NOT_ACCESSED_BY_HW:
				return UrbStatus.Pending; // TODO: is this right?
			default:
				return FromUsbdStatus(status, false);
			}
		}
	}
}
