/*
 * IDevice.cs -- USB device enumeration interfaces
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

namespace Usb.Enumeration
{
	public interface IDevice
	{
		int Tier { get; }
		int Bus { get; }
		byte Address { get; }
		int[] Branch { get; }
		DataRate DataRate { get; }
		IHub Parent { get; }
		IHub Root { get; }
		DeviceDescriptor GetDescriptor();
		bool IsAcquireable { get; }
		IUsbDevice Acquire();
		string ToString(DeviceStringFormat format);
		object Tag { get; set; }
		void DriversProbe();
	}
}
