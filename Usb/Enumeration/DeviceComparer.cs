/*
 * DeviceComparer.cs -- USB device enumeration interfaces
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

namespace Usb.Enumeration
{
	public class DeviceComparer :
		IComparer<IDevice>,
		System.Collections.IComparer
	{
		private DeviceComparisonFlags flags = DeviceComparisonFlags.All;
		public static readonly DeviceComparer Default = new DeviceComparer();

		public DeviceComparer()
		{
		}

		public DeviceComparer(DeviceComparisonFlags flags)
		{
			this.flags = flags;
		}

		public int Compare(IDevice d1, IDevice d2)
		{
			if(d1 == d2)   return  0;
			if(d1 == null) return -1;
			if(d2 == null) return  1;

			// compare bus
			if((flags & DeviceComparisonFlags.Bus) != 0)
			{
				int r = d1.Bus.CompareTo(d2.Bus);
				if(r != 0) return r;
			}

			// compare port branch
			if((flags & DeviceComparisonFlags.Branch) != 0)
			{
				int[] b1 = d1.Branch;
				int[] b2 = d2.Branch;
				int l1 = b1.Length;
				int l2 = b2.Length;
				int r;
				for(int i = 0; i < l1 && i < l2; i++)
				{
					r = b1[i].CompareTo(b2[i]);
					if(r != 0) return r;
				}
				r = l1.CompareTo(l2);
				if(r != 0) return r;
			}

			// compare data rate
			if((flags & DeviceComparisonFlags.DataRate) != 0)
			{
				int r = ((int)d1.DataRate ^ 1).CompareTo((int)d2.DataRate ^ 1);
				if(r != 0) return r;
			}

			// compare address
			if((flags & DeviceComparisonFlags.Address) != 0)
			{
				int r = d1.Address.CompareTo(d2.Address);
				if(r != 0) return r;
			}

			// compare type
			if((flags & DeviceComparisonFlags.Type) != 0)
			{
				int r = (!(d1 is IHub)).CompareTo(!(d2 is IHub));
				if(r != 0) return r;
			}

			DeviceDescriptor desc1 = d1.GetDescriptor();
			DeviceDescriptor desc2 = d2.GetDescriptor();
			ushort vid1 = unchecked((ushort)desc1.VendorID);
			ushort vid2 = unchecked((ushort)desc2.VendorID);
			ushort pid1 = unchecked((ushort)desc1.ProductID);
			ushort pid2 = unchecked((ushort)desc2.ProductID);

			// compare vendor id
			if((flags & DeviceComparisonFlags.Vendor) != 0)
			{
				int r = vid1.CompareTo(vid2);
				if(r != 0) return r;
			}

			// compare product id
			if((flags & DeviceComparisonFlags.Product) != 0)
			{
				int r = pid1.CompareTo(pid2);
				if(r != 0) return r;
			}

			return 0;
		}

		int System.Collections.IComparer.Compare(object o1, object o2)
		{
			IDevice d1 = o1 as IDevice;
			IDevice d2 = o2 as IDevice;
			return Compare(d1, d2);
		}

		public DeviceComparisonFlags Flags
		{
			get { return flags; }
		}
	}
}
