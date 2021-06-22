/*
 * DeviceNode.cs -- VHCI GUI
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
using System.Windows.Forms;

namespace VhciGui
{
	public class DeviceNode : Node
	{
		private Usb.Enumeration.IDevice dev;
		private Usb.DeviceDescriptor desc;
		private Usb.IUsbDevice acq;

		public DeviceNode(TreeNode node,
		                  Usb.Enumeration.IDevice dev,
		                  Usb.DeviceDescriptor desc) : base(node)
		{
			if(dev == null) throw new ArgumentNullException("dev");
			this.dev = dev;
			this.desc = desc;
		}

		public Usb.Enumeration.IDevice Device
		{
			get { return dev; }
		}

		public Usb.DeviceDescriptor Descriptor
		{
			get { return desc; }
		}

		public bool IsAcquired
		{
			get { return acq != null; }
		}

		public bool IsAcquireable
		{
			get { return dev.IsAcquireable; }
		}

		public Usb.IUsbDevice Acquiree
		{
			get { return acq; }
		}

		public Usb.IUsbDevice Acquire()
		{
			if(IsAcquired) throw new InvalidOperationException();
			acq = dev.Acquire();
			return acq;
		}

		public void ReleaseAcquiree()
		{
			if(!IsAcquired) throw new InvalidOperationException();
			acq.Dispose();
			acq = null;
		}

		public void DriversProbe()
		{
			if(IsAcquired) throw new InvalidOperationException();
			dev.DriversProbe();
		}
	}
}
