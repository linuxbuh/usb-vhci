/*
 * RegularDescriptor_TailByteCollection.cs -- USB related classes
 *
 * Copyright (C) 2009-2010 Michael Singer <michael@a-singer.de>
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
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Usb
{
	public partial class RegularDescriptor
	{
		public sealed class TailByteCollection : Collection<byte>
		{
			public event EventHandler Changed;

			public TailByteCollection() : base()
			{
			}

			public TailByteCollection(IList<byte> list) : base(list)
			{
			}

			public TailByteCollection(IEnumerable<byte> collection) : base()
			{
				AddRange(collection);
			}

			public TailByteCollection(IList<byte> source,
			                          int sourceIndex,
			                          int count) : base()
			{
				AddRange(source, sourceIndex, count);
			}

			private void OnChanged(EventArgs e)
			{
				if(Changed != null)
					Changed(this, e);
			}

			protected override sealed void InsertItem(int index, byte item)
			{
				base.InsertItem(index, item);
				OnChanged(null);
			}

			protected override sealed void RemoveItem(int index)
			{
				base.RemoveItem(index);
				OnChanged(null);
			}

			protected override sealed void SetItem(int index, byte item)
			{
				base.SetItem(index, item);
				OnChanged(null);
			}

			protected override sealed void ClearItems()
			{
				bool empty = Count == 0;
				base.ClearItems();
				if(!empty) OnChanged(null);
			}

			public void AddRange(IEnumerable<byte> collection)
			{
				InsertRange(Count, collection);
			}

			public void AddRange(IList<byte> source, int sourceIndex, int count)
			{
				InsertRange(Count, source, sourceIndex, count);
			}

			public void InsertRange(int index, IEnumerable<byte> collection)
			{
				if(collection == null)
					throw new ArgumentNullException("collection");
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(index > Count) throw new ArgumentException("", "index");
				foreach(byte b in collection) base.InsertItem(index++, b);
				OnChanged(null);
			}

			public void InsertRange(int index,
			                        IList<byte> source,
			                        int sourceIndex,
			                        int count)
			{
				if(source == null)
					throw new ArgumentNullException("source");
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(sourceIndex < 0)
					throw new ArgumentOutOfRangeException("sourceIndex",
					                                      sourceIndex,
					                                      "");
				if(count < 0)
					throw new ArgumentOutOfRangeException("count", count, "");
				if(index > Count) throw new ArgumentException("", "index");
				int l = source.Count;
				if(sourceIndex >= l)
					throw new ArgumentException("", "sourceIndex");
				if(sourceIndex + count > l)
					throw new ArgumentException();
				for(int i = 0; i < count; i++)
					base.InsertItem(index++, source[sourceIndex++]);
				OnChanged(null);
			}

			public void RemoveRange(int index, int count)
			{
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(count < 0)
					throw new ArgumentOutOfRangeException("count", count, "");
				if(index + count > Count)
					throw new ArgumentException();
				for(int i = 0; i < count; i++)
					base.RemoveItem(index);
				OnChanged(null);
			}

			public void ReplaceRange(int index, IEnumerable<byte> collection)
			{
				if(collection == null)
					throw new ArgumentNullException("collection");
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(index >= Count) throw new ArgumentException("", "index");
				foreach(byte b in collection) base.SetItem(index++, b);
				OnChanged(null);
			}

			public void ReplaceRange(int index,
			                         IList<byte> source,
			                         int sourceIndex,
			                         int count)
			{
				if(source == null)
					throw new ArgumentNullException("source");
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(sourceIndex < 0)
					throw new ArgumentOutOfRangeException("sourceIndex",
					                                      sourceIndex,
					                                      "");
				if(count < 0)
					throw new ArgumentOutOfRangeException("count", count, "");
				int c = Count;
				if(index >= c) throw new ArgumentException("", "index");
				int l = source.Count;
				if(sourceIndex >= l)
					throw new ArgumentException("", "sourceIndex");
				if(index + count > c || sourceIndex + count > l)
					throw new ArgumentException();
				for(int i = 0; i < count; i++)
					base.SetItem(index++, source[sourceIndex++]);
				OnChanged(null);
			}

			public void CopyTo([In, Out] byte[] array)
			{
				if(array == null)
					throw new ArgumentNullException("array");
				CopyTo(array, 0);
			}

			public void CopyTo(int index,
			                   [In, Out] byte[] array,
			                   int arrayIndex,
			                   int count)
			{
				if(array == null)
					throw new ArgumentNullException("array");
				if(index < 0)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(arrayIndex < 0)
					throw new ArgumentOutOfRangeException("arrayIndex",
					                                      arrayIndex,
					                                      "");
				if(count < 0)
					throw new ArgumentOutOfRangeException("count", count, "");
				int c = Count;
				if(index >= c) throw new ArgumentException("", "index");
				int l = array.Length;
				if(arrayIndex >= l)
					throw new ArgumentException("", "arrayIndex");
				if(index + count > c || arrayIndex + count > l)
					throw new ArgumentException();
				for(int i = 0; i < count; i++)
					array[arrayIndex++] = this[index++];
			}

			public byte[] ToArray()
			{
				byte[] arr = new byte[Count];
				CopyTo(arr, 0);
				return arr;
			}
		}
	}
}
