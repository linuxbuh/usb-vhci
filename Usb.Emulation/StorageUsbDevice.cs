/*
 * StorageUsbDevice.cs -- USB emulation
 *
 * Copyright (C) 2009 Conemis AG Karlsruhe Germany
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

namespace Usb.Emulation
{
	public abstract class StorageUsbDevice : UsbDeviceBase
	{
		private ulong serial;

		protected StorageUsbDevice(Usb.DataRate dataRate, long serial) : base(null, dataRate)
		{
			this.serial = unchecked((ulong)serial);
			Device.SerialNumberStringIndex = 1;
			Device.AddConfiguration(1, new Usb.ConfigurationDescriptor());
			Device[1].AddInterface(0, new Usb.Interface());
			Device[1][0].AddAlternateSetting(0, new Usb.AlternateSetting());
			Device[1][0][0].AddEndpoint(0x01, new Usb.BulkEndpoint(Usb.EndpointDirection.Out));
			Device[1][0][0].AddEndpoint(0x81, new Usb.BulkEndpoint(Usb.EndpointDirection.In));
			Device[1][0][0].InterfaceClass = 0x08; // Mass Storage Class
			Device[1][0][0].InterfaceSubClass = 0x01; // Reduced Block Commands (RBC)
			Device[1][0][0].InterfaceProtocol = 0x50; // Bulk-Only Transport
			Device[1][0][0][0x01].MaxPacketSize = 512;
			Device[1][0][0][0x81].MaxPacketSize = 512;
		}

		protected override bool OnProcGetStringDescriptor(Usb.ControlUrb urb, byte index)
		{
			ushort wLength = (ushort)urb.SetupPacketLength;
			switch(index)
			{
			case 1:
				byte[] b = (new Usb.StringDescriptor(serial.ToString("X16"))).GetDescriptor();
				int l = Math.Min(b.Length, wLength);
				Array.Copy(b, urb.TransferBuffer, l);
				urb.BufferActual = l;
				urb.Ack();
				return true;
			default:
				return base.OnProcGetStringDescriptor(urb, index);
			}
		}
	}
}
