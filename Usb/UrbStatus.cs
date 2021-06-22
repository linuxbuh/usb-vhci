/*
 * UrbStatus.cs -- USB related classes
 *
 * Copyright (C) 2007-2009 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2010 Michael Singer <michael@a-singer.de>
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

namespace Usb
{
	[Serializable]
	public enum UrbStatus
	{
		Success               = 0x00000000,
		Pending               = 0x10000001,
		ShortPacket           = 0x10000002,
		Error                 = 0x7ff00000,
		Canceled              = 0x30000001,
		Timedout              = 0x30000002,
		DeviceDisabled        = 0x71000001,
		DeviceDisconnected    = 0x71000002,
		BitStuff              = 0x72000001,
		Crc                   = 0x72000002,
		NoResponse            = 0x72000003,
		Babble                = 0x72000004,
		Stall                 = 0x74000001,
		BufferOverrun         = 0x72100001,
		BufferUnderrun        = 0x72100002,
		AllIsoPacketsFailed   = 0x78000001
	}
}
