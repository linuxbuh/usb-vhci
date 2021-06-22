/*
 * RegularDescriptor_CustomDescriptorCollection.cs -- USB related classes
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
using System.Collections.ObjectModel;

namespace Usb
{
	partial class RegularDescriptor
	{
		private sealed class CustomDescriptorCollection :
			Collection<CustomDescriptor>
		{
			public event
				EventHandler<CollectionModifiedEventArgs<CustomDescriptor>>
					DescriptorAdded;
			public event
				EventHandler<CollectionModifiedEventArgs<CustomDescriptor>>
					DescriptorRemoved;

			private void OnDescriptorAdded(CollectionModifiedEventArgs
			                               <CustomDescriptor> e)
			{
				if(DescriptorAdded != null)
					DescriptorAdded(this, e);
			}

			private void OnDescriptorRemoved(CollectionModifiedEventArgs
			                                 <CustomDescriptor> e)
			{
				if(DescriptorRemoved != null)
					DescriptorRemoved(this, e);
			}

			protected override void InsertItem(int index, CustomDescriptor item)
			{
				if(item == null) throw new ArgumentNullException("item");
				base.InsertItem(index, item);
				OnDescriptorAdded(new CollectionModifiedEventArgs
				                  <CustomDescriptor>(item));
			}

			protected override void SetItem(int index, CustomDescriptor item)
			{
				if(item == null) throw new ArgumentNullException("item");
				CustomDescriptor prev = Items[index];
				if(prev != item)
				{
					base.SetItem(index, item);
					OnDescriptorRemoved(new CollectionModifiedEventArgs
					                    <CustomDescriptor>(prev));
					OnDescriptorAdded(new CollectionModifiedEventArgs
					                  <CustomDescriptor>(item));
				}
			}

			protected override void RemoveItem(int index)
			{
				CustomDescriptor item = Items[index];
				base.RemoveItem(index);
				OnDescriptorRemoved(new CollectionModifiedEventArgs
				                    <CustomDescriptor>(item));
			}

			protected override void ClearItems()
			{
				CustomDescriptor[] items = new CustomDescriptor[Count];
				Items.CopyTo(items, 0);
				base.ClearItems();
				foreach(CustomDescriptor item in items)
					OnDescriptorRemoved(new CollectionModifiedEventArgs
					                    <CustomDescriptor>(item));
			}
		}
	}
}
