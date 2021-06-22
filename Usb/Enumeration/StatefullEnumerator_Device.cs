/*
 * StatefullEnumerator_Device.cs -- USB device enumeration interfaces
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

namespace Usb.Enumeration
{
	partial class StatefullEnumerator
	{
		protected class Device : IDevice
		{
			protected readonly StatefullEnumerator Enm;
			private Hub parent;
			private object tag;
			private IDevice inner;

			public Device(StatefullEnumerator enm, Hub parent, IDevice inner)
			{
				if(enm == null) throw new ArgumentNullException("enm");
				if(inner == null) throw new ArgumentNullException("inner");
				if(inner.Tag != null) throw new ArgumentException("", "inner");
				Enm = enm;
				this.parent = parent;
				this.inner = inner;
				inner.Tag = this;
			}

			public IHub Parent
			{
				get { return parent; }
			}

			public virtual IHub Root
			{
				get { return parent == null ? null : parent.Root; }
			}

			public int Tier
			{
				get { return inner.Tier; }
			}

			public int Bus
			{
				get { return inner.Bus; }
			}

			public byte Address
			{
				get { return inner.Address; }
			}

			public int[] Branch
			{
				get { return inner.Branch; }
			}

			public DataRate DataRate
			{
				get { return inner.DataRate; }
			}

			public DeviceDescriptor GetDescriptor()
			{
				return inner.GetDescriptor();
			}

			public bool IsAcquireable
			{
				get { return inner.IsAcquireable; }
			}

			public IUsbDevice Acquire()
			{
				return inner.Acquire();
			}

			public override sealed string ToString()
			{
				return inner.ToString();
			}

			public string ToString(DeviceStringFormat format)
			{
				return inner.ToString(format);
			}

			public object Tag
			{
				get { return tag; }
				set { tag = value; }
			}

			public IDevice InnerDevice
			{
				get { return inner; }
				set
				{
					if(value == null) throw new ArgumentNullException("value");
					if(inner == value) return;
					if(value.Tag != null)
						throw new ArgumentException("", "value");
					inner.Tag = null;
					inner = value;
					inner.Tag = this;
					OnRescanInnerDevice();
				}
			}

			protected virtual void OnRescanInnerDevice()
			{
			}

			public void DriversProbe()
			{
				inner.DriversProbe();
			}
		}
	}
}
