/*
 * BidirectionalEndpoint.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2009 Michael Singer <michael@a-singer.de>
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
	public abstract class BidirectionalEndpoint : Endpoint
	{
		protected BidirectionalEndpoint() : base()
		{
		}

		protected BidirectionalEndpoint(byte[] desc, ref int pos) :
			base(desc, ref pos)
		{
		}

		protected BidirectionalEndpoint(byte[] desc) : base(desc)
		{
		}

		[CLSCompliant(false)]
		protected BidirectionalEndpoint(
			ushort maxPacketSize,
			byte interval) : base(maxPacketSize, interval)
		{
		}

		protected BidirectionalEndpoint(
			short maxPacketSize,
			byte interval) : base((ushort)maxPacketSize, interval)
		{
		}

		public override sealed byte BaseAddress
		{
			get
			{
				if(Parent == null)
					throw new InvalidOperationException("Parent == null");
				return
					(byte)(uint)(Parent.GetBaseAddressOfEndpoint(this) & 0x7f);
			}
		}
		
		public override sealed bool IsUsingAddress(byte epadr)
		{
			return ((byte)(uint)(epadr & 0x7f)) == BaseAddress;
		}

		protected override sealed bool IsAddressValid(byte epadr)
		{
			return base.IsAddressValid(epadr);
		}

		public byte OtherAddress
		{
			get { return (byte)(uint)(BaseAddress | 0x80); }
		}

		public new static bool ValidateAddress(byte epadr)
		{
			return Endpoint.ValidateAddress(epadr);
		}
	}
}
