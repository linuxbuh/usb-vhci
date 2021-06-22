/*
 * CustomDescriptor.cs -- USB related classes
 *
 * Copyright (C) 2009-2015 Michael Singer <michael@a-singer.de>
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
	public class CustomDescriptor : Descriptor
	{
		private byte[] data;
		private byte type;

		public CustomDescriptor() : base()
		{
		}

		private void Init(byte[] desc, ref int pos)
		{
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			if(l == 0)
				throw new ArgumentException("desc len is 0", "desc");
			if(al < pos++ + l)
				throw new ArgumentException();
			if(l < 2) return;
			l -= 2;
			type = desc[pos++];
			if(l > 0)
			{
				data = new byte[l];
				Array.Copy(desc, pos, data, 0, l);
				pos += l;
			}
		}

		public CustomDescriptor(byte[] desc, ref int pos) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			int p = pos;
			Init(desc, ref p);
			pos = p; // pos gets updated only if there was no exception thrown
		}

		public CustomDescriptor(byte[] desc) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] != l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p);
			}
		}

		public CustomDescriptor(byte type, byte[] data) : base()
		{
			this.type = type;
			if(data != null && data.Length != 0)
				this.data = (byte[])data.Clone();
		}

		public CustomDescriptor(byte type,
		                        byte[] data,
		                        int index,
		                        int length) : base()
		{
			if(length < 0)
				throw new ArgumentException("", "length");
			this.type = type;
			if(data == null)
			{
				if(length != 0)
					throw new ArgumentNullException("data");
				return;
			}
			int l = data.Length;
			if(index < 0 || index >= l)
				throw new ArgumentOutOfRangeException("index", length, "");
			if(l < index + length)
				throw new InvalidOperationException();
			this.data = new byte[length];
			Array.Copy(data, index, this.data, 0, length);
		}

		public override bool Validate(ValidationMode mode)
		{
			return data == null || data.Length <= 253;
		}

		public byte Type
		{
			get { return type; }
			set { type = value; }
		}

		public byte[] Data
		{
			get
			{
				return data == null ? null : (byte[])data.Clone();
			}

			set
			{
				if(value == null || value.Length == 0)
					data = null;
				else
					data = (byte[])value.Clone();
			}
		}

		public override int GetDescriptor([In, Out] byte[] desc, int index)
		{
			int dl = data == null ? 2 : checked(2 + data.Length);
			if(dl > byte.MaxValue)
				throw new InvalidOperationException();
			if(desc == null)
				return dl;
			int al = desc.Length;
			if(index < 0 || index >= al)
				throw new ArgumentOutOfRangeException("index", index, "");
			if(al < dl + index)
				throw new InvalidOperationException();
			desc[index] = (byte)dl;
			desc[index + 1] = type;
			if(data == null) return dl;
			int l = data.Length;
			if(l > dl - 2) l = dl - 2;
			data.CopyTo(desc, index + 2);
			return dl;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			stm.WriteLine(prefix + "Type:           0x" +
			              type.ToString("x2"));
			int l = data == null ? 0 : data.Length;
			stm.WriteLine(prefix + "Data (" + l.ToString() + " bytes):");
			if(l > 0) Descriptor.HexDump(data, stm, prefix + " ");
		}
	}
}
