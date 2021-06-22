/*
 * ControlEndpoint.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
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

namespace Usb
{
	public class ControlEndpoint : BidirectionalEndpoint
	{
		protected override void Init(byte[] desc, ref int pos)
		{
			int p = pos;
			base.Init(desc, ref pos);
			if((desc[p + 3] & 0x03) != 0x00)
				throw new ArgumentException("(desc[" + (p + 3) +
				                            "] & 0x03) != 0x00",
				                            "desc");
		}

		private ControlEndpoint(int pos,
		                        out int newPos,
		                        byte[] desc) : base(desc, ref pos)
		{
			newPos = pos;
		}

		private ControlEndpoint(IEndpointParent parent,
		                        byte[] desc,
		                        int pos,
		                        out int newPos) : this(pos, out newPos, desc)
		{
			byte epadr = (byte)(uint)(desc[pos + 2] & 0x7f);
			if(epadr != 0x7f)
			{
				if(parent == null)
					throw new ArgumentException("desc[" + (pos + 2) + "] != " +
					                            "0x7f && parent == null");
				parent.AddEndpoint(epadr, this);
			}
			else
				Parent = parent;
		}

		public ControlEndpoint(IEndpointParent parent,
		                       byte[] desc,
		                       ref int pos) : this(parent, desc, pos, out pos)
		{
		}

		public ControlEndpoint(byte[] desc,
		                       ref int pos) : this(null, desc, pos, out pos)
		{
		}

		public ControlEndpoint(IEndpointParent parent, byte[] desc) : base(desc)
		{
			if(desc != null)
			{
				byte epadr = (byte)(uint)(desc[2] & 0x7f);
				if(epadr != 0x7f)
				{
					if(parent == null)
						throw new ArgumentException("desc[2] != 0x7f &&" +
						                            "parent == null");
					parent.AddEndpoint(epadr, this);
				}
				else
					Parent = parent;
			}
			else
				Parent = parent;
		}

		public ControlEndpoint(byte[] desc) : this(null, desc)
		{
		}

		public ControlEndpoint(IEndpointParent parent) : base()
		{
			Parent = parent;
		}

		public ControlEndpoint() : this((IEndpointParent)null)
		{
		}

		[CLSCompliant(false)]
		public ControlEndpoint(
			IEndpointParent parent,
			ushort maxPacketSize,
			byte maxNakRate) : base(maxPacketSize, maxNakRate)
		{
			Parent = parent;
		}

		[CLSCompliant(false)]
		public ControlEndpoint(
			ushort maxPacketSize,
			byte maxNakRate) : this(null, maxPacketSize, maxNakRate)
		{
		}

		public ControlEndpoint(
			IEndpointParent parent,
			short maxPacketSize,
			byte maxNakRate) : this(parent, (ushort)maxPacketSize, maxNakRate)
		{
		}

		public ControlEndpoint(
			short maxPacketSize,
			byte maxNakRate) : this(null, (ushort)maxPacketSize, maxNakRate)
		{
		}

		public sealed override EndpointType Type
		{
			get { return EndpointType.Control; }
		}

		public byte MaxNakRate
		{
			get { return RawInterval; }
			set
			{
				if(RawInterval != value)
				{
					RawInterval = value;
					OnMaxNakRateChanged(null);
				}
			}
		}

		public event EventHandler MaxNakRateChanged;

		protected virtual void OnMaxNakRateChanged(EventArgs e)
		{
			if(MaxNakRateChanged != null)
				MaxNakRateChanged(this, e);
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			base.GetDescriptorContent(desc, index, endian);
			desc[index + 3] = 0x00;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			base.Dump(stm, prefix);
			stm.WriteLine(prefix + "MaxNakRate:     " + MaxNakRate.ToString());
		}
	}
}
