/*
 * StatefullEnumerator_Hub_Interfaces.cs -- USB device enumeration interfaces
 *
 * Copyright (C) 2010 Michael Singer <michael@a-singer.de>
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

namespace Usb.Enumeration
{
	partial class StatefullEnumerator
	{
		partial class Hub
		{
			public int Count
			{
				get { return ports.Length; }
			}

			public void CopyTo([In, Out] IDevice[] arr, int index)
			{
				ports.CopyTo(arr, index);
			}

			public bool Contains(IDevice item)
			{
				foreach(Device dev in ports)
					if(dev == item) return true;
				return false;
			}

			public int IndexOf(IDevice item)
			{
				int c = ports.Length;
				for(int i = 0; i < c; i++)
					if(ports[i] == item) return i;
				return -1;
			}

			public IEnumerator<IDevice> GetEnumerator()
			{
				foreach(Device dev in ports)
					if(dev != null) yield return dev;
			}

			System.Collections.IEnumerator
				System.Collections.IEnumerable.GetEnumerator()
			{
				return (System.Collections.IEnumerator)GetEnumerator();
			}

			public IDevice this[int index]
			{
				get { return ports[index]; }
				set { throw new NotSupportedException("readonly"); }
			}

			bool ICollection<IDevice>.IsReadOnly
			{
				get { return true; }
			}

			void ICollection<IDevice>.Clear()
			{
				throw new NotSupportedException("readonly");
			}

			void ICollection<IDevice>.Add(IDevice item)
			{
				throw new NotSupportedException("readonly");
			}

			void IList<IDevice>.Insert(int index, IDevice item)
			{
				throw new NotSupportedException("readonly");
			}

			bool ICollection<IDevice>.Remove(IDevice item)
			{
				throw new NotSupportedException("readonly");
			}

			void IList<IDevice>.RemoveAt(int index)
			{
				throw new NotSupportedException("readonly");
			}
		}
	}
}
