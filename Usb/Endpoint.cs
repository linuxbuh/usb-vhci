/*
 * Endpoint.cs -- USB related classes
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb
{
	public abstract class Endpoint : RegularDescriptor
	{
		private IEndpointParent parent;
		private ushort maxPacketSize;
		private byte interval;

		public abstract EndpointType Type { get; }
		public abstract byte BaseAddress { get; }
		public abstract bool IsUsingAddress(byte epadr);

		protected Endpoint() : base()
		{
		}

		protected virtual void Init(byte[] desc, ref int pos)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			// See USB specs 2.0 section 9.5:
			//   "If a descriptor returns with a value in its length
			//    field that is less than defined by this
			//    specification, the descriptor is invalid and
			//    should be rejected by the host."
			if(l < Descriptor.EndpointDescriptorLength)
				throw new ArgumentException("desc[" + pos + "] is less than " +
				                            Descriptor.EndpointDescriptorLength,
				                            "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(desc[pos + 1] != Descriptor.EndpointDescriptorType)
				throw new ArgumentException("desc[" + (pos + 1) + "] is not "+
				                            Descriptor.EndpointDescriptorType,
				                            "desc");
			byte epadr = desc[pos + 2];
			maxPacketSize = unchecked((ushort)((desc[pos + 4] |
			                                    desc[pos + 5] << 8) & 0x07ff));
			interval = desc[pos + 6];
			// If endpoint number is 0x7f, next free number gets set automatially
			if(epadr != 0x7f && epadr != 0xff &&
				!Endpoint.ValidateAddress(epadr))
				throw new ArgumentException("desc[" + (pos + 2) + "] contains" +
				                            " an invalid endpoint address",
				                            "desc");
			ParseTail(desc, ref pos);
		}

		protected Endpoint(byte[] desc, ref int pos) : base()
		{
			int p = pos;
			Init(desc, ref p);
			ParseCustomDescriptors(desc, ref p);
			pos = p; // if no exception thrown so far, refresh pos
		}

		protected Endpoint(byte[] desc) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p);
				ParseCustomDescriptors(desc, ref p);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
			}
		}

		[CLSCompliant(false)]
		protected Endpoint(ushort maxPacketSize, byte interval) : base()
		{
			if(maxPacketSize > 0x07ff)
				throw new ArgumentOutOfRangeException(
					"maxPacketSize", maxPacketSize, "");
			this.maxPacketSize = maxPacketSize;
			this.interval = interval;
		}

		protected Endpoint(short maxPacketSize, byte interval) :
			this((ushort)maxPacketSize, interval)
		{
		}

		public static Endpoint Create(IEndpointParent parent,
		                              byte[] desc,
		                              ref int pos)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			// See USB specs 2.0 section 9.5:
			//   "If a descriptor returns with a value in its length
			//    field that is less than defined by this
			//    specification, the descriptor is invalid and
			//    should be rejected by the host."
			if(l < Descriptor.EndpointDescriptorLength)
				throw new ArgumentException("desc[" + pos + "] is less than " +
				                            Descriptor.EndpointDescriptorLength,
				                            "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(desc[pos + 1] != Descriptor.EndpointDescriptorType)
				throw new ArgumentException("desc[" + (pos + 1) + "] is not "+
				                            Descriptor.EndpointDescriptorType,
				                            "desc");
			switch(desc[pos + 3] & 0x03)
			{
			case 0x01: return new IsochronousEndpoint(parent, desc, ref pos);
			case 0x02: return new BulkEndpoint(parent, desc, ref pos);
			case 0x03: return new InterruptEndpoint(parent, desc, ref pos);
			default:   return new ControlEndpoint(parent, desc, ref pos);
			}
		}

		public static Endpoint Create(byte[] desc, ref int pos)
		{
			return Create(null, desc, ref pos);
		}

		public static Endpoint Create(IEndpointParent parent, byte[] desc)
		{
			int p = 0;
			return Create(parent, desc, ref p);
		}

		public static Endpoint Create(byte[] desc)
		{
			return Create(null, desc);
		}

		public override bool Validate(ValidationMode mode)
		{
			// TODO: implement
			return true;
		}

		public byte Number
		{
			get
			{
				if(parent == null)
					throw new InvalidOperationException("parent == null");
				return (byte)(uint)(BaseAddress & 0x7f);
			}
		}
		
		public bool ConflictsWith(Endpoint other)
		{
			if(this.parent == null)
				throw new InvalidOperationException("this.parent == null");
			if(other.parent == null)
				throw new InvalidOperationException("other.parent == null");
			if(this is BidirectionalEndpoint)
			{
				if(other is BidirectionalEndpoint)
					return this.BaseAddress == other.BaseAddress;
				return this.BaseAddress ==
					((byte)(uint)(other.BaseAddress & 0x7f));
			}
			if(other is BidirectionalEndpoint)
				return ((byte)(uint)(this.BaseAddress & 0x7f)) ==
					other.BaseAddress;
			return this.BaseAddress == other.BaseAddress;
		}
		
		public static bool Conflicts(Endpoint ep1, Endpoint ep2)
		{
			return ep1.ConflictsWith(ep2);
		}

		public bool ConflictsWithUnidirectional(byte epadr)
		{
			if(this.parent == null)
				throw new InvalidOperationException("parent == null");
			if(this is BidirectionalEndpoint)
				return this.BaseAddress == ((byte)(uint)(epadr & 0x7f));
			return this.BaseAddress == epadr;
		}

		public bool ConflictsWithBidirectional(byte epadr)
		{
			if(this.parent == null)
				throw new InvalidOperationException("parent == null");
			if(this is BidirectionalEndpoint)
			{
				return this.BaseAddress == ((byte)(uint)(epadr & 0x7f));
			}
			return ((byte)(uint)(this.BaseAddress & 0x7f)) ==
				((byte)(uint)(epadr & 0x7f));
		}

		public static bool Conflicts(
			byte epadr1, bool bi1, byte epadr2, bool bi2)
		{
			if(bi1 || bi2)
				return ((byte)(uint)(epadr1 & 0x7f)) ==
					((byte)(uint)(epadr2 & 0x7f));
			return epadr1 == epadr2;
		}
		
		public IEndpointParent Parent
		{
			get { return parent; }
			set
			{
				if(value == parent) return;
				if(parent != null)
				{
					IEndpointParent p = parent;
					parent = null;
					p.RemoveEndpoint(this);
				}
				if(value == null)
				{
					OnParentChanged(null);
					return;
				}
				parent = value;
				if(!value.ContainsEndpoint(this))
				{
					try
					{
						value.AppendEndpoint(this);
					}
					catch
					{
						parent = null;
						throw;
					}
				}
				OnParentChanged(null);
			}
		}

		protected virtual bool IsAddressValid(byte epadr)
		{
			return ValidateAddress(epadr);
		}

		public static bool ValidateAddress(byte epadr)
		{
			return ((epadr & 0x70) == 0x00);
		}

		public static bool ValidateAddressForInstance(byte epadr, Endpoint ep)
		{
			if(ep == null) throw new ArgumentNullException("ep");
			return ep.IsAddressValid(epadr);
		}

		public short MaxPacketSize
		{
			get { return (short)maxPacketSize; }
			set
			{
				if((ushort)value > 0x07ff)
					throw new ArgumentOutOfRangeException("value", value, "");
				if(maxPacketSize != (ushort)value)
				{
					maxPacketSize = (ushort)value;
					OnMaxPacketSizeChanged(null);
				}
			}
		}
		
		protected byte RawInterval
		{
			get { return interval; }
			set { interval = value; }
		}

		public event EventHandler ParentChanged;
		public event EventHandler MaxPacketSizeChanged;

		protected virtual void OnParentChanged(EventArgs e)
		{
			if(ParentChanged != null)
				ParentChanged(this, e);
		}

		protected virtual void OnMaxPacketSizeChanged(EventArgs e)
		{
			if(MaxPacketSizeChanged != null)
				MaxPacketSizeChanged(this, e);
		}

		public override sealed int RegularSize
		{
			get { return Descriptor.EndpointDescriptorLength; }
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			desc[index + 1] = Descriptor.EndpointDescriptorType;
			byte epadr = 0x7f;
			if(parent != null) epadr = BaseAddress;
			desc[index + 2] = epadr;
			desc[index + 4] = unchecked((byte)maxPacketSize);
			desc[index + 5] = unchecked((byte)(maxPacketSize >> 8 & 0x07));
			desc[index + 6] = interval;
		}

		private sealed class BaseAddressComparerClass :
			IComparer<byte>,
			System.Collections.IComparer
		{
			public int Compare(byte x, byte y)
			{
				int nx = x << 8 & 0x7f00 | x & 0x80;
				int ny = y << 8 & 0x7f00 | y & 0x80;
				return nx.CompareTo(ny);
			}

			public int Compare(object x, object y)
			{
				if(x is byte && y is byte)
					return Compare((byte)x, (byte)y);
				else
					return (x is byte) ? 1 : ((y is byte) ? -1 : 0);
			}
		}

		public static readonly IComparer<byte> BaseAddressComparer =
			new BaseAddressComparerClass();

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			string epadr;
			if(parent != null) epadr = "0x" + BaseAddress.ToString("x2");
			else epadr = "[no parent]";
			string t;
			switch(Type)
			{
			case EndpointType.Control:     t = "CONTROL";     break;
			case EndpointType.Bulk:        t = "BULK";        break;
			case EndpointType.Interrupt:   t = "INTERRUPT";   break;
			case EndpointType.Isochronous: t = "ISOCHRONOUS"; break;
			default:                       t = "INVALID!";    break;
			}
			stm.WriteLine(prefix + "Type:           " + t);
			stm.WriteLine(prefix + "Address:        " + epadr);
			stm.WriteLine(prefix + "MaxPacketSize:  " +
			              maxPacketSize.ToString());
			base.Dump(stm, prefix);
		}
	}
}
