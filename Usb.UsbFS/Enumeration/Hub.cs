/*
 * Hub.cs -- USB-FS device enumeration classes
 *
 * Copyright (C) 2009 Michael Singer <michael@a-singer.de>
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

namespace Usb.UsbFS.Enumeration
{
	public partial class Hub : Device, Usb.Enumeration.IHub
	{
		internal Device[] ports;

		internal Hub(Hub parent,
		             byte address,
		             DataRate rate,
		             byte[] desc,
		             string usbfsRoot)
			: base(parent, address, rate, desc, usbfsRoot)
		{
		}

		public override bool IsAcquireable
		{
			get { return false; }
		}

		public virtual bool IsRoot
		{
			get { return false; }
		}
	}
}
