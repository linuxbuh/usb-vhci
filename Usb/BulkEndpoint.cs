/*
 * BulkEndpoint.cs -- USB related classes
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
	public class BulkEndpoint : UnidirectionalEndpoint
	{
		protected override void Init(byte[] desc, ref int pos)
		{
			int p = pos;
			base.Init(desc, ref pos);
			if((desc[p + 3] & 0x03) != 0x02)
				throw new ArgumentException("(desc[" + (p + 3) +
				                            "] & 0x03) != 0x02",
				                            "desc");
		}

		private BulkEndpoint(int pos,
		                     out int newPos,
		                     byte[] desc) : base(desc, ref pos)
		{
			newPos = pos;
		}

		private BulkEndpoint(IEndpointParent parent,
		                     byte[] desc,
		                     int pos,
		                     out int newPos) : this(pos, out newPos, desc)
		{
			byte epadr = desc[pos + 2];
			if(epadr != 0x7f && epadr != 0xff)
			{
				if(parent == null)
					throw new ArgumentException("desc[" + (pos + 2) + "] != " +
					                            "0x7f && desc[" + (pos + 2) +
					                            "] != 0xff && parent == null");
				parent.AddEndpoint(epadr, this);
			}
			else
				Parent = parent;
		}

		public BulkEndpoint(IEndpointParent parent,
		                    byte[] desc,
		                    ref int pos) : this(parent, desc, pos, out pos)
		{
		}

		public BulkEndpoint(byte[] desc,
		                    ref int pos) : this(null, desc, pos, out pos)
		{
		}

		public BulkEndpoint(IEndpointParent parent, byte[] desc) : base(desc)
		{
			byte epadr = desc[2];
			if(epadr != 0x7f && epadr != 0xff)
			{
				if(parent == null)
					throw new ArgumentException("desc[2] != 0x7f && " +
					                            "desc[2] != 0xff && " +
					                            "parent == null");
				parent.AddEndpoint(epadr, this);
			}
			else
				Parent = parent;
		}

		public BulkEndpoint(byte[] desc) : this(null, desc)
		{
		}

		public BulkEndpoint(
			IEndpointParent parent,
			EndpointDirection dir) : base(dir)
		{
			Parent = parent;
		}

		public BulkEndpoint(EndpointDirection dir) : this(null, dir)
		{
		}

		[CLSCompliant(false)]
		public BulkEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			ushort maxPacketSize,
			byte maxNakRate) : base(dir, maxPacketSize, maxNakRate)
		{
			Parent = parent;
		}

		[CLSCompliant(false)]
		public BulkEndpoint(
			EndpointDirection dir,
			ushort maxPacketSize,
			byte maxNakRate) : this(null, dir, maxPacketSize, maxNakRate)
		{
		}

		public BulkEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			short maxPacketSize,
			byte maxNakRate) :
			this(parent, dir, (ushort)maxPacketSize, maxNakRate)
		{
		}

		public BulkEndpoint(
			EndpointDirection dir,
			short maxPacketSize,
			byte maxNakRate) :
			this(null, dir, (ushort)maxPacketSize, maxNakRate)
		{
		}

		public sealed override EndpointType Type
		{
			get { return EndpointType.Bulk; }
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
			desc[index + 3] = 0x02;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			base.Dump(stm, prefix);
			stm.WriteLine(prefix + "MaxNakRate:     " + MaxNakRate.ToString());
		}
	}
}
