/*
 * InterruptEndpoint.cs -- USB related classes
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
	public class InterruptEndpoint : UnidirectionalEndpoint
	{
		private byte additionalTransactions;

		protected override void Init(byte[] desc, ref int pos)
		{
			int p = pos;
			base.Init(desc, ref pos);
			if((desc[p + 3] & 0x03) != 0x03)
				throw new ArgumentException("(desc[" + (p + 3) +
				                            "] & 0x03) != 0x03",
				                            "desc");
			additionalTransactions = (byte)(uint)((desc[p + 5] >> 3) & 0x03);
		}

		private InterruptEndpoint(int pos,
		                          out int newPos,
		                          byte[] desc) : base(desc, ref pos)
		{
			newPos = pos;
		}

		private InterruptEndpoint(IEndpointParent parent,
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

		public InterruptEndpoint(IEndpointParent parent,
		                         byte[] desc,
		                         ref int pos) : this(parent, desc, pos, out pos)
		{
		}

		public InterruptEndpoint(byte[] desc,
		                         ref int pos) : this(null, desc, pos, out pos)
		{
		}

		public InterruptEndpoint(
			IEndpointParent parent,
			byte[] desc) : base(desc)
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

		public InterruptEndpoint(byte[] desc) : this(null, desc)
		{
		}

		public InterruptEndpoint(
			IEndpointParent parent,
			EndpointDirection dir) : base(dir)
		{
			Parent = parent;
		}

		public InterruptEndpoint(EndpointDirection dir) : this(null, dir)
		{
		}

		[CLSCompliant(false)]
		public InterruptEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			ushort maxPacketSize,
			byte additionalTransactions,
			byte interval) : base(dir, maxPacketSize, interval)
		{
			if(additionalTransactions > 0x03)
				throw new ArgumentOutOfRangeException(
					"additionalTransactions", additionalTransactions, "");
			this.additionalTransactions = additionalTransactions;
			Parent = parent;
		}

		[CLSCompliant(false)]
		public InterruptEndpoint(
			EndpointDirection dir,
			ushort maxPacketSize,
			byte additionalTransactions,
			byte interval) :
			this(null, dir, (ushort)maxPacketSize,
			     additionalTransactions, interval)
		{
		}

		public InterruptEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			short maxPacketSize,
			byte additionalTransactions,
			byte interval) :
			this(parent, dir, (ushort)maxPacketSize,
			     additionalTransactions, interval)
		{
		}

		public InterruptEndpoint(
			EndpointDirection dir,
			short maxPacketSize,
			byte additionalTransactions,
			byte interval) :
			this(null, dir, maxPacketSize, additionalTransactions, interval)
		{
		}

		public sealed override EndpointType Type
		{
			get { return EndpointType.Interrupt; }
		}

		public byte AdditionalTransactions
		{
			get { return additionalTransactions; }
			set
			{
				if(value > 0x03)
					throw new ArgumentOutOfRangeException("value", value, "");
				if(additionalTransactions != value)
				{
					additionalTransactions = value;
					OnAdditionalTransactionsChanged(null);
				}
			}
		}

		public byte Interval
		{
			get { return RawInterval; }
			set
			{
				if(RawInterval != value)
				{
					RawInterval = value;
					OnIntervalChanged(null);
				}
			}
		}

		public event EventHandler AdditionalTransactionsChanged;
		public event EventHandler IntervalChanged;

		protected virtual void OnAdditionalTransactionsChanged(EventArgs e)
		{
			if(AdditionalTransactionsChanged != null)
				AdditionalTransactionsChanged(this, e);
		}

		protected virtual void OnIntervalChanged(EventArgs e)
		{
			if(IntervalChanged != null)
				IntervalChanged(this, e);
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			base.GetDescriptorContent(desc, index, endian);
			desc[index + 3] = 0x03;
			desc[index + 5] |= (byte)((additionalTransactions & 0x03) << 3);
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			base.Dump(stm, prefix);
			stm.WriteLine(prefix + "Interval:       " + Interval.ToString());
			stm.WriteLine(prefix + "AddTransOpp:    " +
			              additionalTransactions.ToString());
		}
	}
}
