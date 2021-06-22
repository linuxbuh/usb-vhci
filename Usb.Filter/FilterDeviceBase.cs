/*
 * FilterDeviceBase.cs -- USB device filter classes
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

namespace Usb.Filter
{
	public abstract class FilterDeviceBase : Usb.IUsbDevice
	{
		private Usb.IUsbDevice inner;
		private bool disposed = false;
		public event EventHandler InnerDeviceChanged;

		protected FilterDeviceBase() : this(null)
		{
		}

		protected FilterDeviceBase(Usb.IUsbDevice inner)
		{
			this.inner = inner;
		}

		~FilterDeviceBase()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if(disposing)
			{
				if(inner != null) inner.Dispose();
			}
			disposed = true;
		}

		public bool IsDisposed
		{
			get { return disposed; }
		}

		public Usb.IUsbDevice InnerDevice
		{
			get { return inner; }
			set
			{
				if(inner != value)
				{
					inner = value;
					OnInnerDeviceChanged(null);
				}
			}
		}

		protected virtual void OnInnerDeviceChanged(EventArgs e)
		{
			if(InnerDeviceChanged != null)
				InnerDeviceChanged(this, e);
		}

		public virtual Usb.DeviceDescriptor Device
		{
			get { return inner.Device; }
		}

		public virtual Usb.DataRate DataRate
		{
			get { return inner.DataRate; }
		}

		public virtual Usb.DeviceState State
		{
			get { return inner.State; }
		}

		public virtual byte Address
		{
			get { return inner.Address; }
		}

		public virtual void ForgetAsyncUrb(Usb.Urb urb)
		{
			inner.ForgetAsyncUrb(urb);
		}

		public virtual void CancelAsyncUrb(Usb.Urb urb)
		{
			inner.CancelAsyncUrb(urb);
		}

		public virtual void AsyncSubmitUrb(Usb.Urb urb,
		                                   System.Threading.EventWaitHandle ewh)
		{
			inner.AsyncSubmitUrb(urb, ewh);
		}

		public virtual Usb.Urb ReapAnyAsyncUrb(int millisecondsTimeout)
		{
			return inner.ReapAnyAsyncUrb(millisecondsTimeout);
		}

		public Usb.Urb ReapAnyAsyncUrb()
		{
			return ReapAnyAsyncUrb(System.Threading.Timeout.Infinite);
		}

		public Usb.Urb ReapAnyAsyncUrb(TimeSpan timeout)
		{
			double d = timeout.TotalMilliseconds;
			if(d > (double)int.MaxValue)
				throw new ArgumentOutOfRangeException("timeout", timeout, "");
			int t;
			if(d < 0.0)
			{
				if(d != (double)System.Threading.Timeout.Infinite)
					throw new ArgumentOutOfRangeException("timeout",
					                                      timeout, "");
				t = System.Threading.Timeout.Infinite;
			}
			else t = (int)d;
			return ReapAnyAsyncUrb(t);
		}

		public virtual bool ReapAsyncUrb(Usb.Urb urb)
		{
			return inner.ReapAsyncUrb(urb);
		}

		public virtual void ProcControlUrb(Usb.ControlUrb urb)
		{
			inner.ProcControlUrb(urb);
		}

		public virtual void ProcBulkUrb(Usb.BulkUrb urb)
		{
			inner.ProcBulkUrb(urb);
		}
	}
}
