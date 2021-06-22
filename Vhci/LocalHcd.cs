/*
 * LocalHcd.cs -- VHCI related classes
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

namespace Vhci
{
	public abstract class LocalHcd : Hcd
	{
		protected LocalHcd(byte ports) : base(ports) { }
		public abstract int VhciID { get; }
		public abstract string BusID { get; }
		public abstract int UsbBusNum { get; }

		public static LocalHcd Create(byte ports)
		{
			switch(Environment.OSVersion.Platform)
			{
			case PlatformID.Win32S:
			case PlatformID.Win32Windows:
			case PlatformID.Win32NT:
			case PlatformID.WinCE:
				return new Vhci.Windows.LocalHcd(ports);
			case PlatformID.Unix:
				return new Vhci.Linux.LocalHcd(ports);
			default:
				throw new NotSupportedException();
			}
		}
	}
}
