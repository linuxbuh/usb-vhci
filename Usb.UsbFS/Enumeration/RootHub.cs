/*
 * RootHub.cs -- USB-FS device enumeration classes
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

namespace Usb.UsbFS.Enumeration
{
	public sealed class RootHub : Hub
	{
		private int bus;

		internal RootHub(byte address,
		                 DataRate rate,
		                 byte[] desc,
		                 string usbfsRoot,
		                 int bus)
			: base(null, address, rate, desc, usbfsRoot)
		{
			this.bus = bus;
		}

		public override bool IsRoot
		{
			get { return true; }
		}

		public override Usb.Enumeration.IHub Root
		{
			get { return this; }
		}

		public override int Tier
		{
			get { return 1; }
		}

		public override int Bus
		{
			get { return bus; }
		}
	}
}
