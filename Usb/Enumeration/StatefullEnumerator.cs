/*
 * StatefullEnumerator.cs -- USB device enumeration interfaces
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
	public partial class StatefullEnumerator : IEnumerator
	{
		protected readonly IEnumerator StatelessEnumerator;
		private object tag;
		private SortedDictionary<IDevice, RootHub> state =
			new SortedDictionary<IDevice, RootHub>(DeviceComparer.Default);

		public event EventHandler<CollectionModifiedEventArgs<IDevice>>
			DeviceAdded;
		public event EventHandler<CollectionModifiedEventArgs<IDevice>>
			DeviceRemoved;

		public StatefullEnumerator(IEnumerator e)
		{
			if(e == null) throw new ArgumentNullException("e");
			StatelessEnumerator = e;
		}

		public virtual IEnumerable<IHub> Scan()
		{
			List<IHub> cur = new List<IHub>(StatelessEnumerator.Scan());
			List<RootHub> rhs = new List<RootHub>(state.Values);

			// update/remove existing root-hubs
			foreach(RootHub rh in rhs)
			{
				// check if rh still exists
				IHub h =
					cur.Find(i => DeviceComparer.Default.Compare(rh, i) == 0);
				if(h != null)
				{
					cur.Remove(h);
					rh.InnerDevice = h;
				}
				else
				{
					state.Remove(rh);
					OnDeviceRemoved(new CollectionModifiedEventArgs<IDevice>
					                (rh));
				}
			}

			// add new root-hubs
			foreach(IHub rh in cur)
			{
				RootHub nrh = new RootHub(this, rh);
				state.Add(nrh, nrh);
				OnDeviceAdded(new CollectionModifiedEventArgs<IDevice>(nrh));
			}

			return State;
		}

		protected virtual void
			OnDeviceAdded(CollectionModifiedEventArgs<IDevice> e)
		{
			if(DeviceAdded != null)
				DeviceAdded(this, e);
			IHub h = e.Item as IHub;
			if(h != null)
			{
				foreach(IDevice d in h)
				{
					OnDeviceAdded(new CollectionModifiedEventArgs<IDevice>(d));
				}
			}
		}

		protected virtual void
			OnDeviceRemoved(CollectionModifiedEventArgs<IDevice> e)
		{
			IHub h = e.Item as IHub;
			if(h != null)
			{
				foreach(IDevice d in h)
				{
					OnDeviceRemoved
						(new CollectionModifiedEventArgs<IDevice>(d));
				}
			}
			if(DeviceRemoved != null)
				DeviceRemoved(this, e);
		}

		public bool IsAvailable
		{
			get { return StatelessEnumerator.IsAvailable; }
		}

		public object Tag
		{
			get { return tag; }
			set { tag = value; }
		}

		public IEnumerable<IHub> State
		{
			get
			{
				RootHub[] r = new RootHub[state.Count];
				state.Values.CopyTo(r, 0);
				return r;
			}
		}

		protected IDictionary<IDevice, RootHub> InternalState
		{
			get { return state; }
		}

		public void Reset()
		{
			state.Clear();
		}
	}
}
