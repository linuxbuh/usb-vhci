/*
 * RegularDescriptor.cs -- USB related classes
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb
{
	public abstract partial class RegularDescriptor : ContainerDescriptor
	{
		// Stores additional data, not part of the USB specification, placed at
		// the end of the descriptor
		private TailByteCollection tail = new TailByteCollection();

		private CustomDescriptorCollection subs =
			new CustomDescriptorCollection();

		public abstract int RegularSize { get; }
		protected abstract void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian);

		public event EventHandler TailChanged;
		public event EventHandler<CollectionModifiedEventArgs<CustomDescriptor>>
			CustomDescriptorAdded;
		public event EventHandler<CollectionModifiedEventArgs<CustomDescriptor>>
			CustomDescriptorRemoved;

		protected RegularDescriptor() : base()
		{
			tail.Changed += OnTailChanged;
			subs.DescriptorAdded += OnCustomDescriptorAdded;
			subs.DescriptorRemoved += OnCustomDescriptorRemoved;
		}

		protected void ParseTail(byte[] desc,
		                         ref int pos)    // beginning of the descriptor
		                                         // (not the tail)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int dl = desc[pos]; 
			if(dl < 2)
				throw new ArgumentException("desc[" + pos + "] is less than 2",
				                            "desc");
			if(pos + dl > al)
				throw new ArgumentException();
			int rl = RegularSize;
			if(rl > dl) throw new ArgumentException("", "desc");
			if(pos + rl < al)
				tail.AddRange(desc, pos + rl, dl - rl);
			pos += dl;
		}

		protected void ParseCustomDescriptors(byte[] desc,
		                                      ref int pos) // points behind the
		                                                   // end of the
		                                                   // descriptor (where
		                                                   // the first custom
		                                                   // descriptor may
		                                                   // begin)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos > al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int p = pos;
			List<CustomDescriptor> tmp = new List<CustomDescriptor>();
			while(p < al)
			{
				int dl = desc[p];
				if(dl < 2)
					throw new ArgumentException("desc[" + p + "] is less " +
					                            "than 2",
					                            "desc");
				if(p + dl > al)
					throw new ArgumentException();
				byte dt = desc[p + 1];
				if(dt == 0)
					throw new ArgumentException("desc[" + (p + 1) +" ] is 0",
					                            "desc");
				if(!Descriptor.IsCustomType(dt)) break;
				tmp.Add(new CustomDescriptor(desc, ref p));
			}
			foreach(CustomDescriptor d in tmp)
				subs.Add(d);
			pos = p;
		}

		public TailByteCollection Tail
		{
			get { return tail; }
		}

		public IList<CustomDescriptor> CustomDescriptors
		{
			get { return subs; }
		}

		protected virtual void OnTailChanged(EventArgs e)
		{
			if(TailChanged != null)
				TailChanged(this, e);
		}

		protected virtual void
			OnCustomDescriptorAdded(CollectionModifiedEventArgs
			                        <CustomDescriptor> e)
		{
			if(CustomDescriptorAdded != null)
				CustomDescriptorAdded(this, e);
		}

		protected virtual void
			OnCustomDescriptorRemoved(CollectionModifiedEventArgs
			                          <CustomDescriptor> e)
		{
			if(CustomDescriptorRemoved != null)
				CustomDescriptorRemoved(this, e);
		}

		private void OnTailChanged(object sender, EventArgs e)
		{
			if(sender != tail) throw new ArgumentException("", "sender");
			OnTailChanged(e);
		}

		private void OnCustomDescriptorAdded(object sender,
		                                     CollectionModifiedEventArgs
		                                     <CustomDescriptor> e)
		{
			if(sender != subs) throw new ArgumentException("", "sender");
			OnCustomDescriptorAdded(e);
		}

		private void OnCustomDescriptorRemoved(object sender,
		                                       CollectionModifiedEventArgs
		                                       <CustomDescriptor> e)
		{
			if(sender != subs) throw new ArgumentException("", "sender");
			OnCustomDescriptorRemoved(e);
		}

		public int LocalSize
		{
			get { return RegularSize + tail.Count; }
		}

		public int GetLocalDescriptor([In, Out] byte[] desc)
		{
			return GetLocalDescriptor(desc, 0);
		}

		public int GetLocalDescriptor([In, Out] byte[] desc,
		                              Endianness endian)
		{
			return GetLocalDescriptor(desc, 0, endian);
		}

		public int GetLocalDescriptor([In, Out] byte[] desc,
		                              int index)
		{
			return GetLocalDescriptor(desc, index, Endianness.UsbSpec);
		}

		public int GetLocalDescriptor([In, Out] byte[] desc,
		                              int index,
		                              Endianness endian)
		{
			int rl = RegularSize;
			int dl = rl + Tail.Count;
			if(dl > byte.MaxValue)
				throw new InvalidOperationException();
			if(desc == null)
				return dl;
			int al = desc.Length;
			if(index < 0 || index >= al)
				throw new ArgumentOutOfRangeException("index", index, "");
			if(al < dl + index)
				throw new ArgumentException();
			desc[index] = (byte)dl;
			GetDescriptorContent(desc, index, endian);
			Tail.CopyTo(desc, index + rl);
			return dl;
		}

		public int GetDescriptor([In, Out] byte[] desc,
		                         int index,
		                         Endianness endian)
		{
			int dtl = GetLocalDescriptor(null);
			foreach(CustomDescriptor sub in subs)
				dtl += sub.GetDescriptor(null);
			dtl += GetSubDescriptors(null, 0, endian);
			if(desc == null)
				return dtl;
			int al = desc.Length;
			if(index < 0 || index >= al)
				throw new ArgumentOutOfRangeException("index", index, "");
			if(al < dtl + index)
				throw new ArgumentException();
			int pos = index + GetLocalDescriptor(desc, index, endian);
			foreach(CustomDescriptor sub in subs)
				pos += sub.GetDescriptor(desc, pos);
			GetSubDescriptors(desc, pos, endian);
			return dtl;
		}

		public override sealed int GetDescriptor([In, Out] byte[] desc,
		                                         int index)
		{
			return GetDescriptor(desc, index, Endianness.UsbSpec);
		}

		protected virtual int GetSubDescriptors([In, Out] byte[] desc,
		                                        int index,
		                                        Endianness endian)
		{
			return 0;
		}

		public byte[] GetLocalDescriptor()
		{
			return GetLocalDescriptor(Endianness.UsbSpec);
		}

		public byte[] GetLocalDescriptor(Endianness endian)
		{
			byte[] desc = new byte[GetLocalDescriptor(null)];
			GetLocalDescriptor(desc, 0, endian);
			return desc;
		}

		public byte[] GetDescriptor(Endianness endian)
		{
			byte[] desc = new byte[GetDescriptor(null)];
			GetDescriptor(desc, 0, endian);
			return desc;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			int tl = tail.Count;
			if(tl > 0)
			{
				stm.WriteLine(prefix + "Additional Data (" +
				              tl.ToString() + " bytes):");
				Descriptor.HexDump(tail, stm, prefix + " ");
			}
			string subPrefix = prefix + "  ";
			foreach(CustomDescriptor sub in subs)
			{
				stm.WriteLine(prefix + "> [custom descriptor]");
				sub.Dump(stm, subPrefix);
			}
		}
	}
}
