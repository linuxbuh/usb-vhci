/*
 * IsochronousPacket.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
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
	public struct IsochronousPacket
	{
		private int offset;
		private int packet_length;
		private int packet_actual;
		private UrbStatus status;

		public IsochronousPacket(int offset,
		                         int packet_length,
		                         int packet_actual,
		                         UrbStatus status)
		{
			this.offset = offset;
			this.packet_length = packet_length;
			this.packet_actual = packet_actual;
			this.status = status;
		}

		public IsochronousPacket(int offset, int packet_length)
		{
			this.offset = offset;
			this.packet_length = packet_length;
			this.packet_actual = 0;
			this.status = UrbStatus.Pending;
		}

		public int Offset
		{
			get { return offset; }
		}

		public int PacketLength
		{
			get { return packet_length; }
		}

		public int PacketActual
		{
			get { return packet_actual; }
			set { packet_actual = value; }
		}

		public UrbStatus Status
		{
			get { return status; }
			set { status = value; }
		}
	}
}
